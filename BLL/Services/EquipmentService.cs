using KrishiLink.BLL.Helpers;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KrishiLink.BLL.Services
{
    public interface IEquipmentService
    {
        // Farmer / public
        Task<EquipmentBrowseViewModel> BrowseAsync(EquipmentSearchCriteria criteria);
        Task<EquipmentDetailViewModel?> GetDetailsAsync(int id, string? currentUserId = null);

        /// <summary>Creates a pending rental request with optional loyalty promo or points. Returns a user-facing error message, or null on success.</summary>
        Task<string?> RequestRentalAsync(string farmerId, int equipmentId, DateTime? start, DateTime? end, string? note, string? promoCode = null, int? pointsToRedeem = null);
        Task<(string? Error, int? BookingId)> RequestRentalWithResultAsync(string farmerId, int equipmentId, DateTime? start, DateTime? end, string? note, string? promoCode = null, int? pointsToRedeem = null);

        // Owner
        Task<EquipmentOwnerDashboardViewModel> GetOwnerDashboardAsync(string ownerId);
        Task<RentalRequestsViewModel> GetOwnerRequestsAsync(string ownerId);
        Task<int> CountPendingAsync(string ownerId);

        /// <summary>
        /// Applies an owner decision. Accepting is refused when the dates clash with an already accepted rental or a
        /// blocked date; on success every other pending request that overlaps the accepted one is auto-rejected.
        /// </summary>
        Task<DecisionResult> RespondAsync(string ownerId, int bookingId, string decision, string? reason);
        Task<EquipmentListingViewModel?> GetListingAsync(string ownerId, int id);
        Task<bool> SaveListingAsync(string ownerId, EquipmentListingViewModel model);
        Task<ManageAvailabilityViewModel?> GetAvailabilityAsync(string ownerId, int equipmentId, DateTime? month);
        Task<bool> SaveAvailabilityAsync(string ownerId, int equipmentId, DateTime month, IEnumerable<DateTime> blockedDates);

        // Maintenance & Health Tracker
        Task<EquipmentMaintenanceDashboardViewModel?> GetMaintenanceDashboardAsync(string ownerId, int equipmentId);
        Task<(bool Success, string? Error)> AddMaintenanceRecordAsync(string ownerId, int equipmentId, EquipmentMaintenanceRecordInputModel model);
        Task<bool> DeleteMaintenanceRecordAsync(string ownerId, int recordId);
    }

    public class EquipmentService : IEquipmentService
    {
        private const string UploadFolder = "equipment";
        private readonly IRepository<Equipment> _equipment;
        private readonly IRepository<EquipmentBooking> _bookings;
        private readonly IRepository<EquipmentBlockedDate> _blockedDates;
        private readonly IRepository<ApplicationUser> _users;
        private readonly IRepository<EquipmentMaintenanceRecord> _maintenanceRecords;
        private readonly IFileStorageService _files;
        private readonly IReviewService _reviews;
        private readonly INotificationService _notifications;
        private readonly IBadgeService _badges;
        private readonly ILeaderboardService _leaderboard;
        private readonly ILoyaltyService _loyalty;
        private readonly ILedgerRepository _ledger;
        private readonly RevenueOptions _revenue;

        public EquipmentService(
            IRepository<Equipment> equipment,
            IRepository<EquipmentBooking> bookings,
            IRepository<EquipmentBlockedDate> blockedDates,
            IRepository<ApplicationUser> users,
            IRepository<EquipmentMaintenanceRecord> maintenanceRecords,
            IFileStorageService files,
            IReviewService reviews,
            INotificationService notifications,
            IBadgeService badges,
            ILeaderboardService leaderboard,
            ILoyaltyService loyalty,
            ILedgerRepository ledger,
            IOptions<RevenueOptions> revenue)
        {
            _equipment = equipment;
            _bookings = bookings;
            _blockedDates = blockedDates;
            _users = users;
            _maintenanceRecords = maintenanceRecords;
            _files = files;
            _reviews = reviews;
            _notifications = notifications;
            _badges = badges;
            _leaderboard = leaderboard;
            _loyalty = loyalty;
            _ledger = ledger;
            _revenue = revenue.Value;
        }

        // ---------------------------------------------------------------- Browse & details

        public async Task<EquipmentBrowseViewModel> BrowseAsync(EquipmentSearchCriteria c)
        {
            var query = _equipment.Query().Include(e => e.Owner).AsQueryable();

            if (!string.IsNullOrWhiteSpace(c.SearchTerm))
            {
                var term = c.SearchTerm.Trim();
                query = query.Where(e => e.Name.Contains(term) || e.Category.Contains(term)
                    || e.Location.Contains(term) || (e.District != null && e.District.Contains(term))
                    || e.Description.Contains(term) || e.Owner!.FullName.Contains(term));
            }
            if (c.SelectedCategories is { Count: > 0 })
                query = query.Where(e => c.SelectedCategories.Contains(e.Category));

            var rawDistrict = !string.IsNullOrWhiteSpace(c.District) ? c.District : c.Location;
            if (!string.IsNullOrWhiteSpace(rawDistrict))
            {
                var targetDistrict = OnboardingOptions.GuessDistrict(rawDistrict.Trim()) ?? rawDistrict.Trim();
                var alt = GetDistrictAlias(targetDistrict);
                if (!string.IsNullOrEmpty(alt) && !alt.Equals(targetDistrict, StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(e => (e.District != null && (e.District == targetDistrict || e.District == alt))
                        || (e.District == null && (e.Location.Contains(targetDistrict) || e.Location.Contains(alt))));
                }
                else
                {
                    query = query.Where(e => (e.District != null && e.District == targetDistrict)
                        || (e.District == null && e.Location.Contains(targetDistrict)));
                }
            }

            if (c.SelectedMinPrice.HasValue && c.SelectedMinPrice.Value > 0)
                query = query.Where(e => e.DailyRate >= c.SelectedMinPrice.Value);
            if (c.SelectedMaxPrice.HasValue && c.SelectedMaxPrice.Value > 0)
                query = query.Where(e => e.DailyRate <= c.SelectedMaxPrice.Value);

            var hasStartDate = c.StartDate.HasValue || c.AvailabilityDate.HasValue;
            if (hasStartDate)
            {
                var start = (c.StartDate ?? c.AvailabilityDate)!.Value.Date;
                var end = (c.EndDate ?? start).Date;
                if (end < start) end = start;

                query = query.Where(e => e.IsAvailable
                    && !e.BlockedDates.Any(d => d.Date >= start && d.Date <= end)
                    && !e.Bookings.Any(b => BookingStatus.Confirmed.Contains(b.Status) && b.StartDate <= end && start <= b.EndDate));
            }

            var sort = (c.SortBy ?? "newest").ToLowerInvariant();
            query = sort switch
            {
                "price_asc" => query.OrderBy(e => e.DailyRate),
                "price_desc" => query.OrderByDescending(e => e.DailyRate),
                "location" or "distance" => query.OrderBy(e => e.District).ThenBy(e => e.Location).ThenByDescending(e => e.CreatedAt),
                "rating_desc" => query.OrderByDescending(e => e.AverageRating).ThenByDescending(e => e.ReviewCount),
                _ => query.OrderByDescending(e => e.CreatedAt)
            };

            var totalCount = await query.CountAsync();
            var page = c.Page > 0 ? c.Page : 1;
            var pageSize = c.PageSize > 0 ? c.PageSize : 24;

            var rawItems = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new
                {
                    e.Id,
                    e.Name,
                    e.Category,
                    e.DailyRate,
                    e.HourlyRate,
                    e.Location,
                    e.District,
                    e.Latitude,
                    e.Longitude,
                    e.IsAvailable,
                    ImageUrl = e.ImageUrls,
                    OwnerName = e.Owner!.FullName,
                    OwnerIsVerified = e.Owner.IsVerified,
                    OwnerVerificationStatus = e.Owner.VerificationStatus,
                    OwnerRating = e.Owner.OwnerAverageRating,
                    OwnerReviewCount = e.Owner.OwnerReviewCount,
                    Rating = e.AverageRating,
                    ReviewCount = e.ReviewCount,
                    CreatedAt = e.CreatedAt,
                    LatestServiceDate = e.MaintenanceRecords.OrderByDescending(m => m.ServiceDate).Select(m => (DateTime?)m.ServiceDate).FirstOrDefault()
                }).ToListAsync();

            var items = rawItems.Select(e =>
            {
                int? daysAgo = e.LatestServiceDate.HasValue
                    ? Math.Max(0, (DateTime.Today - e.LatestServiceDate.Value.Date).Days)
                    : null;

                string? lastServicedText = daysAgo switch
                {
                    null => null,
                    0 => "Serviced today",
                    1 => "Serviced yesterday",
                    _ => $"Serviced {daysAgo} days ago"
                };

                return new EquipmentItemViewModel
                {
                    Id = e.Id,
                    Name = e.Name,
                    Category = e.Category,
                    DailyRate = e.DailyRate,
                    HourlyRate = e.HourlyRate,
                    Location = e.Location,
                    District = e.District,
                    Latitude = e.Latitude,
                    Longitude = e.Longitude,
                    IsAvailable = e.IsAvailable,
                    ImageUrl = ListingFormat.Split(e.ImageUrl).FirstOrDefault() ?? string.Empty,
                    OwnerName = e.OwnerName,
                    OwnerIsVerified = e.OwnerIsVerified,
                    OwnerVerificationStatus = e.OwnerVerificationStatus ?? "Unverified",
                    OwnerRating = e.OwnerRating,
                    OwnerReviewCount = e.OwnerReviewCount,
                    Rating = e.Rating,
                    ReviewCount = e.ReviewCount,
                    LastServicedDaysAgo = daysAgo,
                    LastServicedText = lastServicedText,
                    CreatedAt = e.CreatedAt
                };
            }).ToList();

            var model = new EquipmentBrowseViewModel
            {
                SearchTerm = c.SearchTerm,
                SelectedCategories = c.SelectedCategories ?? new List<string>(),
                District = rawDistrict,
                Location = rawDistrict,
                SelectedMinPrice = c.SelectedMinPrice,
                SelectedMaxPrice = c.SelectedMaxPrice ?? 5000,
                AvailabilityDate = c.AvailabilityDate ?? c.StartDate,
                StartDate = c.StartDate ?? c.AvailabilityDate,
                EndDate = c.EndDate,
                SortBy = sort,
                EquipmentList = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                AvailableCategories = new List<string>(OnboardingOptions.EquipmentCategories),
                AvailableLocations = new List<string>(OnboardingOptions.Districts)
            };

            return model;
        }

        private static string? GetDistrictAlias(string district)
        {
            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Bogra"] = "Bogura",
                ["Bogura"] = "Bogra",
                ["Comilla"] = "Cumilla",
                ["Cumilla"] = "Comilla",
                ["Jessore"] = "Jashore",
                ["Jashore"] = "Jessore",
                ["Chittagong"] = "Chattogram",
                ["Chattogram"] = "Chittagong",
                ["Barisal"] = "Barishal",
                ["Barishal"] = "Barisal",
                ["Nawabganj"] = "Chapainawabganj",
                ["Chapainawabganj"] = "Nawabganj",
                ["Maulvibazar"] = "Moulvibazar",
                ["Moulvibazar"] = "Maulvibazar"
            };

            return aliases.TryGetValue(district, out var alt) ? alt : null;
        }

        public async Task<EquipmentDetailViewModel?> GetDetailsAsync(int id, string? currentUserId = null)
        {
            var e = await _equipment.Query().Include(x => x.Owner).FirstOrDefaultAsync(x => x.Id == id);
            if (e is null) return null;

            var from = DateTime.Today;
            var to = from.AddMonths(3);
            var accepted = await _bookings.Query()
                .Where(b => b.EquipmentId == id && BookingStatus.Confirmed.Contains(b.Status) && b.EndDate >= from && b.StartDate <= to)
                .Select(b => new { b.StartDate, b.EndDate })
                .ToListAsync();
            var blocked = await _blockedDates.Query()
                .Where(d => d.EquipmentId == id && d.Date >= from && d.Date <= to)
                .Select(d => d.Date)
                .ToListAsync();

            var bookedDates = accepted.SelectMany(b => EachDay(b.StartDate, b.EndDate)).Concat(blocked).Distinct().OrderBy(d => d).ToList();
            var reviewsList = await _reviews.GetReviewsForEquipmentAsync(id);

            var maintenanceLogs = await _maintenanceRecords.Query()
                .Where(m => m.EquipmentId == id)
                .OrderByDescending(m => m.ServiceDate)
                .ThenByDescending(m => m.CreatedAt)
                .Select(m => new EquipmentMaintenanceItemViewModel
                {
                    Id = m.Id,
                    EquipmentId = m.EquipmentId,
                    ServiceDate = m.ServiceDate,
                    ServiceType = m.ServiceType,
                    Description = m.Description,
                    Cost = m.Cost,
                    ServicedBy = m.ServicedBy,
                    CreatedAt = m.CreatedAt
                })
                .ToListAsync();

            var latestService = maintenanceLogs.FirstOrDefault();
            int? lastServicedDaysAgo = latestService != null
                ? Math.Max(0, (DateTime.Today - latestService.ServiceDate.Date).Days)
                : null;

            string? lastServicedText = lastServicedDaysAgo switch
            {
                null => null,
                0 => "Serviced today",
                1 => "Serviced yesterday",
                _ => $"Serviced {lastServicedDaysAgo} days ago"
            };

            var lat = e.Latitude;
            var lng = e.Longitude;
            if (!lat.HasValue || !lng.HasValue)
            {
                var (fallbackLat, fallbackLng) = GeoLocationHelper.GetDistrictCoordinates(e.Location);
                lat = fallbackLat;
                lng = fallbackLng;
            }

            var model = new EquipmentDetailViewModel
            {
                Id = e.Id,
                Name = e.Name,
                Category = e.Category,
                Description = e.Description,
                DailyRate = $"{ListingFormat.Taka(e.DailyRate)} / Day",
                DailyRateAmount = e.DailyRate,
                HourlyRate = e.HourlyRate.HasValue ? $"{ListingFormat.Taka(e.HourlyRate.Value)} / Hour" : string.Empty,
                Location = e.Location,
                District = e.District ?? OnboardingOptions.GuessDistrict(e.Location),
                Latitude = lat,
                Longitude = lng,
                Status = e.IsAvailable ? "Available" : "Unavailable",
                OwnerName = e.Owner?.FullName ?? string.Empty,
                OwnerIsVerified = e.Owner?.IsVerified ?? false,
                OwnerVerificationStatus = e.Owner?.VerificationStatus ?? "Unverified",
                OwnerPhone = e.Owner?.PhoneNumber ?? string.Empty,
                OwnerMemberSince = ListingFormat.MemberSince(e.Owner?.CreatedAt ?? e.CreatedAt),
                OwnerRating = e.Owner?.OwnerAverageRating ?? 0.0,
                TotalReviews = e.Owner?.OwnerReviewCount ?? 0,
                AverageRating = e.AverageRating,
                ReviewCount = e.ReviewCount,
                ImageUrls = ListingFormat.Split(e.ImageUrls),
                BookedDates = bookedDates,
                Reviews = reviewsList,
                LastServicedDate = latestService?.ServiceDate,
                LastServicedDaysAgo = lastServicedDaysAgo,
                LastServicedText = lastServicedText,
                MaintenanceHistory = maintenanceLogs
            };

            if (!string.IsNullOrWhiteSpace(e.OwnerId))
            {
                var badges = await _badges.GetOwnerBadgesAsync(e.OwnerId);
                model.OwnerBadges = badges.Where(b => b.IsEarned).ToList();
                var (rank, trust, total) = await _leaderboard.GetOwnerRankAsync(e.OwnerId);
                if (rank.HasValue)
                {
                    model.OwnerRankText = $"Rank #{rank.Value} Top Host";
                }
            }

            // Populate Farmer Loyalty Context if user is authenticated
            if (!string.IsNullOrWhiteSpace(currentUserId))
            {
                var farmer = await _users.FirstOrDefaultAsync(u => u.Id == currentUserId);
                if (farmer != null)
                {
                    model.FarmerLoyaltyPoints = farmer.LoyaltyPoints;
                    model.FarmerTierName = _loyalty.CalculateTier(farmer.LoyaltyPoints).TierName;
                    model.AvailableConversionTiers = _loyalty.GetConversionTiers();
                }
            }

            return model;
        }

        public async Task<string?> RequestRentalAsync(string farmerId, int equipmentId, DateTime? start, DateTime? end, string? note, string? promoCode = null, int? pointsToRedeem = null)
        {
            var res = await RequestRentalWithResultAsync(farmerId, equipmentId, start, end, note, promoCode, pointsToRedeem);
            return res.Error;
        }

        public async Task<(string? Error, int? BookingId)> RequestRentalWithResultAsync(
            string farmerId,
            int equipmentId,
            DateTime? start,
            DateTime? end,
            string? note,
            string? promoCode = null,
            int? pointsToRedeem = null)
        {
            if (start is null || end is null) return ("Please choose a start and end date.", null);
            var s = start.Value.Date;
            var t = end.Value.Date;
            if (s < DateTime.Today) return ("Start date cannot be in the past.", null);
            if (t < s) return ("End date must be on or after the start date.", null);

            var e = await _equipment.Query().FirstOrDefaultAsync(x => x.Id == equipmentId);
            if (e is null) return ("This equipment listing no longer exists.", null);
            if (!e.IsAvailable) return ("This equipment is currently unavailable for rent.", null);
            if (e.OwnerId == farmerId) return ("You cannot rent your own equipment.", null);

            var clash = await FindConflictAsync(equipmentId, s, t);
            if (clash is not null) return (clash, null);

            int days = (t - s).Days + 1;
            decimal gross = days * e.DailyRate;

            decimal discountAmount = 0m;
            string? appliedPromo = null;
            int pointsUsed = 0;

            if (!string.IsNullOrWhiteSpace(promoCode) || (pointsToRedeem.HasValue && pointsToRedeem.Value > 0))
            {
                var discountCheck = await _loyalty.ValidateAndCalculateDiscountAsync(farmerId, promoCode, pointsToRedeem, gross);
                if (!discountCheck.IsValid)
                {
                    return (discountCheck.Message, null);
                }

                discountAmount = discountCheck.DiscountAmount;
                appliedPromo = discountCheck.PromoCode;
                pointsUsed = discountCheck.PointsRequired;
            }

            var newBooking = new EquipmentBooking
            {
                EquipmentId = equipmentId,
                FarmerId = farmerId,
                StartDate = s,
                EndDate = t,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                Status = BookingStatus.Pending,
                RequestedOn = DateTime.Now,
                DiscountAmount = discountAmount,
                AppliedPromoCode = appliedPromo,
                PointsUsed = pointsUsed
            };

            await _bookings.AddAsync(newBooking);
            await _bookings.SaveChangesAsync();

            // If points or promo were redeemed, deduct from farmer's balance and record ledger transaction
            if (pointsUsed > 0 || discountAmount > 0)
            {
                await _loyalty.RedeemPointsForBookingAsync(
                    farmerId,
                    appliedPromo,
                    pointsUsed,
                    gross,
                    "Equipment",
                    newBooking.Id,
                    $"#EQ-{newBooking.Id:D4}"
                );
            }

            // Notify equipment owner of new pending rental request
            var farmer = await _users.FirstOrDefaultAsync(u => u.Id == farmerId);
            var farmerName = farmer?.FullName ?? "A farmer";
            await _notifications.NotifyAsync(new NotificationRequest
            {
                UserId = e.OwnerId,
                Type = NotificationTypes.BookingRequest,
                TitleKey = "New Equipment Booking Request",
                MessageKey = "{0} requested to rent {1} from {2} to {3}.",
                Args = new object[] { farmerName, e.Name, $"{s:dd MMM yyyy}", $"{t:dd MMM yyyy}" },
                LinkUrl = AppLinks.OwnerRequests("equipment", newBooking.Id),
                DedupeKey = $"booking:equipment:{newBooking.Id}:Requested",
                SendEmail = false
            });

            return (null, newBooking.Id);
        }

        // ---------------------------------------------------------------- Owner

        public async Task<EquipmentOwnerDashboardViewModel> GetOwnerDashboardAsync(string ownerId)
        {
            var today = DateTime.Today;
            var rawListings = await _equipment.Query()
                .Where(e => e.OwnerId == ownerId)
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new
                {
                    e.Id,
                    e.Name,
                    e.Category,
                    e.DailyRate,
                    e.ImageUrls,
                    e.IsAvailable,
                    RentedToday = e.Bookings.Any(b => BookingStatus.Confirmed.Contains(b.Status) && b.StartDate <= today && today <= b.EndDate),
                    LatestServiceDate = e.MaintenanceRecords.OrderByDescending(m => m.ServiceDate).Select(m => (DateTime?)m.ServiceDate).FirstOrDefault()
                })
                .ToListAsync();

            var requests = await OwnerBookingsQuery(ownerId).Where(b => b.Status == BookingStatus.Pending).ToListAsync();

            var listings = rawListings.Select(l =>
            {
                int? daysAgo = l.LatestServiceDate.HasValue
                    ? Math.Max(0, (today - l.LatestServiceDate.Value.Date).Days)
                    : null;

                string? lastServicedText = daysAgo switch
                {
                    null => null,
                    0 => "Serviced today",
                    1 => "Serviced yesterday",
                    _ => $"Serviced {daysAgo}d ago"
                };

                return new OwnerListingItem
                {
                    Id = l.Id,
                    Name = l.Name,
                    Category = l.Category,
                    DailyRate = $"{ListingFormat.Taka(l.DailyRate)} / Day",
                    ImageUrl = ListingFormat.Split(l.ImageUrls).FirstOrDefault() ?? string.Empty,
                    Status = l.RentedToday ? "Rented" : l.IsAvailable ? "Available" : "Unavailable",
                    LastServicedDaysAgo = daysAgo,
                    LastServicedText = lastServicedText
                };
            }).ToList();

            var (rank, trustScore, totalRanked) = await _leaderboard.GetOwnerRankAsync(ownerId);
            var badgeWidget = await _badges.GetOwnerBadgeWidgetAsync(ownerId, rank, totalRanked);
            badgeWidget.TrustScore = trustScore;

            return new EquipmentOwnerDashboardViewModel
            {
                TotalListings = listings.Count,
                ActiveRentals = rawListings.Count(l => l.RentedToday),
                Listings = listings,
                PendingRequestItems = requests.Select(ToRequestItem).OrderByDescending(r => r.RequestedOn).ToList(),
                BadgeWidget = badgeWidget
            };
        }

        public async Task<RentalRequestsViewModel> GetOwnerRequestsAsync(string ownerId)
        {
            var bookings = await OwnerBookingsQuery(ownerId).ToListAsync();
            var items = bookings.Select(ToRequestItem).ToList();
            var accepted = bookings.Where(b => BookingStatus.Confirmed.Contains(b.Status)).ToList();

            foreach (var pending in items.Where(i => i.Status == BookingStatus.Pending))
            {
                var source = bookings.First(b => b.Id == pending.Id);
                var clash = accepted.FirstOrDefault(a => a.EquipmentId == source.EquipmentId
                    && BookingWorkflow.Overlaps(a.StartDate, a.EndDate, source.StartDate, source.EndDate));
                if (clash is null) continue;
                pending.HasConflict = true;
                pending.ConflictHint = $"Dates overlap with an accepted rental ({ListingFormat.DateRange(clash.StartDate, clash.EndDate)})";
            }

            return new RentalRequestsViewModel { Requests = items.OrderByDescending(r => r.RequestedOn).ToList() };
        }

        public Task<int> CountPendingAsync(string ownerId) =>
            _bookings.Query().CountAsync(b => b.Equipment!.OwnerId == ownerId && b.Status == BookingStatus.Pending);

        public async Task<DecisionResult> RespondAsync(string ownerId, int bookingId, string decision, string? reason)
        {
            var booking = await _bookings.QueryTracked().Include(b => b.Equipment).Include(b => b.Payment)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.Equipment!.OwnerId == ownerId);
            if (booking is null) return DecisionResult.Fail("This request could not be found.");

            var next = BookingWorkflow.Next(booking.Status, decision);
            var guard = BookingWorkflow.Guard(booking, decision, next);
            if (guard is not null) return DecisionResult.Fail(guard);

            if (string.Equals(decision, "undo", StringComparison.OrdinalIgnoreCase) && booking.Status == BookingStatus.Completed)
            {
                var existingReview = await _reviews.GetReviewByBookingAsync("Equipment", booking.Id);
                if (existingReview != null)
                {
                    return DecisionResult.Fail("This booking has been reviewed by the farmer and can no longer be reopened.");
                }
            }

            var autoRejected = new List<int>();
            if (next == BookingStatus.Accepted)
            {
                var clash = await FindConflictAsync(booking.EquipmentId, booking.StartDate, booking.EndDate, excludeBookingId: booking.Id);
                if (clash is not null) return DecisionResult.Fail(clash);

                // First accepted wins: every other pending request that overlaps these dates is declined with a reason.
                var losers = await _bookings.QueryTracked()
                    .Where(b => b.EquipmentId == booking.EquipmentId && b.Id != booking.Id && b.Status == BookingStatus.Pending
                        && b.StartDate <= booking.EndDate && booking.StartDate <= b.EndDate)
                    .ToListAsync();
                foreach (var loser in losers)
                {
                    loser.Status = BookingStatus.Rejected;
                    loser.RejectReason = BookingWorkflow.AutoRejectReason;
                    loser.UpdatedOn = DateTime.Now;
                    autoRejected.Add(loser.Id);

                    // Notify conflicting farmer of auto-rejection
                    var loserUser = await _users.FirstOrDefaultAsync(u => u.Id == loser.FarmerId);
                    await _notifications.NotifyAsync(new NotificationRequest
                    {
                        UserId = loser.FarmerId,
                        Type = NotificationTypes.BookingRejected,
                        TitleKey = "Rental Request Declined",
                        MessageKey = "Your rental request for {0} ({1} - {2}) was declined due to an overlapping confirmed booking.",
                        Args = new object[] { booking.Equipment!.Name, $"{loser.StartDate:dd MMM yyyy}", $"{loser.EndDate:dd MMM yyyy}" },
                        LinkUrl = AppLinks.FarmerBookings("equipment", loser.Id),
                        DedupeKey = $"booking:equipment:{loser.Id}:AutoRejected",
                        SendEmail = true,
                        RecipientEmail = loserUser?.Email
                    });
                }
            }

            var equipment = booking.Equipment!;
            BookingWorkflow.ApplyMoney(booking, next!, "Equipment", ownerId, _ledger,
                equipment.DailyRate, BookingPricing.EquipmentGross(booking.StartDate, booking.EndDate, equipment.DailyRate), _revenue.PlatformCommissionRate);
            booking.Status = next!;
            booking.RejectReason = next == BookingStatus.Rejected && !string.IsNullOrWhiteSpace(reason) ? reason.Trim() : null;
            booking.UpdatedOn = DateTime.Now;
            await _bookings.SaveChangesAsync();

            // Notify farmer of owner decision
            var farmer = await _users.FirstOrDefaultAsync(u => u.Id == booking.FarmerId);
            if (next == BookingStatus.Accepted)
            {
                await _notifications.NotifyAsync(new NotificationRequest
                {
                    UserId = booking.FarmerId,
                    Type = NotificationTypes.BookingAccepted,
                    TitleKey = "Rental Request Accepted",
                    MessageKey = "Your rental request for {0} ({1} - {2}) was accepted. Please pay ৳{3} to confirm the booking.",
                    Args = new object[] { booking.Equipment!.Name, $"{booking.StartDate:dd MMM yyyy}", $"{booking.EndDate:dd MMM yyyy}", $"{booking.AgreedGross ?? 0:N0}" },
                    LinkUrl = AppLinks.FarmerBookings("equipment", booking.Id),
                    DedupeKey = $"booking:equipment:{booking.Id}:Accepted",
                    SendEmail = true,
                    RecipientEmail = farmer?.Email
                });
            }
            else if (next == BookingStatus.Rejected)
            {
                var hasReason = !string.IsNullOrWhiteSpace(booking.RejectReason);
                await _notifications.NotifyAsync(new NotificationRequest
                {
                    UserId = booking.FarmerId,
                    Type = NotificationTypes.BookingRejected,
                    TitleKey = "Rental Request Declined",
                    MessageKey = hasReason
                        ? "Your rental request for {0} ({1} - {2}) was declined. Reason: {3}"
                        : "Your rental request for {0} ({1} - {2}) was declined.",
                    Args = hasReason
                        ? new object[] { booking.Equipment!.Name, $"{booking.StartDate:dd MMM yyyy}", $"{booking.EndDate:dd MMM yyyy}", booking.RejectReason! }
                        : new object[] { booking.Equipment!.Name, $"{booking.StartDate:dd MMM yyyy}", $"{booking.EndDate:dd MMM yyyy}" },
                    LinkUrl = AppLinks.FarmerBookings("equipment", booking.Id),
                    DedupeKey = $"booking:equipment:{booking.Id}:Rejected",
                    SendEmail = true,
                    RecipientEmail = farmer?.Email
                });

                // Refund points if promo/points were redeemed
                if (booking.PointsUsed > 0 || booking.DiscountAmount > 0)
                {
                    await _loyalty.RefundPointsForCancelledBookingAsync(booking.FarmerId, "Equipment", booking.Id, $"#EQ-{booking.Id:D4}");
                }
            }
            else if (next == BookingStatus.Completed)
            {
                await _notifications.NotifyAsync(new NotificationRequest
                {
                    UserId = booking.FarmerId,
                    Type = NotificationTypes.BookingCompleted,
                    TitleKey = "Rental Completed",
                    MessageKey = "Your rental of {0} is completed. Please take a moment to rate and review your experience!",
                    Args = new object[] { booking.Equipment!.Name },
                    LinkUrl = AppLinks.FarmerBookingsReview("equipment", booking.Id),
                    DedupeKey = $"booking:equipment:{booking.Id}:Completed",
                    SendEmail = false
                });

                // Award loyalty points on what the farmer actually paid (snapshot), net of discounts
                decimal grossSpent = (booking.AgreedGross ?? 0) - booking.DiscountAmount;
                await _loyalty.AwardPointsForCompletedBookingAsync(
                    booking.FarmerId,
                    "Equipment",
                    booking.Id,
                    Math.Max(0m, grossSpent),
                    $"#EQ-{booking.Id:D4}"
                );
            }

            return DecisionResult.Ok(autoRejected);
        }

        public async Task<EquipmentListingViewModel?> GetListingAsync(string ownerId, int id)
        {
            var e = await _equipment.Query().FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId);
            if (e is null) return null;

            return new EquipmentListingViewModel
            {
                Id = e.Id,
                Name = e.Name,
                Category = e.Category,
                Description = e.Description,
                Location = e.Location,
                District = e.District ?? OnboardingOptions.GuessDistrict(e.Location) ?? string.Empty,
                Latitude = e.Latitude,
                Longitude = e.Longitude,
                DailyRate = e.DailyRate,
                HourlyRate = e.HourlyRate,
                IsAvailable = e.IsAvailable,
                ExistingImageUrls = ListingFormat.Split(e.ImageUrls)
            };
        }

        public async Task<bool> SaveListingAsync(string ownerId, EquipmentListingViewModel model)
        {
            Equipment entity;
            List<string> previousImagesToDelete = new();

            if (model.IsEditMode)
            {
                var existing = await _equipment.QueryTracked().FirstOrDefaultAsync(x => x.Id == model.Id && x.OwnerId == ownerId);
                if (existing is null) return false;
                entity = existing;

                // Identify removed images to delete after successful save
                var previousImages = ListingFormat.Split(entity.ImageUrls);
                var retainedExisting = model.ExistingImageUrls ?? new List<string>();
                previousImagesToDelete = previousImages.Where(img => !retainedExisting.Contains(img, StringComparer.OrdinalIgnoreCase)).ToList();
            }
            else
            {
                entity = new Equipment { OwnerId = ownerId, CreatedAt = DateTime.UtcNow };
                await _equipment.AddAsync(entity);
            }

            var savedNewImages = await _files.SaveImagesAsync(model.ImageFiles, UploadFolder);
            try
            {
                var retainedUrls = model.ExistingImageUrls ?? new List<string>();
                var orderedImages = ArrangeImagesWithPrimary(retainedUrls, savedNewImages, model.PrimaryImageKey);

                entity.Name = model.Name.Trim();
                entity.Category = model.Category.Trim();
                entity.Description = model.Description.Trim();
                entity.Location = model.Location.Trim();
                entity.District = !string.IsNullOrWhiteSpace(model.District)
                    ? (OnboardingOptions.GuessDistrict(model.District.Trim()) ?? model.District.Trim())
                    : (OnboardingOptions.GuessDistrict(model.Location) ?? model.Location.Trim());

                if (model.Latitude.HasValue && model.Longitude.HasValue)
                {
                    entity.Latitude = model.Latitude.Value;
                    entity.Longitude = model.Longitude.Value;
                }
                else
                {
                    var (fallbackLat, fallbackLng) = GeoLocationHelper.GetDistrictCoordinates(model.Location);
                    entity.Latitude = fallbackLat;
                    entity.Longitude = fallbackLng;
                }

                entity.DailyRate = model.DailyRate;
                entity.HourlyRate = model.HourlyRate is > 0 ? model.HourlyRate : null;
                entity.IsAvailable = model.IsAvailable;
                entity.ImageUrls = ListingFormat.Join(orderedImages);

                await _equipment.SaveChangesAsync();

                // On successful commit, delete previously removed images from disk
                _files.DeleteFiles(previousImagesToDelete);

                model.Id = entity.Id;
                model.Latitude = entity.Latitude;
                model.Longitude = entity.Longitude;
                return true;
            }
            catch
            {
                // Rollback: clean up newly saved images so orphaned files are not left on disk
                _files.DeleteFiles(savedNewImages);
                throw;
            }
        }

        private static List<string> ArrangeImagesWithPrimary(List<string> existingUrls, List<string> newUrls, string? primaryKey)
        {
            var all = new List<string>(existingUrls);
            all.AddRange(newUrls);

            if (all.Count == 0) return all;

            string? primaryUrl = null;

            if (!string.IsNullOrWhiteSpace(primaryKey))
            {
                if (primaryKey.StartsWith("new_", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(primaryKey[4..], out var newIndex)
                    && newIndex >= 0 && newIndex < newUrls.Count)
                {
                    primaryUrl = newUrls[newIndex];
                }
                else if (all.Contains(primaryKey, StringComparer.OrdinalIgnoreCase))
                {
                    primaryUrl = all.First(x => string.Equals(x, primaryKey, StringComparison.OrdinalIgnoreCase));
                }
            }

            primaryUrl ??= all[0];

            var result = new List<string> { primaryUrl };
            foreach (var img in all)
            {
                if (!string.Equals(img, primaryUrl, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(img);
                }
            }

            return result;
        }

        public async Task<ManageAvailabilityViewModel?> GetAvailabilityAsync(string ownerId, int equipmentId, DateTime? month)
        {
            var e = await _equipment.Query().FirstOrDefaultAsync(x => x.Id == equipmentId && x.OwnerId == ownerId);
            if (e is null) return null;

            var first = new DateTime((month ?? DateTime.Today).Year, (month ?? DateTime.Today).Month, 1);
            var last = first.AddMonths(1).AddDays(-1);

            var accepted = await _bookings.Query()
                .Where(b => b.EquipmentId == equipmentId && BookingStatus.Confirmed.Contains(b.Status) && b.EndDate >= first && b.StartDate <= last)
                .Select(b => new { b.StartDate, b.EndDate })
                .ToListAsync();
            var blocked = await _blockedDates.Query()
                .Where(d => d.EquipmentId == equipmentId && d.Date >= first && d.Date <= last)
                .Select(d => d.Date)
                .ToListAsync();

            return new ManageAvailabilityViewModel
            {
                ListingId = e.Id,
                ListingName = e.Name,
                Category = e.Category,
                RateText = $"{ListingFormat.Taka(e.DailyRate)} / Day",
                Location = e.Location,
                ThumbnailUrl = ListingFormat.Split(e.ImageUrls).FirstOrDefault() ?? string.Empty,
                Month = first,
                MonthName = first.ToString("MMMM yyyy"),
                FarmerBookedDates = accepted.SelectMany(b => EachDay(b.StartDate, b.EndDate)).Where(d => d >= first && d <= last).Distinct().ToList(),
                OwnerBlockedDates = blocked
            };
        }

        public async Task<bool> SaveAvailabilityAsync(string ownerId, int equipmentId, DateTime month, IEnumerable<DateTime> blockedDates)
        {
            if (!await _equipment.Query().AnyAsync(x => x.Id == equipmentId && x.OwnerId == ownerId)) return false;

            var first = new DateTime(month.Year, month.Month, 1);
            var last = first.AddMonths(1).AddDays(-1);

            var current = await _blockedDates.QueryTracked()
                .Where(d => d.EquipmentId == equipmentId && d.Date >= first && d.Date <= last)
                .ToListAsync();
            _blockedDates.RemoveRange(current);

            var farmerBooked = (await _bookings.Query()
                    .Where(b => b.EquipmentId == equipmentId && BookingStatus.Confirmed.Contains(b.Status) && b.EndDate >= first && b.StartDate <= last)
                    .Select(b => new { b.StartDate, b.EndDate })
                    .ToListAsync())
                .SelectMany(b => EachDay(b.StartDate, b.EndDate))
                .ToHashSet();

            foreach (var date in blockedDates.Select(d => d.Date).Distinct().Where(d => d >= first && d <= last && !farmerBooked.Contains(d)))
                await _blockedDates.AddAsync(new EquipmentBlockedDate { EquipmentId = equipmentId, Date = date });

            await _blockedDates.SaveChangesAsync();
            return true;
        }

        // ---------------------------------------------------------------- Equipment Health Tracker

        public async Task<EquipmentMaintenanceDashboardViewModel?> GetMaintenanceDashboardAsync(string ownerId, int equipmentId)
        {
            var e = await _equipment.Query().FirstOrDefaultAsync(x => x.Id == equipmentId && x.OwnerId == ownerId);
            if (e is null) return null;

            var records = await _maintenanceRecords.Query()
                .Where(m => m.EquipmentId == equipmentId)
                .OrderByDescending(m => m.ServiceDate)
                .ThenByDescending(m => m.CreatedAt)
                .Select(m => new EquipmentMaintenanceItemViewModel
                {
                    Id = m.Id,
                    EquipmentId = m.EquipmentId,
                    ServiceDate = m.ServiceDate,
                    ServiceType = m.ServiceType,
                    Description = m.Description,
                    Cost = m.Cost,
                    ServicedBy = m.ServicedBy,
                    CreatedAt = m.CreatedAt
                })
                .ToListAsync();

            var totalCost = records.Sum(r => r.Cost);
            var latest = records.FirstOrDefault();
            int? daysSince = latest != null ? Math.Max(0, (DateTime.Today - latest.ServiceDate.Date).Days) : null;

            var (statusText, badgeClass) = daysSince switch
            {
                null => ("No maintenance logged yet", "bg-secondary"),
                <= 30 => ($"Serviced {daysSince} days ago (Prime Condition)", "bg-success"),
                <= 90 => ($"Serviced {daysSince} days ago (Good Condition)", "bg-info"),
                _ => ($"Serviced {daysSince} days ago (Service Recommended)", "bg-warning text-dark")
            };

            return new EquipmentMaintenanceDashboardViewModel
            {
                EquipmentId = e.Id,
                EquipmentName = e.Name,
                Category = e.Category,
                Location = e.Location,
                PrimaryImageUrl = ListingFormat.Split(e.ImageUrls).FirstOrDefault() ?? string.Empty,
                DailyRate = $"{ListingFormat.Taka(e.DailyRate)} / Day",
                IsAvailable = e.IsAvailable,
                TotalMaintenanceCost = totalCost,
                LastServicedDate = latest?.ServiceDate,
                DaysSinceLastService = daysSince,
                LastServicedStatusText = statusText,
                HealthBadgeClass = badgeClass,
                Records = records,
                NewRecord = new EquipmentMaintenanceRecordInputModel
                {
                    EquipmentId = e.Id,
                    ServiceDate = DateTime.Today
                }
            };
        }

        public async Task<(bool Success, string? Error)> AddMaintenanceRecordAsync(string ownerId, int equipmentId, EquipmentMaintenanceRecordInputModel model)
        {
            var isOwner = await _equipment.Query().AnyAsync(e => e.Id == equipmentId && e.OwnerId == ownerId);
            if (!isOwner) return (false, "Equipment listing not found or access denied.");

            if (model.ServiceDate.Date > DateTime.Today)
                return (false, "Service date cannot be in the future.");

            var record = new EquipmentMaintenanceRecord
            {
                EquipmentId = equipmentId,
                ServiceDate = model.ServiceDate.Date,
                ServiceType = model.ServiceType.Trim(),
                Description = model.Description.Trim(),
                Cost = Math.Max(0, model.Cost),
                ServicedBy = string.IsNullOrWhiteSpace(model.ServicedBy) ? null : model.ServicedBy.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            await _maintenanceRecords.AddAsync(record);
            await _maintenanceRecords.SaveChangesAsync();

            return (true, null);
        }

        public async Task<bool> DeleteMaintenanceRecordAsync(string ownerId, int recordId)
        {
            var record = await _maintenanceRecords.QueryTracked()
                .Include(m => m.Equipment)
                .FirstOrDefaultAsync(m => m.Id == recordId && m.Equipment!.OwnerId == ownerId);

            if (record is null) return false;

            _maintenanceRecords.Remove(record);
            await _maintenanceRecords.SaveChangesAsync();
            return true;
        }

        // ---------------------------------------------------------------- Helpers

        /// <summary>
        /// The single source of truth for equipment double-booking: an accepted (or ongoing) rental or an owner-blocked
        /// date inside [start, end] makes the range unavailable. Returns a user-facing message, or null when free.
        /// </summary>
        private async Task<string?> FindConflictAsync(int equipmentId, DateTime start, DateTime end, int? excludeBookingId = null)
        {
            var accepted = await _bookings.Query()
                .Where(b => b.EquipmentId == equipmentId && b.Id != excludeBookingId && BookingStatus.Confirmed.Contains(b.Status)
                    && b.StartDate <= end && start <= b.EndDate)
                .OrderBy(b => b.StartDate)
                .Select(b => new { b.StartDate, b.EndDate })
                .FirstOrDefaultAsync();
            if (accepted is not null)
                return $"These dates overlap an accepted rental ({ListingFormat.DateRange(accepted.StartDate, accepted.EndDate)}). Please choose different dates.";

            var blocked = await _blockedDates.Query()
                .Where(d => d.EquipmentId == equipmentId && d.Date >= start && d.Date <= end)
                .OrderBy(d => d.Date)
                .Select(d => (DateTime?)d.Date)
                .FirstOrDefaultAsync();
            return blocked is null ? null : $"The owner has marked {blocked:dd MMM yyyy} as unavailable. Please choose different dates.";
        }

        private IQueryable<EquipmentBooking> OwnerBookingsQuery(string ownerId) =>
            _bookings.Query()
                .Include(b => b.Equipment)
                .Include(b => b.Farmer)
                .Include(b => b.Payment)
                .Where(b => b.Equipment!.OwnerId == ownerId);

        private static RentalRequestItem ToRequestItem(EquipmentBooking b) => new()
        {
            Id = b.Id,
            FarmerName = string.IsNullOrWhiteSpace(b.Farmer?.FullName) ? "Farmer" : b.Farmer!.FullName,
            EquipmentName = b.Equipment?.Name ?? string.Empty,
            EquipmentCategory = b.Equipment?.Category ?? string.Empty,
            DailyRate = $"{ListingFormat.Taka(b.AgreedRate ?? b.Equipment?.DailyRate ?? 0)} / Day",
            Location = b.Equipment?.Location ?? string.Empty,
            DateRange = ListingFormat.DateRange(b.StartDate, b.EndDate),
            StartDate = b.StartDate,
            EndDate = b.EndDate,
            Note = b.Note,
            Status = b.Status,
            RejectReason = b.RejectReason,
            RequestedOn = b.RequestedOn,
            AgreedGross = b.AgreedGross ?? (b.Equipment is null ? 0 : BookingPricing.EquipmentGross(b.StartDate, b.EndDate, b.Equipment.DailyRate)),
            PaymentReference = b.Payment?.Status == PaymentStatus.Succeeded ? b.Payment.Reference : null
        };

        private static IEnumerable<DateTime> EachDay(DateTime start, DateTime end)
        {
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1)) yield return d;
        }
    }
}
