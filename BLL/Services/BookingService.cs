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

        // QR Code Booking Confirmation & Verification
        Task<BookingConfirmationViewModel?> GetConfirmationAsync(string? userId, string bookingType, int bookingId, string requestHost, bool justCreated = false);
        Task<BookingConfirmationViewModel?> GetConfirmationByCodeAsync(string? userId, string bookingCode, string requestHost);
        Task<BookingVerificationViewModel> GetVerificationByCodeAsync(string bookingCode, string? currentUserId, string requestHost);
        Task<string?> QuickVerifyActionAsync(string ownerId, string bookingCode, string action);
    }

    public class BookingService : IBookingService
    {
        private readonly IRepository<EquipmentBooking> _rentals;
        private readonly IRepository<GodownBooking> _storage;
        private readonly IQrCodeService _qrCode;
        private readonly ILoyaltyService _loyalty;
        private readonly INotificationService _notifications;
        private readonly IPaymentService _payments;
        private readonly IEquipmentService _equipmentService;
        private readonly IGodownService _godownService;

        public BookingService(
            IRepository<EquipmentBooking> rentals,
            IRepository<GodownBooking> storage,
            IQrCodeService qrCode,
            ILoyaltyService loyalty,
            INotificationService notifications,
            IPaymentService payments,
            IEquipmentService equipmentService,
            IGodownService godownService)
        {
            _rentals = rentals;
            _storage = storage;
            _qrCode = qrCode;
            _loyalty = loyalty;
            _notifications = notifications;
            _payments = payments;
            _equipmentService = equipmentService;
            _godownService = godownService;
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
                RecentActivity = activity.OrderByDescending(a => a.At).Take(6).Select(a => a.Item).ToList()
            };
        }

        public async Task<(string? Error, decimal? Refunded)> CancelAsync(string farmerId, string bookingType, int bookingId)
        {
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
            b.CancelledOn = b.UpdatedOn = DateTime.Now;
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

        // ---------------------------------------------------------------- QR Code & Confirmation Voucher

        public async Task<BookingConfirmationViewModel?> GetConfirmationAsync(string? userId, string bookingType, int bookingId, string requestHost, bool justCreated = false)
        {
            if (string.Equals(bookingType, "Equipment", StringComparison.OrdinalIgnoreCase))
            {
                var b = await _rentals.Query()
                    .Include(x => x.Equipment!).ThenInclude(e => e.Owner)
                    .Include(x => x.Farmer)
                    .Include(x => x.Payment)
                    .FirstOrDefaultAsync(x => x.Id == bookingId);

                if (b == null || b.Equipment == null) return null;
                return BuildEquipmentConfirmation(b, requestHost, justCreated);
            }
            else
            {
                var g = await _storage.Query()
                    .Include(x => x.Godown!).ThenInclude(god => god.Owner)
                    .Include(x => x.Farmer)
                    .Include(x => x.Payment)
                    .FirstOrDefaultAsync(x => x.Id == bookingId);

                if (g == null || g.Godown == null) return null;
                return BuildGodownConfirmation(g, requestHost, justCreated);
            }
        }

        public async Task<BookingConfirmationViewModel?> GetConfirmationByCodeAsync(string? userId, string bookingCode, string requestHost)
        {
            var (type, id) = ParseBookingCode(bookingCode);
            if (id <= 0) return null;
            return await GetConfirmationAsync(userId, type, id, requestHost, false);
        }

        public async Task<BookingVerificationViewModel> GetVerificationByCodeAsync(string bookingCode, string? currentUserId, string requestHost)
        {
            var (type, id) = ParseBookingCode(bookingCode);
            if (id <= 0)
            {
                return new BookingVerificationViewModel
                {
                    IsFound = false,
                    IsValid = false,
                    VerificationStatusMessage = "Invalid or unrecognized booking reference code.",
                    SecurityBadgeClass = "bg-danger"
                };
            }

            if (type.Equals("Equipment", StringComparison.OrdinalIgnoreCase))
            {
                var b = await _rentals.Query()
                    .Include(x => x.Equipment!).ThenInclude(e => e.Owner)
                    .Include(x => x.Farmer)
                    .Include(x => x.Payment)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (b == null || b.Equipment == null)
                {
                    return new BookingVerificationViewModel
                    {
                        IsFound = false,
                        IsValid = false,
                        VerificationStatusMessage = "Booking record not found in system.",
                        SecurityBadgeClass = "bg-danger"
                    };
                }

                var code = $"KL-EQ-{b.RequestedOn.Year}-{b.Id:D3}";
                var verUrl = $"{requestHost.TrimEnd('/')}/Verify/{code}";
                var isOwner = !string.IsNullOrEmpty(currentUserId) && b.Equipment.OwnerId == currentUserId;
                var days = ListingFormat.InclusiveDays(b.StartDate, b.EndDate);

                var vm = new BookingVerificationViewModel
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
                    TotalCost = BookingPricing.EquipmentGrossOf(b, b.Equipment.DailyRate),
                    DiscountAmount = b.DiscountAmount,
                    AppliedPromoCode = b.AppliedPromoCode,
                    PointsUsed = b.PointsUsed,
                    RateDescription = b.PricingNote ?? ($"{ListingFormat.Taka(b.Equipment.DailyRate)} / day × {days} {(days == 1 ? "day" : "days")}" + (b.Units > 1 ? $" × {b.Units} units" : "")),
                    QuantityDisplay = b.Units > 1 ? $"{b.Units} × {b.Equipment.Category}" : $"1 {b.Equipment.Category}",
                    Status = b.Status,
                    PaymentStatus = GetPaymentStatus(b),
                    RequestedAt = b.RequestedOn,
                    UpdatedAt = b.UpdatedOn,
                    FarmerId = b.FarmerId,
                    FarmerName = b.Farmer?.FullName ?? "Registered Farmer",
                    FarmerPhone = b.Farmer?.PhoneNumber ?? "—",
                    FarmerLocation = b.Farmer?.Location ?? "—",
                    OwnerId = b.Equipment.OwnerId,
                    OwnerName = b.Equipment.Owner?.FullName ?? "Equipment Owner",
                    OwnerPhone = b.Equipment.Owner?.PhoneNumber ?? "—",
                    OwnerBusiness = b.Equipment.Owner?.BusinessOrFarmName ?? string.Empty,
                    IsCurrentOwner = isOwner,
                    CanAccept = isOwner && b.Status == BookingStatus.Pending,
                    CanReject = isOwner && b.Status == BookingStatus.Pending,
                    CanConfirmPickup = isOwner && b.Status == BookingStatus.Paid,
                    CanComplete = isOwner && b.Status == BookingStatus.Paid,
                    VerificationUrl = verUrl,
                    QrCodeSvg = _qrCode.GenerateSvg(verUrl, 8),
                    QrCodeBase64 = _qrCode.GenerateBase64Png(verUrl, 8),
                    SecurityBadgeClass = b.Status switch
                    {
                        BookingStatus.Accepted or BookingStatus.Paid => "bg-success",
                        BookingStatus.Completed => "bg-primary",
                        BookingStatus.Pending => "bg-warning text-dark",
                        _ => "bg-danger"
                    },
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

                return vm;
            }
            else
            {
                var g = await _storage.Query()
                    .Include(x => x.Godown!).ThenInclude(god => god.Owner)
                    .Include(x => x.Farmer)
                    .Include(x => x.Payment)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (g == null || g.Godown == null)
                {
                    return new BookingVerificationViewModel
                    {
                        IsFound = false,
                        IsValid = false,
                        VerificationStatusMessage = "Storage booking record not found.",
                        SecurityBadgeClass = "bg-danger"
                    };
                }

                var code = $"KL-GD-{g.RequestedOn.Year}-{g.Id:D3}";
                var verUrl = $"{requestHost.TrimEnd('/')}/Verify/{code}";
                var isOwner = !string.IsNullOrEmpty(currentUserId) && g.Godown.OwnerId == currentUserId;
                var months = ListingFormat.Months(g.StartDate, g.EndDate);

                var vm = new BookingVerificationViewModel
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
                    TotalCost = g.AgreedGross ?? BookingPricing.GodownGross(g.StartDate, g.EndDate, g.StorageTons, g.Godown.PricePerTonPerMonth),
                    DiscountAmount = g.DiscountAmount,
                    AppliedPromoCode = g.AppliedPromoCode,
                    PointsUsed = g.PointsUsed,
                    RateDescription = $"{ListingFormat.Taka(g.Godown.PricePerTonPerMonth)} / ton / mo × {g.StorageTons:N0} Tons",
                    QuantityDisplay = $"{g.StorageTons:N0} Tons Capacity",
                    Status = g.Status,
                    PaymentStatus = GetPaymentStatus(g),
                    RequestedAt = g.RequestedOn,
                    UpdatedAt = g.UpdatedOn,
                    FarmerId = g.FarmerId,
                    FarmerName = g.Farmer?.FullName ?? "Registered Farmer",
                    FarmerPhone = g.Farmer?.PhoneNumber ?? "—",
                    FarmerLocation = g.Farmer?.Location ?? "—",
                    OwnerId = g.Godown.OwnerId,
                    OwnerName = g.Godown.Owner?.FullName ?? "Godown Owner",
                    OwnerPhone = g.Godown.Owner?.PhoneNumber ?? "—",
                    OwnerBusiness = g.Godown.Owner?.BusinessOrFarmName ?? string.Empty,
                    IsCurrentOwner = isOwner,
                    CanAccept = isOwner && g.Status == BookingStatus.Pending,
                    CanReject = isOwner && g.Status == BookingStatus.Pending,
                    CanConfirmPickup = isOwner && g.Status == BookingStatus.Paid,
                    CanComplete = isOwner && g.Status == BookingStatus.Paid,
                    VerificationUrl = verUrl,
                    QrCodeSvg = _qrCode.GenerateSvg(verUrl, 8),
                    QrCodeBase64 = _qrCode.GenerateBase64Png(verUrl, 8),
                    SecurityBadgeClass = g.Status switch
                    {
                        BookingStatus.Accepted or BookingStatus.Paid => "bg-success",
                        BookingStatus.Completed => "bg-primary",
                        BookingStatus.Pending => "bg-warning text-dark",
                        _ => "bg-danger"
                    },
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

                return vm;
            }
        }

        /// <summary>
        /// QR quick actions go through the same owner decision pipeline as the Requests page, so the price snapshot,
        /// payment gate, conflict checks and ledger rows apply identically however the owner reaches the decision.
        /// </summary>
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

        private BookingConfirmationViewModel BuildEquipmentConfirmation(EquipmentBooking b, string requestHost, bool justCreated)
        {
            var e = b.Equipment!;
            var days = ListingFormat.InclusiveDays(b.StartDate, b.EndDate);
            var code = $"KL-EQ-{b.RequestedOn.Year}-{b.Id:D3}";
            var verUrl = $"{requestHost.TrimEnd('/')}/Verify/{code}";
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
                Timeline = GenerateTimeline(b.Status, b.RequestedOn, b.UpdatedOn, b.PaidOn, e.Owner?.FullName ?? "Owner", b.StartDate, b.EndDate, "Rental Requested", "Active in Field", "Equipment in use", "Completed & Handover")
            };

            return vm;
        }

        private BookingConfirmationViewModel BuildGodownConfirmation(GodownBooking b, string requestHost, bool justCreated)
        {
            var g = b.Godown!;
            var months = ListingFormat.Months(b.StartDate, b.EndDate);
            var code = $"KL-GD-{b.RequestedOn.Year}-{b.Id:D3}";
            var verUrl = $"{requestHost.TrimEnd('/')}/Verify/{code}";
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
                Timeline = GenerateTimeline(b.Status, b.RequestedOn, b.UpdatedOn, b.PaidOn, g.Owner?.FullName ?? "Owner", b.StartDate, b.EndDate, "Storage Requested", "Produce Stored", "Goods in storage", "Storage Period Ended")
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
                .Where(b => b.FarmerId == farmerId)
                .ToListAsync();
            var storage = await _storage.Query()
                .Include(b => b.Godown!).ThenInclude(g => g.Owner)
                .Include(b => b.Review)
                .Include(b => b.Payment)
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
                ReviewedAt = b.Review?.CreatedAt
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
                ReviewedAt = b.Review?.CreatedAt
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
    }
}
