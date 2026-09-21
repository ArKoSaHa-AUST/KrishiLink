using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.BLL.Services
{
    /// <summary>Farmer-side view of their own equipment rentals and godown storage bookings.</summary>
    public interface IBookingService
    {
        Task<BookingHistoryViewModel> GetHistoryAsync(string farmerId, string tab, string status, DateTime? from, DateTime? to, string? search);
        Task<FarmerDashboardViewModel> GetDashboardAsync(string farmerId);

        /// <summary>Farmer cancels their own pending, unpaid-accepted, or paid-but-not-started booking. Paid bookings are refunded in the same operation.</summary>
        Task<(string? Error, decimal? Refunded)> CancelAsync(string farmerId, string bookingType, int bookingId);

        /// <summary>Farmer modifies their own pending or unpaid-accepted booking. Accepted bookings return to pending for owner re-approval.</summary>
        Task<(string? Error, bool NeedsReapproval)> ModifyAsync(string farmerId, string bookingType, int bookingId, DateTime startDate, DateTime endDate, int? units, double? tons);

        // QR Code Booking Confirmation & Verification
        /// <summary>Returns null unless <paramref name="userId"/> is the booking's farmer or the listing's owner.</summary>
        Task<BookingConfirmationViewModel?> GetConfirmationAsync(string? userId, string bookingType, int bookingId, string requestHost, bool justCreated = false);
        Task<BookingConfirmationViewModel?> GetConfirmationByCodeAsync(string? userId, string bookingCode, string requestHost);

        /// <summary>
        /// Public verification certificate. Full detail is shown to the booking's farmer, the listing's owner and
        /// administrators; an anonymous scan needs the signed <paramref name="token"/> from the QR link and never
        /// sees phone numbers or money. Anything else is reported as an unrecognised reference.
        /// </summary>
        Task<BookingVerificationViewModel> GetVerificationByCodeAsync(string bookingCode, string? token, string? currentUserId, bool currentUserIsAdmin, string requestHost);
        Task<string?> QuickVerifyActionAsync(string ownerId, string bookingCode, string action);

        /// <summary>
        /// Generates the official payment receipt PDF if requester is the booking farmer or listing owner,
        /// and the booking has a succeeded or refunded payment.
        /// </summary>
        Task<(byte[] Content, string FileName)?> GetReceiptPdfAsync(string requesterId, string bookingType, int bookingId, string requestHost);
    }

    public class BookingService : IBookingService
    {
        private readonly IRepository<EquipmentBooking> _rentals;
        private readonly IRepository<GodownBooking> _storage;
        private readonly IRepository<HarvestPlan> _harvestPlans;
        private readonly IQrCodeService _qrCode;
        private readonly ILoyaltyService _loyalty;
        private readonly INotificationService _notifications;
        private readonly IPaymentService _payments;
        private readonly IEquipmentService _equipmentService;
        private readonly IGodownService _godownService;
        private readonly IRepository<Favorite> _favorites;
        private readonly IRepository<SavedSearch> _savedSearches;
        private readonly IReceiptDocumentService _receipts;

        public BookingService(
            IRepository<EquipmentBooking> rentals,
            IRepository<GodownBooking> storage,
            IRepository<HarvestPlan> harvestPlans,
            IQrCodeService qrCode,
            ILoyaltyService loyalty,
            INotificationService notifications,
            IPaymentService payments,
            IEquipmentService equipmentService,
            IGodownService godownService,
            IRepository<Favorite> favorites,
            IRepository<SavedSearch> savedSearches,
            IReceiptDocumentService receipts)
        {
            _rentals = rentals;
            _storage = storage;
            _harvestPlans = harvestPlans;
            _qrCode = qrCode;
            _loyalty = loyalty;
            _notifications = notifications;
            _payments = payments;
            _equipmentService = equipmentService;
            _godownService = godownService;
            _favorites = favorites;
            _savedSearches = savedSearches;
            _receipts = receipts;
        }

        public async Task<BookingHistoryViewModel> GetHistoryAsync(string farmerId, string tab, string status, DateTime? from, DateTime? to, string? search)
        {
            var all = await LoadAllAsync(farmerId);
            tab = string.IsNullOrWhiteSpace(tab) ? "all" : tab.ToLowerInvariant();
            status = string.IsNullOrWhiteSpace(status) ? "all" : status.ToLowerInvariant();

            IEnumerable<BookingHistoryItemViewModel> query = all;
            if (tab is "equipment" or "godown")
                query = query.Where(b => b.BookingType.Equals(tab, StringComparison.OrdinalIgnoreCase));
            if (status != "all")
                query = query.Where(b => b.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(b => b.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || b.BookingCode.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || b.Location.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || b.OwnerName.Contains(term, StringComparison.OrdinalIgnoreCase));
            }
            if (from.HasValue) query = query.Where(b => b.StartDate >= from.Value.Date);
            if (to.HasValue) query = query.Where(b => b.EndDate <= to.Value.Date);

            return new BookingHistoryViewModel
            {
                ActiveTab = tab,
                StatusFilter = status,
                DateFrom = from,
                DateTo = to,
                SearchTerm = search,
                Bookings = query.OrderByDescending(b => b.RequestedAt).ToList(),
                TotalAllCount = all.Count,
                EquipmentCount = all.Count(b => b.BookingType == "Equipment"),
                GodownCount = all.Count(b => b.BookingType == "Godown"),
                PendingCount = all.Count(b => b.Status == BookingStatus.Pending),
                AcceptedCount = all.Count(b => b.Status == BookingStatus.Accepted),
                PaidCount = all.Count(b => b.Status == BookingStatus.Paid),
                CompletedCount = all.Count(b => b.Status == BookingStatus.Completed),
                RejectedCount = all.Count(b => b.Status == BookingStatus.Rejected),
                CancelledCount = all.Count(b => b.Status == BookingStatus.Cancelled)
            };
        }

        public async Task<FarmerDashboardViewModel> GetDashboardAsync(string farmerId)
        {
            var all = await LoadAllAsync(farmerId);

            var activity = new List<(DateTime At, ActivityFeedItem Item)>();
            foreach (var b in all)
            {
                var noun = b.BookingType == "Equipment" ? "Equipment request" : "Godown booking request";
                activity.Add((b.RequestedAt, Feed($"{noun} sent for {b.ItemName}.", b.BookingType == "Equipment" ? "bi-tools" : "bi-building", "text-warning", b.RequestedAt)));

                if (b.PaidOn is not null)
                    activity.Add((b.PaidOn.Value, Feed($"Payment confirmed for {b.ItemName}.", "bi-credit-card-fill", "text-success", b.PaidOn.Value)));
                if (b.RefundedOn is not null)
                    activity.Add((b.RefundedOn.Value, Feed($"Refund issued for {b.ItemName}.", "bi-arrow-counterclockwise", "text-info", b.RefundedOn.Value)));

                if (b.UpdatedAt is null || b.Status == BookingStatus.Pending) continue;
                var (text, icon, color) = b.Status switch
                {
                    BookingStatus.Accepted => ($"{b.BookingType} request for {b.ItemName} was accepted by the owner.", "bi-check-circle-fill", "text-success"),
                    BookingStatus.Paid => ($"{b.BookingType} booking for {b.ItemName} is confirmed and paid.", "bi-shield-check", "text-success"),
                    BookingStatus.Rejected => ($"{b.BookingType} request for {b.ItemName} was rejected by the owner.", "bi-x-circle-fill", "text-danger"),
                    BookingStatus.Completed => ($"{b.ItemName} booking completed.", "bi-flag-fill", "text-secondary"),
                    _ => ($"{b.ItemName} booking was {b.Status.ToLowerInvariant()}.", "bi-info-circle-fill", "text-secondary")
                };
                activity.Add((b.UpdatedAt.Value, Feed(text, icon, color, b.UpdatedAt.Value)));
            }

            var draftPlansCount = await _harvestPlans.Query()
                .CountAsync(p => p.FarmerId == farmerId && p.Status == HarvestPlanStatus.Draft);

            var nextPlan = await _harvestPlans.Query()
                .Include(p => p.Items)
                .Where(p => p.FarmerId == farmerId && p.Status == HarvestPlanStatus.Submitted)
                .OrderByDescending(p => p.SubmittedOn ?? p.CreatedAt)
                .FirstOrDefaultAsync();

            HarvestPlanSummaryViewModel? nextPlanSummary = null;
            if (nextPlan != null)
            {
                var minDate = nextPlan.Items.Count > 0 ? nextPlan.Items.Min(i => i.StartDate) : (DateTime?)null;
                var maxDate = nextPlan.Items.Count > 0 ? nextPlan.Items.Max(i => i.EndDate) : (DateTime?)null;
                nextPlanSummary = new HarvestPlanSummaryViewModel
                {
                    Id = nextPlan.Id,
                    Name = nextPlan.Name,
                    Crop = nextPlan.Crop,
                    Status = nextPlan.Status,
                    ItemCount = nextPlan.Items.Count,
                    EarliestStartDate = minDate,
                    TargetDateRange = minDate.HasValue && maxDate.HasValue ? ListingFormat.DateRange(minDate.Value, maxDate.Value) : string.Empty
                };
            }

            var favoritesCount = await _favorites.Query().CountAsync(f => f.UserId == farmerId);
            var savedSearchesCount = await _savedSearches.Query().CountAsync(s => s.UserId == farmerId);

            return new FarmerDashboardViewModel
            {
                ActiveBookings = all
                    .Where(b => b.Status is BookingStatus.Pending or BookingStatus.Accepted or BookingStatus.Paid)
                    .OrderBy(b => b.StartDate)
                    .Take(6)
                    .Select(b => new BookingSummaryItem
                    {
                        Id = b.Id,
                        ItemName = b.ItemName,
                        BookingType = b.BookingType,
                        Location = b.Location,
                        DateRange = b.DateRangeDisplay,
                        Status = b.Status,
                        DetailUrl = "/Bookings"
                    }).ToList(),
                RecentActivity = activity.OrderByDescending(a => a.At).Take(6).Select(a => a.Item).ToList(),
                DraftHarvestPlansCount = draftPlansCount,
                NextSubmittedHarvestPlan = nextPlanSummary,
                FavoritesCount = favoritesCount,
                SavedSearchesCount = savedSearchesCount
            };
        }

        public async Task<(string? Error, decimal? Refunded)> CancelAsync(string farmerId, string bookingType, int bookingId)
        {
            await using var transaction = await _rentals.BeginWorkflowAsync();
            if (bookingType.Equals("Equipment", StringComparison.OrdinalIgnoreCase))
            {
                var b = await _rentals.QueryTracked()
                    .Include(x => x.Equipment)
                    .Include(x => x.Farmer)
                    .Include(x => x.Payment)
                    .FirstOrDefaultAsync(x => x.Id == bookingId && x.FarmerId == farmerId);
                var error = ValidateCancel(b);
                if (error is not null) return (error, null);
                var refunded = await CancelCoreAsync(b!);
                await _rentals.SaveChangesAsync();

                if (b!.PointsUsed > 0 || b.DiscountAmount > 0)
                {
                    await _loyalty.RefundPointsForCancelledBookingAsync(farmerId, "Equipment", b.Id, $"#EQ-{b.Id:D4}");
                }
                await transaction.CommitAsync();

                if (b.Equipment != null && !string.IsNullOrEmpty(b.Equipment.OwnerId))
                {
                    var farmerName = b.Farmer?.FullName ?? "A farmer";
                    await _notifications.NotifyAsync(new NotificationRequest
                    {
                        UserId = b.Equipment.OwnerId,
                        Type = NotificationTypes.BookingRejected,
                        TitleKey = "Rental Booking Cancelled",
                        MessageKey = "{0} cancelled the equipment booking for {1} ({2} - {3}).",
                        Args = new object[] { farmerName, b.Equipment.Name, $"{b.StartDate:dd MMM yyyy}", $"{b.EndDate:dd MMM yyyy}" },
                        LinkUrl = AppLinks.OwnerRequests("equipment", b.Id),
                        DedupeKey = $"booking:equipment:{b.Id}:Cancelled",
                        SendEmail = false
                    });
                }

                return (null, refunded);
            }

            var g = await _storage.QueryTracked()
                .Include(x => x.Godown)
                .Include(x => x.Farmer)
                .Include(x => x.Payment)
                .FirstOrDefaultAsync(x => x.Id == bookingId && x.FarmerId == farmerId);
            var err = ValidateCancel(g);
            if (err is not null) return (err, null);
            var refundedTons = await CancelCoreAsync(g!);
            await _storage.SaveChangesAsync();

            if (g!.PointsUsed > 0 || g.DiscountAmount > 0)
            {
                await _loyalty.RefundPointsForCancelledBookingAsync(farmerId, "Godown", g.Id, $"#GD-{g.Id:D4}");
            }
            await transaction.CommitAsync();

            if (g.Godown != null && !string.IsNullOrEmpty(g.Godown.OwnerId))
            {
                var farmerName = g.Farmer?.FullName ?? "A farmer";
                await _notifications.NotifyAsync(new NotificationRequest
                {
                    UserId = g.Godown.OwnerId,
                    Type = NotificationTypes.BookingRejected,
                    TitleKey = "Storage Booking Cancelled",
                    MessageKey = "{0} cancelled the storage booking for {1} tons in {2} ({3} - {4}).",
                    Args = new object[] { farmerName, g.StorageTons, g.Godown.Name, $"{g.StartDate:dd MMM yyyy}", $"{g.EndDate:dd MMM yyyy}" },
                    LinkUrl = AppLinks.OwnerRequests("godown", g.Id),
                    DedupeKey = $"booking:godown:{g.Id}:Cancelled",
                    SendEmail = false
                });
            }

            return (null, refundedTons);
        }

        /// <summary>Flips the booking to Cancelled and, when the farmer had paid, refunds in the same unit of work. Returns the refunded amount.</summary>
        private async Task<decimal?> CancelCoreAsync(IPayableBooking b)
        {
            decimal? refunded = null;
            if (b.Payment?.Status == PaymentStatus.Succeeded)
            {
                await _payments.RefundAsync(b.Payment);
                refunded = b.Payment.Amount;
            }
            b.Status = BookingStatus.Cancelled;
            b.CancelledOn = b.UpdatedOn = DateTime.UtcNow;
            return refunded;
        }

        /// <summary>
        /// Pending requests can always be withdrawn. Accepted (unpaid) and Paid bookings can be cancelled until they start;
        /// a paid booking is refunded. Once started, completed, rejected or cancelled, nothing can be undone by the farmer.
        /// </summary>
        private static string? ValidateCancel(IPayableBooking? b) => b?.Status switch
        {
            null => "This booking could not be found.",
            BookingStatus.Pending => null,
            BookingStatus.Accepted or BookingStatus.Paid when b.StartDate > DateTime.Today => null,
            BookingStatus.Accepted or BookingStatus.Paid => "This booking has already started and can no longer be cancelled. Please contact the owner.",
            _ => $"A {b.Status.ToLowerInvariant()} booking cannot be cancelled."
        };

        private static bool CanCancel(IPayableBooking b) => ValidateCancel(b) is null;

        public const int MaxModifications = 3;

        /// <summary>
        /// Farmers can modify Pending or unpaid Accepted bookings before their start date, up to MaxModifications times.
        /// Paid bookings cannot be modified as payment is locked in escrow.
        /// </summary>
        private static string? ValidateModify(IPayableBooking? b)
        {
            if (b is null) return "This booking could not be found.";
            if (b.ModificationCount >= MaxModifications)
                return $"This booking has already been modified the maximum allowed {MaxModifications} times.";
            if (b.Payment?.Status == PaymentStatus.Succeeded)
                return "This booking has been paid and its payment is locked. Please cancel and request a refund instead of modifying.";
            if (b.StartDate.Date <= DateTime.Today)
                return "This booking has already started or starts today and can no longer be modified.";
            if (b.Status is not (BookingStatus.Pending or BookingStatus.Accepted))
                return $"A {b.Status.ToLowerInvariant()} booking cannot be modified.";
            return null;
        }

        private static bool CanModify(IPayableBooking b) => ValidateModify(b) is null;

        public async Task<(string? Error, bool NeedsReapproval)> ModifyAsync(
            string farmerId,
            string bookingType,
            int bookingId,
            DateTime startDate,
            DateTime endDate,
            int? units,
            double? tons)
        {
            if (startDate.Date < DateTime.Today)
                return ("Start date cannot be in the past.", false);
            if (endDate.Date < startDate.Date)
                return ("End date cannot be before start date.", false);

            await using var transaction = await _rentals.BeginWorkflowAsync();

            if (bookingType.Equals("Equipment", StringComparison.OrdinalIgnoreCase))
            {
                var b = await _rentals.QueryTracked()
                    .Include(x => x.Equipment!).ThenInclude(e => e.Owner)
                    .Include(x => x.Farmer)
                    .Include(x => x.Payment)
                    .FirstOrDefaultAsync(x => x.Id == bookingId && x.FarmerId == farmerId);

                var error = ValidateModify(b);
                if (error is not null) return (error, false);

                var e = b!.Equipment!;
                var days = ListingFormat.InclusiveDays(startDate, endDate);
                if (days < e.MinRentalDays)
                    return ($"Minimum rental duration is {e.MinRentalDays} {(e.MinRentalDays == 1 ? "day" : "days")}.", false);

                var reqUnits = units ?? b.Units;
                if (reqUnits < 1 || reqUnits > e.Quantity)
                    return ($"Requested units must be between 1 and {e.Quantity}.", false);

                var conflict = await _equipmentService.CheckAvailabilityAsync(e.Id, startDate, endDate, reqUnits, excludeBookingId: b.Id);
                if (conflict is not null) return (conflict, false);

                var quote = await _equipmentService.QuoteGrossAsync(e.Id, startDate, endDate, reqUnits);

                var oldRange = ListingFormat.DateRange(b.StartDate, b.EndDate);
                var oldUnits = b.Units;
                var prevDetails = $"{oldRange} ({oldUnits} {(oldUnits == 1 ? "unit" : "units")})";

                var needsReapproval = b.Status == BookingStatus.Accepted;

                if (b.PointsUsed > 0 || !string.IsNullOrEmpty(b.AppliedPromoCode))
                {
                    await _loyalty.RefundPointsForCancelledBookingAsync(farmerId, "Equipment", b.Id, $"#EQ-{b.Id:D4}");
                    var (redeemSuccess, _, valResult) = await _loyalty.RedeemPointsForBookingAsync(
                        farmerId,
                        b.AppliedPromoCode,
                        b.PointsUsed > 0 ? b.PointsUsed : null,
                        quote.Gross,
                        "Equipment",
                        b.Id,
                        $"#EQ-{b.Id:D4}");

                    if (redeemSuccess && valResult != null)
                    {
                        b.DiscountAmount = valResult.DiscountAmount;
                        b.PointsUsed = valResult.PointsRequired;
                    }
                    else
                    {
                        b.DiscountAmount = 0;
                        b.PointsUsed = 0;
                        b.AppliedPromoCode = null;
                    }
                }

                b.StartDate = startDate.Date;
                b.EndDate = endDate.Date;
                b.Units = reqUnits;
                b.QuotedGross = quote.Gross;
                b.PricingNote = quote.PricingNote;

                b.PreviousDetails = prevDetails.Length > 200 ? prevDetails[..200] : prevDetails;
                b.ModificationCount++;
                b.ModifiedOn = DateTime.UtcNow;
                b.UpdatedOn = DateTime.UtcNow;

                if (needsReapproval)
                {
                    b.Status = BookingStatus.Pending;
                    b.AgreedRate = null;
                    b.AgreedGross = null;
                    b.CommissionRate = null;
                    b.RejectReason = null;
                }

                await _rentals.SaveChangesAsync();
                await transaction.CommitAsync();

                if (e.OwnerId != null)
                {
                    var farmerName = b.Farmer?.FullName ?? "A farmer";
                    await _notifications.NotifyAsync(new NotificationRequest
                    {
                        UserId = e.OwnerId,
                        Type = NotificationTypes.BookingModified,
                        TitleKey = "Booking Modified",
                        MessageKey = "{0} updated the equipment booking for {1} to {2} - {3} ({4} units).",
                        Args = new object[] { farmerName, e.Name, $"{b.StartDate:dd MMM yyyy}", $"{b.EndDate:dd MMM yyyy}", b.Units },
                        LinkUrl = AppLinks.OwnerRequests("equipment", b.Id),
                        DedupeKey = $"booking:equipment:{b.Id}:Modified:{b.ModificationCount}",
                        SendEmail = false
                    });
                }

                return (null, needsReapproval);
            }
            else
            {
                var g = await _storage.QueryTracked()
                    .Include(x => x.Godown!).ThenInclude(god => god.Owner)
                    .Include(x => x.Farmer)
                    .Include(x => x.Payment)
                    .FirstOrDefaultAsync(x => x.Id == bookingId && x.FarmerId == farmerId);

                var error = ValidateModify(g);
                if (error is not null) return (error, false);

                var godown = g!.Godown!;
                var reqTons = tons ?? g.StorageTons;
                if (reqTons <= 0 || reqTons > godown.CapacityInTons)
                    return ($"Requested capacity must be between 1 and {godown.CapacityInTons:N0} tons.", false);

                var conflict = await _godownService.CheckAvailabilityAsync(godown.Id, reqTons, startDate, endDate, excludeBookingId: g.Id);
                if (conflict is not null) return (conflict, false);

                var newGross = BookingPricing.GodownGross(startDate, endDate, reqTons, godown.PricePerTonPerMonth);

                var oldRange = ListingFormat.DateRange(g.StartDate, g.EndDate);
                var prevDetails = $"{oldRange} ({g.StorageTons:N0} tons)";

                var needsReapproval = g.Status == BookingStatus.Accepted;

                if (g.PointsUsed > 0 || !string.IsNullOrEmpty(g.AppliedPromoCode))
                {
                    await _loyalty.RefundPointsForCancelledBookingAsync(farmerId, "Godown", g.Id, $"#GD-{g.Id:D4}");
                    var (redeemSuccess, _, valResult) = await _loyalty.RedeemPointsForBookingAsync(
                        farmerId,
                        g.AppliedPromoCode,
                        g.PointsUsed > 0 ? g.PointsUsed : null,
                        newGross,
                        "Godown",
                        g.Id,
                        $"#GD-{g.Id:D4}");

                    if (redeemSuccess && valResult != null)
                    {
                        g.DiscountAmount = valResult.DiscountAmount;
                        g.PointsUsed = valResult.PointsRequired;
                    }
                    else
                    {
                        g.DiscountAmount = 0;
                        g.PointsUsed = 0;
                        g.AppliedPromoCode = null;
                    }
                }

                g.StartDate = startDate.Date;
                g.EndDate = endDate.Date;
                g.StorageTons = reqTons;

                g.PreviousDetails = prevDetails.Length > 200 ? prevDetails[..200] : prevDetails;
                g.ModificationCount++;
                g.ModifiedOn = DateTime.UtcNow;
                g.UpdatedOn = DateTime.UtcNow;

                if (needsReapproval)
                {
                    g.Status = BookingStatus.Pending;
                    g.AgreedRate = null;
                    g.AgreedGross = null;
                    g.CommissionRate = null;
                    g.RejectReason = null;
                }

                await _storage.SaveChangesAsync();
                await transaction.CommitAsync();

                if (godown.OwnerId != null)
                {
                    var farmerName = g.Farmer?.FullName ?? "A farmer";
                    await _notifications.NotifyAsync(new NotificationRequest
                    {
                        UserId = godown.OwnerId,
                        Type = NotificationTypes.BookingModified,
                        TitleKey = "Booking Modified",
                        MessageKey = "{0} updated the storage booking for {1} to {2} tons ({3} - {4}).",
                        Args = new object[] { farmerName, godown.Name, g.StorageTons, $"{g.StartDate:dd MMM yyyy}", $"{g.EndDate:dd MMM yyyy}" },
                        LinkUrl = AppLinks.OwnerRequests("godown", g.Id),
                        DedupeKey = $"booking:godown:{g.Id}:Modified:{g.ModificationCount}",
                        SendEmail = false
                    });
                }

                return (null, needsReapproval);
            }
        }

        // ---------------------------------------------------------------- QR Code & Confirmation Voucher

        /// <summary>Random, URL-safe secret embedded in a booking's QR link (16 chars).</summary>
        private static string NewVerifyToken() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(12)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        /// <summary>Issues the QR secret on first use, so bookings created before tokens existed still get one.</summary>
        private static async Task<string> EnsureVerifyTokenAsync<T>(T booking, IRepository<T> repo) where T : class, IPayableBooking
        {
            if (!string.IsNullOrEmpty(booking.VerifyToken)) return booking.VerifyToken!;
            booking.VerifyToken = NewVerifyToken();
            await repo.SaveChangesAsync();
            return booking.VerifyToken;
        }

        /// <summary>The canonical human reference for a booking, e.g. KL-EQ-2026-001.</summary>
        internal static string BuildBookingCode(string prefix, DateTime requestedOn, int id) => $"KL-{prefix}-{requestedOn.Year}-{id:D3}";

        internal static string BuildVerifyUrl(string requestHost, string code, string? token) =>
            $"{requestHost.TrimEnd('/')}/Verify/{code}" + (string.IsNullOrEmpty(token) ? string.Empty : $"?t={token}");

        public async Task<BookingConfirmationViewModel?> GetConfirmationAsync(string? userId, string bookingType, int bookingId, string requestHost, bool justCreated = false)
        {
            if (string.IsNullOrEmpty(userId)) return null;

            if (string.Equals(bookingType, "Equipment", StringComparison.OrdinalIgnoreCase))
            {
                var b = await _rentals.QueryTracked()
                    .Include(x => x.Equipment!).ThenInclude(e => e.Owner)
                    .Include(x => x.Farmer)
                    .Include(x => x.Payment)
                    .FirstOrDefaultAsync(x => x.Id == bookingId);

                if (b == null || b.Equipment == null) return null;

                // The voucher carries both parties' contact details, so only the two parties may open it.
                var isFarmer = b.FarmerId == userId;
                var isOwner = b.Equipment.OwnerId == userId;
                if (!isFarmer && !isOwner) return null;

                var token = await EnsureVerifyTokenAsync(b, _rentals);
                var vm = BuildEquipmentConfirmation(b, requestHost, token, justCreated);
                if (isOwner) vm.FarmerProfileUrl = AppLinks.FarmerProfile(vm.FarmerId);
                return vm;
            }
            else
            {
                var g = await _storage.QueryTracked()
                    .Include(x => x.Godown!).ThenInclude(god => god.Owner)
                    .Include(x => x.Farmer)
                    .Include(x => x.Payment)
                    .Include(x => x.IntakeLots)
                    .FirstOrDefaultAsync(x => x.Id == bookingId);

                if (g == null || g.Godown == null) return null;

                var isFarmer = g.FarmerId == userId;
                var isOwner = g.Godown.OwnerId == userId;
                if (!isFarmer && !isOwner) return null;

                var token = await EnsureVerifyTokenAsync(g, _storage);
                var vm = BuildGodownConfirmation(g, requestHost, token, justCreated);
                if (isOwner) vm.FarmerProfileUrl = AppLinks.FarmerProfile(vm.FarmerId);
                return vm;
            }
        }

        public async Task<BookingConfirmationViewModel?> GetConfirmationByCodeAsync(string? userId, string bookingCode, string requestHost)
        {
            var (type, id) = ParseBookingCode(bookingCode);
            if (id <= 0) return null;
            return await GetConfirmationAsync(userId, type, id, requestHost, false);
        }

        private static BookingVerificationViewModel Unrecognised(string message = "Invalid or unrecognized booking reference code.") => new()
        {
            IsFound = false,
            IsValid = false,
            VerificationStatusMessage = message,
            SecurityBadgeClass = "bg-danger"
        };

        public async Task<BookingVerificationViewModel> GetVerificationByCodeAsync(
            string bookingCode, string? token, string? currentUserId, bool currentUserIsAdmin, string requestHost)
        {
            var (type, id) = ParseBookingCode(bookingCode);
            if (id <= 0) return Unrecognised();

            if (type.Equals("Equipment", StringComparison.OrdinalIgnoreCase))
            {
                var b = await _rentals.Query()
                    .Include(x => x.Equipment!).ThenInclude(e => e.Owner)
                    .Include(x => x.Farmer)
                    .Include(x => x.Payment)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (b == null || b.Equipment == null) return Unrecognised("Booking record not found in system.");

                var code = BuildBookingCode("EQ", b.RequestedOn, b.Id);
                var access = ResolveAccess(bookingCode, code, token, b.VerifyToken, currentUserId, b.FarmerId, b.Equipment.OwnerId, currentUserIsAdmin);
                if (access == VerificationAccess.Denied) return Unrecognised();

                var trusted = access == VerificationAccess.Party;
                var isOwner = !string.IsNullOrEmpty(currentUserId) && b.Equipment.OwnerId == currentUserId;
                var verifyUrl = BuildVerifyUrl(requestHost, code, b.VerifyToken);
                var days = ListingFormat.InclusiveDays(b.StartDate, b.EndDate);

                return new BookingVerificationViewModel
                {
                    IsFound = true,
                    IsValid = b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.Rejected,
                    BookingCode = code,
                    BookingId = b.Id,
                    BookingType = "Equipment",
                    ListingId = b.EquipmentId,
                    ItemName = b.Equipment.Name,
                    Category = b.Equipment.Category,
                    ImageUrl = ListingFormat.Split(b.Equipment.ImageUrls).FirstOrDefault() ?? string.Empty,
                    Location = b.Equipment.Location,
                    StartDate = b.StartDate,
                    EndDate = b.EndDate,
                    DurationDisplay = days == 1 ? "1 Day" : $"{days} Days",
                    TotalCost = trusted ? BookingPricing.EquipmentGrossOf(b, b.Equipment.DailyRate) : 0m,
                    DiscountAmount = trusted ? b.DiscountAmount : 0m,
                    AppliedPromoCode = trusted ? b.AppliedPromoCode : null,
                    PointsUsed = trusted ? b.PointsUsed : 0,
                    RateDescription = trusted
                        ? (b.PricingNote ?? ($"{ListingFormat.Taka(b.Equipment.DailyRate)} / day × {days} {(days == 1 ? "day" : "days")}" + (b.Units > 1 ? $" × {b.Units} units" : "")))
                        : string.Empty,
                    QuantityDisplay = b.Units > 1 ? $"{b.Units} × {b.Equipment.Category}" : $"1 {b.Equipment.Category}",
                    Status = b.Status,
                    PaymentStatus = trusted ? GetPaymentStatus(b) : string.Empty,
                    RequestedAt = b.RequestedOn,
                    UpdatedAt = b.UpdatedOn,
                    FarmerId = trusted ? b.FarmerId : string.Empty,
                    FarmerName = b.Farmer?.FullName ?? "Registered Farmer",
                    FarmerPhone = trusted ? (b.Farmer?.PhoneNumber ?? "—") : string.Empty,
                    FarmerLocation = b.Farmer?.Location ?? "—",
                    OwnerId = trusted ? b.Equipment.OwnerId : string.Empty,
                    OwnerName = b.Equipment.Owner?.FullName ?? "Equipment Owner",
                    OwnerPhone = trusted ? (b.Equipment.Owner?.PhoneNumber ?? "—") : string.Empty,
                    OwnerBusiness = b.Equipment.Owner?.BusinessOrFarmName ?? string.Empty,
                    ShowContactDetails = trusted,
                    ShowFinancials = trusted,
                    IsCurrentOwner = isOwner,
                    FarmerProfileUrl = isOwner ? AppLinks.FarmerProfile(b.FarmerId) : null,
                    CanAccept = isOwner && b.Status == BookingStatus.Pending,
                    CanReject = isOwner && b.Status == BookingStatus.Pending,
                    CanConfirmPickup = isOwner && b.Status == BookingStatus.Paid,
                    CanComplete = isOwner && b.Status == BookingStatus.Paid,
                    VerificationUrl = verifyUrl,
                    QrCodeSvg = _qrCode.GenerateSvg(verifyUrl, 8),
                    QrCodeBase64 = _qrCode.GenerateBase64Png(verifyUrl, 8),
                    SecurityBadgeClass = BadgeFor(b.Status),
                    VerificationStatusMessage = b.Status switch
                    {
                        BookingStatus.Accepted => "Authentic Booking — Accepted, Awaiting Farmer Payment",
                        BookingStatus.Paid => "Authentic & Verified Booking — Paid, Confirmed & Active",
                        BookingStatus.Pending => "Pending Owner Review — Not Yet Confirmed",
                        BookingStatus.Completed => "Completed Booking — Service Finished & Handover Settled",
                        BookingStatus.Cancelled => "Booking Cancelled by Farmer",
                        BookingStatus.Rejected => "Booking Rejected by Owner",
                        _ => "Unverified Status"
                    }
                };
            }
            else
            {
                var g = await _storage.Query()
                    .Include(x => x.Godown!).ThenInclude(god => god.Owner)
                    .Include(x => x.Farmer)
                    .Include(x => x.Payment)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (g == null || g.Godown == null) return Unrecognised("Storage booking record not found.");

                var code = BuildBookingCode("GD", g.RequestedOn, g.Id);
                var access = ResolveAccess(bookingCode, code, token, g.VerifyToken, currentUserId, g.FarmerId, g.Godown.OwnerId, currentUserIsAdmin);
                if (access == VerificationAccess.Denied) return Unrecognised();

                var trusted = access == VerificationAccess.Party;
                var isOwner = !string.IsNullOrEmpty(currentUserId) && g.Godown.OwnerId == currentUserId;
                var verifyUrl = BuildVerifyUrl(requestHost, code, g.VerifyToken);
                var months = ListingFormat.Months(g.StartDate, g.EndDate);

                return new BookingVerificationViewModel
                {
                    IsFound = true,
                    IsValid = g.Status != BookingStatus.Cancelled && g.Status != BookingStatus.Rejected,
                    BookingCode = code,
                    BookingId = g.Id,
                    BookingType = "Godown",
                    ListingId = g.GodownId,
                    ItemName = g.Godown.Name,
                    Category = g.Godown.StorageType,
                    ImageUrl = ListingFormat.Split(g.Godown.ImageUrls).FirstOrDefault() ?? string.Empty,
                    Location = g.Godown.Location,
                    StartDate = g.StartDate,
                    EndDate = g.EndDate,
                    DurationDisplay = $"{months:0.#} Months ({g.StorageTons:N0} Tons)",
                    TotalCost = trusted ? (g.AgreedGross ?? BookingPricing.GodownGross(g.StartDate, g.EndDate, g.StorageTons, g.Godown.PricePerTonPerMonth)) : 0m,
                    DiscountAmount = trusted ? g.DiscountAmount : 0m,
                    AppliedPromoCode = trusted ? g.AppliedPromoCode : null,
                    PointsUsed = trusted ? g.PointsUsed : 0,
                    RateDescription = trusted ? $"{ListingFormat.Taka(g.Godown.PricePerTonPerMonth)} / ton / mo × {g.StorageTons:N0} Tons" : string.Empty,
                    QuantityDisplay = $"{g.StorageTons:N0} Tons Capacity",
                    Status = g.Status,
                    PaymentStatus = trusted ? GetPaymentStatus(g) : string.Empty,
                    RequestedAt = g.RequestedOn,
                    UpdatedAt = g.UpdatedOn,
                    FarmerId = trusted ? g.FarmerId : string.Empty,
                    FarmerName = g.Farmer?.FullName ?? "Registered Farmer",
                    FarmerPhone = trusted ? (g.Farmer?.PhoneNumber ?? "—") : string.Empty,
                    FarmerLocation = g.Farmer?.Location ?? "—",
                    OwnerId = trusted ? g.Godown.OwnerId : string.Empty,
                    OwnerName = g.Godown.Owner?.FullName ?? "Godown Owner",
                    OwnerPhone = trusted ? (g.Godown.Owner?.PhoneNumber ?? "—") : string.Empty,
                    OwnerBusiness = g.Godown.Owner?.BusinessOrFarmName ?? string.Empty,
                    ShowContactDetails = trusted,
                    ShowFinancials = trusted,
                    IsCurrentOwner = isOwner,
                    FarmerProfileUrl = isOwner ? AppLinks.FarmerProfile(g.FarmerId) : null,
                    CanAccept = isOwner && g.Status == BookingStatus.Pending,
                    CanReject = isOwner && g.Status == BookingStatus.Pending,
                    CanConfirmPickup = isOwner && g.Status == BookingStatus.Paid,
                    CanComplete = isOwner && g.Status == BookingStatus.Paid,
                    VerificationUrl = verifyUrl,
                    QrCodeSvg = _qrCode.GenerateSvg(verifyUrl, 8),
                    QrCodeBase64 = _qrCode.GenerateBase64Png(verifyUrl, 8),
                    SecurityBadgeClass = BadgeFor(g.Status),
                    VerificationStatusMessage = g.Status switch
                    {
                        BookingStatus.Accepted => "Authentic Storage Booking — Accepted, Awaiting Farmer Payment",
                        BookingStatus.Paid => "Authentic & Verified Storage Booking — Paid, Space Allocated & Active",
                        BookingStatus.Pending => "Pending Owner Review — Not Yet Confirmed",
                        BookingStatus.Completed => "Completed Storage — Storage Period Ended & Handover Settled",
                        BookingStatus.Cancelled => "Booking Cancelled by Farmer",
                        BookingStatus.Rejected => "Booking Rejected by Owner",
                        _ => "Unverified Status"
                    }
                };
            }
        }

        private enum VerificationAccess { Denied, Scanner, Party }

        /// <summary>
        /// Decides how much of a certificate a viewer may see. Parties and administrators are trusted outright;
        /// everyone else must present the exact canonical code plus the QR secret, which keeps the page unenumerable.
        /// </summary>
        private static VerificationAccess ResolveAccess(
            string suppliedCode, string canonicalCode, string? suppliedToken, string? storedToken,
            string? currentUserId, string farmerId, string ownerId, bool isAdmin)
        {
            if (isAdmin) return VerificationAccess.Party;
            if (!string.IsNullOrEmpty(currentUserId) && (currentUserId == farmerId || currentUserId == ownerId))
                return VerificationAccess.Party;

            if (!string.Equals(suppliedCode?.Trim(), canonicalCode, StringComparison.OrdinalIgnoreCase))
                return VerificationAccess.Denied;
            if (string.IsNullOrEmpty(storedToken) || string.IsNullOrEmpty(suppliedToken))
                return VerificationAccess.Denied;

            return CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(storedToken),
                    Encoding.UTF8.GetBytes(suppliedToken))
                ? VerificationAccess.Scanner
                : VerificationAccess.Denied;
        }

        private static string BadgeFor(string status) => status switch
        {
            BookingStatus.Accepted or BookingStatus.Paid => "bg-success",
            BookingStatus.Completed => "bg-primary",
            BookingStatus.Pending => "bg-warning text-dark",
            _ => "bg-danger"
        };

        public async Task<string?> QuickVerifyActionAsync(string ownerId, string bookingCode, string action)
        {
            var (type, id) = ParseBookingCode(bookingCode);
            if (id <= 0) return "Invalid booking reference code.";

            var decision = action.ToLowerInvariant().Trim() switch
            {
                "accept" => "accept",
                "reject" => "reject",
                "confirm-pickup" or "complete" => "complete",
                _ => null
            };
            if (decision is null) return "Unknown action.";

            var result = type.Equals("Equipment", StringComparison.OrdinalIgnoreCase)
                ? await _equipmentService.RespondAsync(ownerId, id, decision, null)
                : await _godownService.RespondAsync(ownerId, id, decision, null);
            return result.Success ? null : result.Error;
        }

        // ---------------------------------------------------------------- Mapping & Helpers

        private static (string Type, int Id) ParseBookingCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return ("Equipment", 0);
            var clean = code.Trim().ToUpperInvariant();

            // Match KL-EQ-2026-001 or EQ-001 or KL-GD-2026-002
            if (clean.Contains("GD"))
            {
                var match = Regex.Match(clean, @"\d+$");
                return ("Godown", match.Success && int.TryParse(match.Value, out var id) ? id : 0);
            }
            else
            {
                var match = Regex.Match(clean, @"\d+$");
                return ("Equipment", match.Success && int.TryParse(match.Value, out var id) ? id : 0);
            }
        }

        private BookingConfirmationViewModel BuildEquipmentConfirmation(EquipmentBooking b, string requestHost, string? verifyToken, bool justCreated)
        {
            var e = b.Equipment!;
            var days = ListingFormat.InclusiveDays(b.StartDate, b.EndDate);
            var code = BuildBookingCode("EQ", b.RequestedOn, b.Id);
            var verUrl = BuildVerifyUrl(requestHost, code, verifyToken);
            var grossCost = BookingPricing.EquipmentGrossOf(b, e.DailyRate);
            var netCost = Math.Max(0m, grossCost - b.DiscountAmount);

            var vm = new BookingConfirmationViewModel
            {
                BookingId = b.Id,
                BookingCode = code,
                BookingType = "Equipment",
                ListingId = e.Id,
                ItemName = e.Name,
                Category = e.Category,
                ImageUrl = ListingFormat.Split(e.ImageUrls).FirstOrDefault() ?? string.Empty,
                Location = e.Location,
                Latitude = e.Latitude,
                Longitude = e.Longitude,
                StartDate = b.StartDate,
                EndDate = b.EndDate,
                TotalCost = grossCost,
                DiscountAmount = b.DiscountAmount,
                AppliedPromoCode = b.AppliedPromoCode,
                PointsUsed = b.PointsUsed,
                PointsEarned = _loyalty.CalculatePointsEarned(netCost),
                RateDescription = b.PricingNote ?? ($"{ListingFormat.Taka(e.DailyRate)} / day × {days} {(days == 1 ? "day" : "days")}" + (b.Units > 1 ? $" × {b.Units} units" : "")),
                QuantityDisplay = b.Units > 1 ? $"{b.Units} × {e.Category}" : $"1 {e.Category}",
                PaymentStatus = GetPaymentStatus(b),
                Status = b.Status,
                RequestedAt = b.RequestedOn,
                UpdatedAt = b.UpdatedOn,
                FarmerNotes = b.Note,
                OwnerRemarks = b.RejectReason,
                FarmerId = b.FarmerId,
                FarmerName = b.Farmer?.FullName ?? "Registered Farmer",
                FarmerPhone = b.Farmer?.PhoneNumber ?? "—",
                FarmerEmail = b.Farmer?.Email ?? "—",
                FarmerAddress = b.Farmer?.Location ?? "—",
                OwnerId = e.OwnerId,
                OwnerName = e.Owner?.FullName ?? "Equipment Owner",
                OwnerPhone = e.Owner?.PhoneNumber ?? "—",
                OwnerBusiness = e.Owner?.BusinessOrFarmName ?? string.Empty,
                OwnerLocation = e.Owner?.Location ?? e.Location,
                OwnerIsVerified = e.Owner?.IsVerified ?? false,
                VerificationUrl = verUrl,
                QrCodeSvg = _qrCode.GenerateSvg(verUrl, 8),
                QrCodeBase64 = _qrCode.GenerateBase64Png(verUrl, 8),
                JustCreated = justCreated,
                CanCancel = CanCancel(b),
                CanModify = CanModify(b),
                ModificationCount = b.ModificationCount,
                PreviousDetails = b.PreviousDetails,
                Timeline = GenerateTimeline(b.Status, b.RequestedOn, b.UpdatedOn, b.PaidOn, e.Owner?.FullName ?? "Owner", b.StartDate, b.EndDate, "Rental Requested", "Active in Field", "Equipment in use", "Completed & Handover")
            };

            return vm;
        }

        private BookingConfirmationViewModel BuildGodownConfirmation(GodownBooking b, string requestHost, string? verifyToken, bool justCreated)
        {
            var g = b.Godown!;
            var months = ListingFormat.Months(b.StartDate, b.EndDate);
            var code = BuildBookingCode("GD", b.RequestedOn, b.Id);
            var verUrl = BuildVerifyUrl(requestHost, code, verifyToken);
            var grossCost = b.AgreedGross ?? BookingPricing.GodownGross(b.StartDate, b.EndDate, b.StorageTons, g.PricePerTonPerMonth);
            var netCost = Math.Max(0m, grossCost - b.DiscountAmount);

            var vm = new BookingConfirmationViewModel
            {
                BookingId = b.Id,
                BookingCode = code,
                BookingType = "Godown",
                ListingId = g.Id,
                ItemName = g.Name,
                Category = g.StorageType,
                ImageUrl = ListingFormat.Split(g.ImageUrls).FirstOrDefault() ?? string.Empty,
                Location = g.Location,
                Latitude = g.Latitude,
                Longitude = g.Longitude,
                StartDate = b.StartDate,
                EndDate = b.EndDate,
                TotalCost = grossCost,
                DiscountAmount = b.DiscountAmount,
                AppliedPromoCode = b.AppliedPromoCode,
                PointsUsed = b.PointsUsed,
                PointsEarned = _loyalty.CalculatePointsEarned(netCost),
                RateDescription = $"{ListingFormat.Taka(g.PricePerTonPerMonth)} / ton / mo × {b.StorageTons:N0} Tons × {months:0.#} Months",
                QuantityDisplay = $"{b.StorageTons:N0} Tons Capacity",
                PaymentStatus = GetPaymentStatus(b),
                Status = b.Status,
                RequestedAt = b.RequestedOn,
                UpdatedAt = b.UpdatedOn,
                FarmerNotes = b.Note,
                OwnerRemarks = b.RejectReason,
                FarmerId = b.FarmerId,
                FarmerName = b.Farmer?.FullName ?? "Registered Farmer",
                FarmerPhone = b.Farmer?.PhoneNumber ?? "—",
                FarmerEmail = b.Farmer?.Email ?? "—",
                FarmerAddress = b.Farmer?.Location ?? "—",
                OwnerId = g.OwnerId,
                OwnerName = g.Owner?.FullName ?? "Godown Owner",
                OwnerPhone = g.Owner?.PhoneNumber ?? "—",
                OwnerBusiness = g.Owner?.BusinessOrFarmName ?? string.Empty,
                OwnerLocation = g.Owner?.Location ?? g.Location,
                OwnerIsVerified = g.Owner?.IsVerified ?? false,
                VerificationUrl = verUrl,
                QrCodeSvg = _qrCode.GenerateSvg(verUrl, 8),
                QrCodeBase64 = _qrCode.GenerateBase64Png(verUrl, 8),
                JustCreated = justCreated,
                CanCancel = CanCancel(b),
                CanModify = CanModify(b),
                ModificationCount = b.ModificationCount,
                PreviousDetails = b.PreviousDetails,
                Timeline = GenerateTimeline(b.Status, b.RequestedOn, b.UpdatedOn, b.PaidOn, g.Owner?.FullName ?? "Owner", b.StartDate, b.EndDate, "Storage Requested", "Produce Stored", "Goods in storage", "Storage Period Ended"),
                IntakeLots = b.IntakeLots.OrderByDescending(l => l.IntakeDate).Select(ToLotViewModel).ToList()
            };

            return vm;
        }

        /// <summary>Farmer-facing payment label derived from the actual Payment row, never from the booking status alone.</summary>
        private static string GetPaymentStatus(IPayableBooking b) => (b.Status, b.Payment?.Status) switch
        {
            (BookingStatus.Completed, _) => "Paid & settled",
            (_, PaymentStatus.Succeeded) => $"Paid ৳{b.Payment!.Amount:N0} via {b.Payment.Method} on {b.Payment.PaidOn:dd MMM yyyy}",
            (_, PaymentStatus.Refunded) => "Refunded",
            (BookingStatus.Accepted, PaymentStatus.Pending) => "Payment in progress",
            (BookingStatus.Accepted, _) => "Payment required",
            (BookingStatus.Pending, _) => "Awaiting owner confirmation",
            _ => "Not applicable"
        };

        private static List<BookingTimelineStep> GenerateTimeline(string status, DateTime requestedOn, DateTime? updatedOn, DateTime? paidOn, string ownerName, DateTime start, DateTime end, string requestedTitle, string activeTitle, string activeDesc, string completedTitle)
        {
            var range = ListingFormat.DateRange(start, end);
            var decided = updatedOn?.ToString("dd MMM yyyy, hh:mm tt");
            var timeline = new List<BookingTimelineStep>
            {
                new() { Title = requestedTitle, Description = "Request submitted", DateDisplay = requestedOn.ToString("dd MMM yyyy, hh:mm tt"), IsCompleted = true, State = "done" }
            };

            switch (status)
            {
                case BookingStatus.Pending:
                    timeline.Add(new() { Title = "Owner Review", Description = $"{ownerName} is reviewing your request", DateDisplay = "In Progress", IsCurrent = true, State = "active" });
                    timeline.Add(new() { Title = "Payment", Description = "Pay into KrishiLink escrow once accepted", State = "pending" });
                    timeline.Add(new() { Title = activeTitle, Description = activeDesc, DateDisplay = range, State = "pending" });
                    timeline.Add(new() { Title = completedTitle, Description = "Final handover and payment", DateDisplay = $"Expected {end:dd MMM yyyy}", State = "pending" });
                    break;
                case BookingStatus.Rejected:
                case BookingStatus.Cancelled:
                    timeline.Add(new() { Title = status, Description = status == BookingStatus.Cancelled ? "Cancelled by you" : "Request rejected", DateDisplay = decided, IsCompleted = true, IsCurrent = true, State = "rejected" });
                    break;
                default:
                    var paid = status is BookingStatus.Paid or BookingStatus.Completed;
                    var inField = paid && start <= DateTime.Today;
                    var completed = status == BookingStatus.Completed;
                    timeline.Add(new() { Title = "Owner Accepted", Description = $"Accepted by {ownerName}", DateDisplay = paid ? null : decided, IsCompleted = true, State = "done" });
                    timeline.Add(new() { Title = "Payment", Description = paid ? "Held in KrishiLink escrow until completion" : "Pay now to confirm your booking", DateDisplay = paidOn?.ToString("dd MMM yyyy, hh:mm tt") ?? "Payment required", IsCompleted = paid, IsCurrent = !paid, State = paid ? "done" : "active" });
                    timeline.Add(new() { Title = activeTitle, Description = activeDesc, DateDisplay = range, IsCompleted = completed, IsCurrent = inField && !completed, State = completed ? "done" : inField ? "active" : "pending" });
                    timeline.Add(new() { Title = completedTitle, Description = "Final handover; owner is paid from escrow", DateDisplay = completed ? decided : $"Expected {end:dd MMM yyyy}", IsCompleted = completed, IsCurrent = completed, State = completed ? "done" : "pending" });
                    break;
            }

            return timeline;
        }

        private async Task<List<BookingHistoryItemViewModel>> LoadAllAsync(string farmerId)
        {
            var rentals = await _rentals.Query()
                .Include(b => b.Equipment!).ThenInclude(e => e.Owner)
                .Include(b => b.Review)
                .Include(b => b.Payment)
                .Include(b => b.HarvestPlan)
                .Where(b => b.FarmerId == farmerId)
                .ToListAsync();
            var storage = await _storage.Query()
                .Include(b => b.Godown!).ThenInclude(g => g.Owner)
                .Include(b => b.Review)
                .Include(b => b.Payment)
                .Include(b => b.HarvestPlan)
                .Include(b => b.IntakeLots)
                .Where(b => b.FarmerId == farmerId)
                .ToListAsync();

            return rentals.Select(ToItem).Concat(storage.Select(ToItem)).ToList();
        }

        private static BookingHistoryItemViewModel ToItem(EquipmentBooking b)
        {
            var e = b.Equipment!;
            var days = ListingFormat.InclusiveDays(b.StartDate, b.EndDate);
            var item = new BookingHistoryItemViewModel
            {
                Id = b.Id,
                BookingCode = $"KL-EQ-{b.RequestedOn.Year}-{b.Id:D3}",
                ItemName = e.Name,
                BookingType = "Equipment",
                Category = e.Category,
                ImageUrl = ListingFormat.Split(e.ImageUrls).FirstOrDefault() ?? string.Empty,
                Location = e.Location,
                StartDate = b.StartDate,
                EndDate = b.EndDate,
                TotalCost = BookingPricing.EquipmentGrossOf(b, e.DailyRate),
                RateDescription = b.PricingNote ?? ($"{ListingFormat.Taka(b.AgreedRate ?? e.DailyRate)} / day × {days} {(days == 1 ? "day" : "days")}" + (b.Units > 1 ? $" × {b.Units} units" : "")),
                QuantityDisplay = b.Units > 1 ? $"{b.Units} × {e.Category}" : $"1 {e.Category}",
                ListingDetailUrl = $"/Equipment/Details/{e.Id}",
                ListingId = e.Id,
                HasReview = b.Review != null,
                ReviewRating = b.Review?.Rating,
                ReviewComment = b.Review?.Comment,
                ReviewedAt = b.Review?.CreatedAt,
                Units = b.Units,
                MaxUnits = e.Quantity,
                MinDays = e.MinRentalDays,
                HarvestPlanId = b.HarvestPlanId,
                HarvestPlanName = b.HarvestPlan?.Name
            };
            return Finish(item, b, b.Note, b.RejectReason, b.RequestedOn, b.UpdatedOn, e.Owner, "Rental Requested", "Active in Field", "Equipment in use", "Completed & Handover");
        }

        private static BookingHistoryItemViewModel ToItem(GodownBooking b)
        {
            var g = b.Godown!;
            var months = ListingFormat.Months(b.StartDate, b.EndDate);
            var item = new BookingHistoryItemViewModel
            {
                Id = b.Id,
                BookingCode = $"KL-GD-{b.RequestedOn.Year}-{b.Id:D3}",
                ItemName = g.Name,
                BookingType = "Godown",
                Category = g.StorageType,
                ImageUrl = ListingFormat.Split(g.ImageUrls).FirstOrDefault() ?? string.Empty,
                Location = g.Location,
                StartDate = b.StartDate,
                EndDate = b.EndDate,
                TotalCost = b.AgreedGross ?? BookingPricing.GodownGross(b.StartDate, b.EndDate, b.StorageTons, g.PricePerTonPerMonth),
                RateDescription = $"{ListingFormat.Taka(b.AgreedRate ?? g.PricePerTonPerMonth)} / ton / mo × {b.StorageTons:N0} Tons × {months:0.#} Months",
                QuantityDisplay = $"{b.StorageTons:N0} Tons Capacity",
                ListingDetailUrl = $"/Godown/Details/{g.Id}",
                ListingId = g.Id,
                HasReview = b.Review != null,
                ReviewRating = b.Review?.Rating,
                ReviewComment = b.Review?.Comment,
                ReviewedAt = b.Review?.CreatedAt,
                StorageTons = b.StorageTons,
                MinDays = 1,
                HarvestPlanId = b.HarvestPlanId,
                HarvestPlanName = b.HarvestPlan?.Name,
                IntakeLots = b.IntakeLots.OrderByDescending(l => l.IntakeDate).Select(ToLotViewModel).ToList()
            };
            return Finish(item, b, b.Note, b.RejectReason, b.RequestedOn, b.UpdatedOn, g.Owner, "Booking Requested", "Produce Stored", "Goods in storage", "Storage Period Ended");
        }

        private static BookingHistoryItemViewModel Finish(BookingHistoryItemViewModel item, IPayableBooking b, string? note, string? rejectReason,
            DateTime requestedOn, DateTime? updatedOn, ApplicationUser? owner, string requestedTitle, string activeTitle, string activeDesc, string completedTitle)
        {
            var status = b.Status;
            item.Status = status;
            item.RequestedAt = requestedOn;
            item.UpdatedAt = updatedOn;
            item.OwnerName = owner?.FullName ?? string.Empty;
            item.OwnerPhone = owner?.PhoneNumber ?? string.Empty;
            item.FarmerNotes = note ?? string.Empty;
            item.OwnerRemarks = rejectReason;
            item.CanCancel = CanCancel(b);
            item.CanModify = CanModify(b);
            item.ModificationCount = b.ModificationCount;
            item.PreviousDetails = b.PreviousDetails;
            item.CanPay = status == BookingStatus.Accepted && b.AgreedGross > 0;
            item.PayUrl = $"/Bookings/Pay?type={item.BookingType}&id={b.Id}";
            item.PaymentStatus = GetPaymentStatus(b);
            item.PaymentReference = b.Payment?.Status is PaymentStatus.Succeeded or PaymentStatus.Refunded ? b.Payment.Reference : null;
            item.PaymentMethod = b.Payment?.Method;
            item.PaidOn = b.PaidOn ?? b.Payment?.PaidOn;
            item.RefundedOn = b.Payment?.RefundedOn;
            item.Timeline = GenerateTimeline(status, requestedOn, updatedOn, b.PaidOn, item.OwnerName, item.StartDate, item.EndDate, requestedTitle, activeTitle, activeDesc, completedTitle);

            return item;
        }

        private static ActivityFeedItem Feed(string text, string icon, string color, DateTime at) =>
            new() { Description = text, IconClass = icon, IconColor = color, TimeAgo = TimeAgoFormatter.Format(at) };

        private static StorageIntakeLotItemViewModel ToLotViewModel(StorageIntakeLot l) => new()
        {
            Id = l.Id,
            GodownBookingId = l.GodownBookingId,
            ReceiptNumber = l.ReceiptNumber,
            IntakeDate = l.IntakeDate,
            Crop = l.Crop,
            Variety = l.Variety,
            Bags = l.Bags,
            BagWeightKg = l.BagWeightKg,
            NetWeightKg = l.NetWeightKg,
            MoisturePercent = l.MoisturePercent,
            Grade = l.Grade,
            Remarks = l.Remarks,
            Status = l.Status,
            ReleasedOn = l.ReleasedOn,
            ReleasedTo = l.ReleasedTo,
            ReleaseRemarks = l.ReleaseRemarks,
            RecordedAt = l.RecordedAt,
            UpdatedAt = l.UpdatedAt,
            ReceiptPdfUrl = AppLinks.WarehouseReceipt(l.Id)
        };

        public async Task<(byte[] Content, string FileName)?> GetReceiptPdfAsync(string requesterId, string bookingType, int bookingId, string requestHost)
        {
            if (string.Equals(bookingType, "Equipment", StringComparison.OrdinalIgnoreCase))
            {
                var b = await _rentals.Query().Include(x => x.Equipment).Include(x => x.Payment).FirstOrDefaultAsync(x => x.Id == bookingId);
                if (b is null || b.Payment is null) return null;

                var isFarmer = b.FarmerId == requesterId;
                var isOwner = b.Equipment != null && b.Equipment.OwnerId == requesterId;
                if (!isFarmer && !isOwner) return null;

                var isEligible = b.Payment.Status == PaymentStatus.Succeeded ||
                                 b.Payment.Status == PaymentStatus.Refunded ||
                                 b.Status == BookingStatus.Completed ||
                                 b.Status == BookingStatus.Paid;
                if (!isEligible) return null;

                return await _receipts.BuildAsync(bookingType, bookingId, requestHost);
            }

            if (string.Equals(bookingType, "Godown", StringComparison.OrdinalIgnoreCase))
            {
                var g = await _storage.Query().Include(x => x.Godown).Include(x => x.Payment).FirstOrDefaultAsync(x => x.Id == bookingId);
                if (g is null || g.Payment is null) return null;

                var isFarmer = g.FarmerId == requesterId;
                var isOwner = g.Godown != null && g.Godown.OwnerId == requesterId;
                if (!isFarmer && !isOwner) return null;

                var isEligible = g.Payment.Status == PaymentStatus.Succeeded ||
                                 g.Payment.Status == PaymentStatus.Refunded ||
                                 g.Status == BookingStatus.Completed ||
                                 g.Status == BookingStatus.Paid;
                if (!isEligible) return null;

                return await _receipts.BuildAsync(bookingType, bookingId, requestHost);
            }

            return null;
        }
    }
}
