using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KrishiLink.BLL.Services
{
    /// <summary>
    /// Farmer → platform escrow. Every state change and its ledger row are saved in one unit of work,
    /// and <see cref="CompleteAsync"/> is idempotent so a repeated gateway callback cannot double-post.
    /// </summary>
    public interface IPaymentService
    {
        /// <summary>Checkout details for an accepted, unpaid booking of this farmer; null when it cannot be paid.</summary>
        Task<PaymentCheckoutViewModel?> GetCheckoutAsync(string farmerId, string bookingType, int bookingId);

        /// <summary>Creates a pending payment and returns the gateway URL to redirect to, or an error.</summary>
        Task<(string? Error, string? RedirectUrl)> InitiateAsync(string farmerId, string bookingType, int bookingId, string method, string? account);

        Task<PaymentSummary?> GetByGatewayReferenceAsync(string gatewayReference);

        /// <summary>Applies the gateway outcome. Returns the failure reason (null on success) and the payment.</summary>
        Task<(string? Error, PaymentSummary? Payment)> CompleteAsync(string gatewayReference, string outcome);

        /// <summary>Marks a succeeded payment refunded and posts the ledger row. Does not save — the caller commits it with the cancellation.</summary>
        Task RefundAsync(Payment payment);
    }

    public class PaymentService : IPaymentService
    {
        private readonly IRepository<EquipmentBooking> _rentals;
        private readonly IRepository<GodownBooking> _storage;
        private readonly IRepository<Payment> _payments;
        private readonly IRepository<ApplicationUser> _users;
        private readonly ILedgerRepository _ledger;
        private readonly IPaymentGateway _gateway;
        private readonly INotificationService _notifications;
        private readonly IReceiptDocumentService _receipts;
        private readonly IEmailQueue _emailQueue;
        private readonly ILogger<PaymentService> _logger;
        private readonly AppOptions _appOptions;

        public PaymentService(
            IRepository<EquipmentBooking> rentals,
            IRepository<GodownBooking> storage,
            IRepository<Payment> payments,
            IRepository<ApplicationUser> users,
            ILedgerRepository ledger,
            IPaymentGateway gateway,
            INotificationService notifications,
            IReceiptDocumentService receipts,
            IEmailQueue emailQueue,
            ILogger<PaymentService> logger,
            IOptions<AppOptions> appOptions)
        {
            _rentals = rentals;
            _storage = storage;
            _payments = payments;
            _users = users;
            _ledger = ledger;
            _gateway = gateway;
            _notifications = notifications;
            _receipts = receipts;
            _emailQueue = emailQueue;
            _logger = logger;
            _appOptions = appOptions.Value;
        }

        public async Task<PaymentCheckoutViewModel?> GetCheckoutAsync(string farmerId, string bookingType, int bookingId)
        {
            var b = await LoadAsync(bookingType, bookingId);
            if (b is null || b.Booking.FarmerId != farmerId || b.Booking.Status != BookingStatus.Accepted || b.Booking.AgreedGross is null) return null;

            var farmer = await _users.FirstOrDefaultAsync(u => u.Id == farmerId);
            var lastFailure = await _payments.Query()
                .Where(p => p.BookingType == b.Type && p.BookingId == bookingId && p.Status == PaymentStatus.Failed)
                .OrderByDescending(p => p.CreatedOn)
                .Select(p => p.FailureReason)
                .FirstOrDefaultAsync();

            return new PaymentCheckoutViewModel
            {
                BookingType = b.Type,
                BookingId = bookingId,
                BookingCode = b.Code,
                ItemName = b.ItemName,
                OwnerName = b.OwnerName,
                DateRange = ListingFormat.DateRange(b.Booking.StartDate, b.Booking.EndDate),
                QuantityText = b.QuantityText,
                RateText = b.RateText,
                Amount = b.Booking.AgreedGross.Value,
                Methods = PaymentMethods.All,
                Account = farmer?.PhoneNumber,
                LastFailureReason = lastFailure
            };
        }

        public async Task<(string? Error, string? RedirectUrl)> InitiateAsync(string farmerId, string bookingType, int bookingId, string method, string? account)
        {
            var b = await LoadAsync(bookingType, bookingId);
            if (b is null || b.Booking.FarmerId != farmerId) return ("This booking could not be found.", null);
            if (b.Booking.Status != BookingStatus.Accepted) return ($"A {b.Booking.Status.ToLowerInvariant()} booking cannot be paid for.", null);
            if (b.Booking.AgreedGross is null or <= 0) return ("This booking has no agreed price yet. Please contact the owner.", null);
            if (!PaymentMethods.All.Contains(method)) return ("Please choose a valid payment method.", null);
            if (string.IsNullOrWhiteSpace(account) || account.Trim().Length < 6) return ("Please enter the wallet or card number you are paying from.", null);
            if (b.Booking.Payment?.Status == PaymentStatus.Succeeded) return ("This booking has already been paid.", null);

            // An abandoned checkout is closed out so the retry has a clean, single live attempt.
            if (b.Booking.Payment?.Status == PaymentStatus.Pending)
            {
                b.Booking.Payment.Status = PaymentStatus.Failed;
                b.Booking.Payment.FailureReason = "Superseded by a new payment attempt";
            }

            var payment = new Payment
            {
                BookingType = b.Type,
                BookingId = bookingId,
                FarmerId = farmerId,
                Amount = b.Booking.AgreedGross.Value,
                Method = method,
                PayerAccount = account.Trim(),
                Reference = $"KL-PM-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                Status = PaymentStatus.Pending
            };
            var initiation = await _gateway.InitiateAsync(payment, "/Bookings/PaymentCallback");
            payment.GatewayReference = initiation.GatewayReference;

            await _payments.AddAsync(payment);
            b.Booking.Payment = payment;
            await _payments.SaveChangesAsync();

            return (null, initiation.RedirectUrl);
        }

        public async Task<PaymentSummary?> GetByGatewayReferenceAsync(string gatewayReference)
        {
            var payment = await _payments.Query().FirstOrDefaultAsync(p => p.GatewayReference == gatewayReference);
            if (payment is null) return null;
            var b = await LoadAsync(payment.BookingType, payment.BookingId);
            return ToSummary(payment, b?.ItemName ?? string.Empty);
        }

        public async Task<(string? Error, PaymentSummary? Payment)> CompleteAsync(string gatewayReference, string outcome)
        {
            var payment = await _payments.QueryTracked().FirstOrDefaultAsync(p => p.GatewayReference == gatewayReference);
            if (payment is null) return ("This payment could not be found.", null);

            var b = await LoadAsync(payment.BookingType, payment.BookingId);
            var itemName = b?.ItemName ?? string.Empty;

            // Idempotent: a repeated callback for a finished payment changes nothing.
            if (payment.Status != PaymentStatus.Pending)
                return (payment.Status == PaymentStatus.Succeeded ? null : payment.FailureReason ?? "Payment failed.", ToSummary(payment, itemName));

            var result = await _gateway.VerifyAsync(payment, outcome);
            var booking = b?.Booking;
            if (result.Succeeded && (booking is null || booking.Status != BookingStatus.Accepted))
                result = new GatewayResult(false, "The booking is no longer awaiting payment.");

            if (result.Succeeded)
            {
                payment.Status = PaymentStatus.Succeeded;
                payment.PaidOn = DateTime.UtcNow;
                booking!.Status = BookingWorkflow.Next(booking.Status, "paid")!;
                booking.PaidOn = booking.UpdatedOn = DateTime.Now;
                _ledger.Add(LedgerPostings.PaymentIn(payment));
            }
            else
            {
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = result.FailureReason;
                if (booking?.PaymentId == payment.Id)
                {
                    booking.Payment = null;
                    booking.PaymentId = null;
                }
            }
            await _payments.SaveChangesAsync();

            if (result.Succeeded && b is not null)
            {
                await _notifications.NotifyAsync(new NotificationRequest
                {
                    UserId = b.OwnerId,
                    Type = NotificationTypes.PaymentReceived,
                    TitleKey = "Payment Received",
                    MessageKey = "Farmer paid ৳{0} for {1}. Mark the booking completed once the service is done.",
                    Args = new object[] { $"{payment.Amount:N0}", itemName },
                    LinkUrl = AppLinks.OwnerRequests(b.Type, payment.BookingId),
                    DedupeKey = $"payment:{payment.Id}:Succeeded",
                    SendEmail = false
                });

                // In-app notification to farmer
                await _notifications.NotifyAsync(new NotificationRequest
                {
                    UserId = payment.FarmerId,
                    Type = NotificationTypes.PaymentReceived,
                    TitleKey = "Payment confirmed",
                    MessageKey = "Your payment of ৳{0} for {1} is confirmed (ref {2}). Your receipt is available in My Bookings.",
                    Args = new object[] { $"{payment.Amount:N0}", itemName, payment.Reference },
                    LinkUrl = AppLinks.FarmerBookings(b.Type, payment.BookingId),
                    DedupeKey = $"payment:{payment.Id}:FarmerReceipt",
                    SendEmail = false
                });

                // Auto-email receipt PDF on success when farmer has a real email
                try
                {
                    var farmer = await _users.Query().FirstOrDefaultAsync(u => u.Id == payment.FarmerId);
                    if (farmer != null && ApplicationUser.HasRealEmail(farmer.Email))
                    {
                        var receipt = await _receipts.BuildAsync(payment.BookingType, payment.BookingId, _appOptions.PublicBaseUrl);
                        if (receipt != null)
                        {
                            var emailSubject = $"[KrishiLink] Payment receipt {payment.Reference}";
                            var emailHtml = $@"<div style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                                <h2 style='color: #2d6a4f;'>KrishiLink Payment Receipt</h2>
                                <p>Dear {farmer.FullName ?? "Farmer"},</p>
                                <p>Your payment of <strong>BDT {payment.Amount:N0}</strong> for <strong>{itemName}</strong> has been confirmed and placed into secure escrow.</p>
                                <p><strong>Payment Reference:</strong> <code>{payment.Reference}</code><br/>
                                <strong>Method:</strong> {payment.Method}<br/>
                                <strong>Date:</strong> {payment.PaidOn:dd MMM yyyy HH:mm} UTC</p>
                                <p>Your official payment receipt PDF is attached to this email. You can also view and download it at any time from your KrishiLink dashboard.</p>
                                <hr style='border: none; border-top: 1px solid #e2e8df; margin: 20px 0;' />
                                <p style='font-size: 12px; color: #666;'>KrishiLink Platform · Bangladesh Agricultural Asset & Storage Sharing</p>
                            </div>";

                            await _emailQueue.EnqueueAsync(new EmailJob(
                                farmer.Email!,
                                emailSubject,
                                emailHtml,
                                new EmailAttachment(receipt.Value.FileName, receipt.Value.Content, "application/pdf")
                            ));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to build or email receipt PDF for payment {PaymentId} ({Reference})", payment.Id, payment.Reference);
                }
            }

            return (result.Succeeded ? null : result.FailureReason, ToSummary(payment, itemName));
        }

        public async Task RefundAsync(Payment payment)
        {
            if (payment.Status != PaymentStatus.Succeeded) return;
            payment.Status = PaymentStatus.Refunded;
            payment.RefundedOn = DateTime.UtcNow;
            _ledger.Add(LedgerPostings.Refund(payment));
            await _gateway.RefundAsync(payment);
        }

        // ---- Helpers -------------------------------------------------------------------------

        private record Loaded(string Type, IPayableBooking Booking, string Code, string ItemName, string OwnerId, string OwnerName, string QuantityText, string RateText);

        private async Task<Loaded?> LoadAsync(string bookingType, int bookingId)
        {
            if (string.Equals(bookingType, "Equipment", StringComparison.OrdinalIgnoreCase))
            {
                var b = await _rentals.QueryTracked().Include(x => x.Payment).Include(x => x.Equipment!).ThenInclude(e => e.Owner)
                    .FirstOrDefaultAsync(x => x.Id == bookingId);
                if (b?.Equipment is null) return null;
                var days = ListingFormat.InclusiveDays(b.StartDate, b.EndDate);
                return new Loaded("Equipment", b, $"KL-EQ-{b.RequestedOn.Year}-{b.Id:D3}", b.Equipment.Name, b.Equipment.OwnerId,
                    b.Equipment.Owner?.FullName ?? "Owner", days == 1 ? "1 day" : $"{days} days", $"{ListingFormat.Taka(b.AgreedRate ?? b.Equipment.DailyRate)} / day");
            }
            if (string.Equals(bookingType, "Godown", StringComparison.OrdinalIgnoreCase))
            {
                var g = await _storage.QueryTracked().Include(x => x.Payment).Include(x => x.Godown!).ThenInclude(x => x.Owner)
                    .FirstOrDefaultAsync(x => x.Id == bookingId);
                if (g?.Godown is null) return null;
                var months = ListingFormat.Months(g.StartDate, g.EndDate);
                return new Loaded("Godown", g, $"KL-GD-{g.RequestedOn.Year}-{g.Id:D3}", g.Godown.Name, g.Godown.OwnerId,
                    g.Godown.Owner?.FullName ?? "Owner", $"{g.StorageTons:N0} t × {months:0.##} mo", $"{ListingFormat.Taka(g.AgreedRate ?? g.Godown.PricePerTonPerMonth)} / t / mo");
            }
            return null;
        }

        private static PaymentSummary ToSummary(Payment p, string itemName) =>
            new(p.Id, p.BookingType, p.BookingId, itemName, p.Amount, p.Method, p.PayerAccount, p.Reference, p.GatewayReference ?? string.Empty, p.Status, p.PaidOn, p.FailureReason);
    }
}
