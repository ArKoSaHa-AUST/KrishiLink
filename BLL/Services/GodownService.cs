using KrishiLink.BLL.Helpers;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KrishiLink.BLL.Services
{
    public interface IGodownService
    {
        // Farmer / public
        Task<GodownBrowseViewModel> BrowseAsync(GodownSearchCriteria criteria);
        Task<GodownDetailViewModel?> GetDetailsAsync(int id, string? currentUserId = null);

        /// <summary>Creates a pending storage request. Returns a user-facing error message, or null on success.</summary>
        Task<string?> RequestStorageAsync(string farmerId, GodownDetailViewModel request, string? promoCode = null, int? pointsToRedeem = null);
        Task<(string? Error, int? BookingId)> RequestStorageWithResultAsync(string farmerId, GodownDetailViewModel request, string? promoCode = null, int? pointsToRedeem = null, int? harvestPlanId = null, string? planName = null);

        // Owner
        Task<GodownOwnerDashboardViewModel> GetOwnerDashboardAsync(string ownerId);
        Task<GodownBookingRequestsViewModel> GetOwnerRequestsAsync(string ownerId);
        Task<int> CountPendingAsync(string ownerId);

        /// <summary>
        /// Applies an owner decision. Accepting is refused when the request would exceed free capacity for its dates
        /// or touches a blocked date; on success overlapping pending requests that no longer fit are auto-rejected.
        /// </summary>
        Task<DecisionResult> RespondAsync(string ownerId, int bookingId, string decision, string? reason);
        Task<GodownListingViewModel?> GetListingAsync(string ownerId, int id);
        Task<bool> SaveListingAsync(string ownerId, GodownListingViewModel model);
        Task<ManageAvailabilityViewModel?> GetAvailabilityAsync(string ownerId, int godownId, DateTime? month);
        Task<bool> SaveAvailabilityAsync(string ownerId, int godownId, DateTime month, IEnumerable<DateTime> blockedDates);
        Task<BulkAvailabilityResult> BlockRangeAsync(string ownerId, int listingId, DateTime from, DateTime to, IReadOnlyCollection<DayOfWeek>? daysOfWeek, string? reason);
        Task<BulkAvailabilityResult> UnblockRangeAsync(string ownerId, int listingId, DateTime from, DateTime to, IReadOnlyCollection<DayOfWeek>? daysOfWeek);
        Task<string?> CheckAvailabilityAsync(int godownId, double tons, DateTime start, DateTime end, int? excludeBookingId = null);
    }

    public class GodownService : IGodownService
    {
        private const string UploadFolder = "godowns";
        private const int CalendarHorizonMonths = 3;
        private readonly IRepository<Godown> _godowns;
        private readonly IRepository<GodownBooking> _bookings;
        private readonly IRepository<GodownBlockedDate> _blockedDates;
        private readonly IRepository<ApplicationUser> _users;
        private readonly IFileStorageService _files;
        private readonly IReviewService _reviews;
        private readonly INotificationService _notifications;
        private readonly IBadgeService _badges;
        private readonly ILeaderboardService _leaderboard;
        private readonly ILoyaltyService _loyalty;
        private readonly IFarmerProfileService _farmerProfile;
        private readonly ILedgerRepository _ledger;
        private readonly IFavoriteService _favoriteService;
        private readonly RevenueOptions _revenue;

        public GodownService(
            IRepository<Godown> godowns,
            IRepository<GodownBooking> bookings,
            IRepository<GodownBlockedDate> blockedDates,
            IRepository<ApplicationUser> users,
            IFileStorageService files,
            IReviewService reviews,
            INotificationService notifications,
            IBadgeService badges,
            ILeaderboardService leaderboard,
            ILoyaltyService loyalty,
            IFarmerProfileService farmerProfile,
            ILedgerRepository ledger,
            IFavoriteService favoriteService,
            IOptions<RevenueOptions> revenue)
        {
            _godowns = godowns;
            _bookings = bookings;
            _blockedDates = blockedDates;
            _users = users;
            _files = files;
            _reviews = reviews;
            _notifications = notifications;
            _badges = badges;
            _leaderboard = leaderboard;
            _loyalty = loyalty;
            _farmerProfile = farmerProfile;
            _ledger = ledger;
            _favoriteService = favoriteService;
            _revenue = revenue.Value;
        }

        // ---------------------------------------------------------------- Browse & details

        public async Task<GodownBrowseViewModel> BrowseAsync(GodownSearchCriteria c)
        {
            var today = DateTime.Today;
            var query = _godowns.Query().Where(g => g.IsActive);

            if (!string.IsNullOrWhiteSpace(c.SearchTerm))
            {
                var term = c.SearchTerm.Trim();
                query = query.Where(g => g.Name.Contains(term) || g.StorageType.Contains(term)
                    || g.Location.Contains(term) || (g.District != null && g.District.Contains(term))
                    || g.Description.Contains(term) || g.Facilities.Contains(term)
                    || g.Owner!.FullName.Contains(term));
            }
            if (c.SelectedStorageTypes is { Count: > 0 })
                query = query.Where(g => c.SelectedStorageTypes.Contains(g.StorageType));

            var rawDistrict = !string.IsNullOrWhiteSpace(c.District) ? c.District : c.Location;
            if (!string.IsNullOrWhiteSpace(rawDistrict))
            {
                var targetDistrict = OnboardingOptions.GuessDistrict(rawDistrict.Trim()) ?? rawDistrict.Trim();
                var alt = GetDistrictAlias(targetDistrict);
                if (!string.IsNullOrEmpty(alt) && !alt.Equals(targetDistrict, StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(g => (g.District != null && (g.District == targetDistrict || g.District == alt))
                        || (g.District == null && (g.Location.Contains(targetDistrict) || g.Location.Contains(alt))));
                }
                else
                {
                    query = query.Where(g => (g.District != null && g.District == targetDistrict)
                        || (g.District == null && g.Location.Contains(targetDistrict)));
                }
            }

            if (c.SelectedMinPrice.HasValue && c.SelectedMinPrice.Value > 0)
                query = query.Where(g => g.PricePerTonPerMonth >= c.SelectedMinPrice.Value);
            if (c.SelectedMaxPrice.HasValue && c.SelectedMaxPrice.Value > 0)
                query = query.Where(g => g.PricePerTonPerMonth <= c.SelectedMaxPrice.Value);

            // Occupancy is measured for the requested window when given, otherwise for today.
            var windowStart = (c.AvailableStartDate ?? today).Date;
            var windowEnd = (c.AvailableEndDate ?? windowStart).Date;
            if (windowEnd < windowStart) windowEnd = windowStart;
            if (c.AvailableStartDate.HasValue || c.AvailableEndDate.HasValue)
                query = query.Where(g => !g.BlockedDates.Any(d => d.Date >= windowStart && d.Date <= windowEnd));

            if (c.SelectedMinCapacity is > 0)
            {
                var minCap = c.SelectedMinCapacity.Value;
                query = query.Where(g => (g.CapacityInTons - (g.Bookings
                    .Where(b => BookingStatus.Confirmed.Contains(b.Status) && b.StartDate <= windowEnd && windowStart <= b.EndDate)
                    .Sum(b => (double?)b.StorageTons) ?? 0)) >= minCap);
            }

            if (c.AvailableStartDate.HasValue || c.AvailableEndDate.HasValue)
            {
                query = query.Where(g => (g.CapacityInTons - (g.Bookings
                    .Where(b => BookingStatus.Confirmed.Contains(b.Status) && b.StartDate <= windowEnd && windowStart <= b.EndDate)
                    .Sum(b => (double?)b.StorageTons) ?? 0)) > 0);
            }

            var sort = (c.SortBy ?? "newest").ToLowerInvariant();
            query = sort switch
            {
                "price_asc" => query.OrderBy(g => g.PricePerTonPerMonth),
                "price_desc" => query.OrderByDescending(g => g.PricePerTonPerMonth),
                "capacity_desc" => query.OrderByDescending(g => g.CapacityInTons - (g.Bookings
                    .Where(b => BookingStatus.Confirmed.Contains(b.Status) && b.StartDate <= windowEnd && windowStart <= b.EndDate)
                    .Sum(b => (double?)b.StorageTons) ?? 0)),
                "location" or "distance" => query.OrderBy(g => g.District).ThenBy(g => g.Location).ThenByDescending(g => g.CreatedAt),
                "rating_desc" => query.OrderByDescending(g => g.AverageRating).ThenByDescending(g => g.ReviewCount),
                _ => query.OrderByDescending(g => g.CreatedAt)
            };

            var totalCount = await query.CountAsync();
            var page = c.Page > 0 ? c.Page : 1;
            var pageSize = c.PageSize > 0 ? c.PageSize : 24;

            var rows = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(g => new
                {
                    g.Id,
                    g.Name,
                    g.StorageType,
                    g.Location,
                    g.District,
                    g.Latitude,
                    g.Longitude,
                    g.CapacityInTons,
                    g.PricePerTonPerMonth,
                    g.ImageUrls,
                    g.Facilities,
                    g.AverageRating,
                    g.ReviewCount,
                    CreatedAt = g.CreatedAt,
                    OwnerName = g.Owner!.FullName,
                    OwnerIsVerified = g.Owner.IsVerified,
                    OwnerVerificationStatus = g.Owner.VerificationStatus,
                    OwnerRating = g.Owner.OwnerAverageRating,
                    OwnerReviewCount = g.Owner.OwnerReviewCount,
                    Occupied = g.Bookings
                        .Where(b => BookingStatus.Confirmed.Contains(b.Status) && b.StartDate <= windowEnd && windowStart <= b.EndDate)
                        .Sum(b => (double?)b.StorageTons) ?? 0
                }).ToListAsync();

            var items = rows.Select(g => new GodownItemViewModel
            {
                Id = g.Id,
                Name = g.Name,
                StorageType = g.StorageType,
                Location = g.Location,
                District = g.District,
                Latitude = g.Latitude,
                Longitude = g.Longitude,
                TotalCapacityTons = g.CapacityInTons,
                AvailableCapacityTons = Math.Max(0, g.CapacityInTons - g.Occupied),
                PricePerTonPerMonth = g.PricePerTonPerMonth,
                ImageUrl = ListingFormat.Split(g.ImageUrls).FirstOrDefault() ?? string.Empty,
                OwnerName = g.OwnerName,
                OwnerIsVerified = g.OwnerIsVerified,
                OwnerVerificationStatus = g.OwnerVerificationStatus ?? "Unverified",
                Rating = g.AverageRating,
                ReviewCount = g.ReviewCount,
                OwnerRating = g.OwnerRating,
                OwnerReviewCount = g.OwnerReviewCount,
                Facilities = ListingFormat.Split(g.Facilities),
                CreatedAt = g.CreatedAt
            }).ToList();

            HashSet<int> favoriteIds = new();
            if (!string.IsNullOrWhiteSpace(c.CurrentUserId))
            {
                favoriteIds = await _favoriteService.GetIdsAsync(c.CurrentUserId, ListingTypes.Godown);
                foreach (var item in items)
                {
                    item.IsFavorite = favoriteIds.Contains(item.Id);
                }
            }

            var model = new GodownBrowseViewModel
            {
                SearchTerm = c.SearchTerm,
                SelectedStorageTypes = c.SelectedStorageTypes ?? new List<string>(),
                District = rawDistrict,
                Location = rawDistrict,
                SelectedMinCapacity = c.SelectedMinCapacity,
                SelectedMinPrice = c.SelectedMinPrice,
                SelectedMaxPrice = c.SelectedMaxPrice ?? 2500,
                AvailableStartDate = c.AvailableStartDate,
                AvailableEndDate = c.AvailableEndDate,
                SortBy = sort,
                GodownList = items,
                FavoriteIds = favoriteIds,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                AvailableStorageTypes = new List<string>(OnboardingOptions.StorageTypes),
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

        public async Task<GodownDetailViewModel?> GetDetailsAsync(int id, string? currentUserId = null)
        {
            var g = await _godowns.Query().Include(x => x.Owner).FirstOrDefaultAsync(x => x.Id == id);
            if (g is null) return null;

            var available = Math.Max(0, g.CapacityInTons - await OccupiedTonsAsync(id, DateTime.Today, DateTime.Today));

            var from = DateTime.Today;
            var to = from.AddMonths(CalendarHorizonMonths);
            var accepted = await _bookings.Query()
                .Where(b => b.GodownId == id && BookingStatus.Confirmed.Contains(b.Status) && b.EndDate >= from && b.StartDate <= to)
                .Select(b => new { b.StartDate, b.EndDate, b.StorageTons })
                .ToListAsync();
            var blocked = await _blockedDates.Query()
                .Where(d => d.GodownId == id && d.Date >= from && d.Date <= to)
                .Select(d => d.Date)
                .ToListAsync();
            var fullDays = EachDay(from, to)
                .Where(day => accepted.Where(b => b.StartDate <= day && day <= b.EndDate).Sum(b => b.StorageTons) >= g.CapacityInTons);
            var reviewsList = await _reviews.GetReviewsForGodownAsync(id);

            var lat = g.Latitude;
            var lng = g.Longitude;
            if (!lat.HasValue || !lng.HasValue)
            {
                var (fallbackLat, fallbackLng) = GeoLocationHelper.GetDistrictCoordinates(g.Location);
                lat = fallbackLat;
                lng = fallbackLng;
            }

            var model = new GodownDetailViewModel
            {
                Id = g.Id,
                Name = g.Name,
                StorageType = g.StorageType,
                Location = g.Location,
                District = g.District ?? OnboardingOptions.GuessDistrict(g.Location),
                Latitude = lat,
                Longitude = lng,
                Description = g.Description,
                TotalCapacityTons = g.CapacityInTons,
                AvailableCapacityTons = available,
                UnavailableDates = blocked.Concat(fullDays).Distinct().OrderBy(d => d).ToList(),
                PricePerTonPerMonth = $"{ListingFormat.Taka(g.PricePerTonPerMonth)} / Ton / Month",
                PricePerTonPerMonthAmount = g.PricePerTonPerMonth,
                DailyRatePerTon = $"{ListingFormat.Taka(Math.Round(g.PricePerTonPerMonth / 30m, 2))} / Ton / Day",
                Status = !g.IsActive ? "Inactive" : available > 0 ? "Available" : "Fully Booked",
                OwnerName = g.Owner?.FullName ?? string.Empty,
                OwnerIsVerified = g.Owner?.IsVerified ?? false,
                OwnerVerificationStatus = g.Owner?.VerificationStatus ?? "Unverified",
                OwnerPhone = g.Owner?.PhoneNumber ?? string.Empty,
                OwnerMemberSince = ListingFormat.MemberSince(g.Owner?.CreatedAt ?? g.CreatedAt),
                OwnerRating = g.Owner?.OwnerAverageRating ?? 0.0,
                TotalReviews = g.Owner?.OwnerReviewCount ?? 0,
                AverageRating = g.AverageRating,
                ReviewCount = g.ReviewCount,
                ImageUrls = ListingFormat.Split(g.ImageUrls),
                Facilities = ListingFormat.Split(g.Facilities),
                StartDate = DateTime.Today.AddDays(1),
                EndDate = DateTime.Today.AddMonths(1),
                RequestedCapacityTons = Math.Min(10, available),
                Reviews = reviewsList
            };

            if (!string.IsNullOrWhiteSpace(g.OwnerId))
            {
                var badges = await _badges.GetOwnerBadgesAsync(g.OwnerId);
                model.OwnerBadges = badges.Where(b => b.IsEarned).ToList();
                var (rank, trust, total) = await _leaderboard.GetOwnerRankAsync(g.OwnerId);
                if (rank.HasValue)
                {
                    model.OwnerRankText = $"Rank #{rank.Value} Top Host";
                }
            }

            // Populate Farmer Loyalty Context if user is authenticated
            if (!string.IsNullOrWhiteSpace(currentUserId))
            {
                var userFavs = await _favoriteService.GetIdsAsync(currentUserId, ListingTypes.Godown);
                model.IsFavorite = userFavs.Contains(id);

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

        public async Task<string?> RequestStorageAsync(string farmerId, GodownDetailViewModel r, string? promoCode = null, int? pointsToRedeem = null)
        {
            var res = await RequestStorageWithResultAsync(farmerId, r, promoCode, pointsToRedeem);
            return res.Error;
        }

        public async Task<(string? Error, int? BookingId)> RequestStorageWithResultAsync(string farmerId, GodownDetailViewModel r, string? promoCode = null, int? pointsToRedeem = null, int? harvestPlanId = null, string? planName = null)
        {
            if (r.StartDate is null || r.EndDate is null) return ("Please choose a start and end date.", null);
            var s = r.StartDate.Value.Date;
            var t = r.EndDate.Value.Date;
            if (s < DateTime.Today) return ("Start date cannot be in the past.", null);
            if (t <= s) return ("End date must be after the start date.", null);
            if (r.RequestedCapacityTons <= 0) return ("Requested capacity must be greater than zero.", null);

            var g = await _godowns.Query().FirstOrDefaultAsync(x => x.Id == r.Id);
            if (g is null) return ("This storage facility no longer exists.", null);
            if (!g.IsActive) return ("This storage facility is not accepting bookings right now.", null);
            if (g.OwnerId == farmerId) return ("You cannot book your own storage facility.", null);

            var clash = await FindConflictAsync(g, r.RequestedCapacityTons, s, t);
            if (clash is not null) return (clash, null);

            var days = (t - s).Days + 1;
            var months = Math.Max(1.0, (double)days / 30.0);
            decimal gross = decimal.Round((decimal)r.RequestedCapacityTons * g.PricePerTonPerMonth * (decimal)months, 0);

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

            var newBooking = new GodownBooking
            {
                GodownId = g.Id,
                FarmerId = farmerId,
                StorageTons = r.RequestedCapacityTons,
                StartDate = s,
                EndDate = t,
                Note = string.IsNullOrWhiteSpace(r.BookingNotes) ? null : r.BookingNotes.Trim(),
                Status = BookingStatus.Pending,
                RequestedOn = DateTime.Now,
                DiscountAmount = discountAmount,
                AppliedPromoCode = appliedPromo,
                PointsUsed = pointsUsed,
                HarvestPlanId = harvestPlanId
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
                    "Godown",
                    newBooking.Id,
                    $"#GD-{newBooking.Id:D4}"
                );
            }

            // Notify godown owner of new pending storage request
            var farmer = await _users.FirstOrDefaultAsync(u => u.Id == farmerId);
            var farmerName = farmer?.FullName ?? "A farmer";
            var hasPlan = !string.IsNullOrWhiteSpace(planName);
            await _notifications.NotifyAsync(new NotificationRequest
            {
                UserId = g.OwnerId,
                Type = NotificationTypes.BookingRequest,
                TitleKey = "New Godown Storage Request",
                MessageKey = hasPlan
                    ? "{0} requested {1} tons of storage at {2} from {3} to {4} as part of harvest plan \"{5}\"."
                    : "{0} requested storage for {1} tons in {2} from {3} to {4}.",
                Args = hasPlan
                    ? new object[] { farmerName, r.RequestedCapacityTons, g.Name, $"{s:dd MMM yyyy}", $"{t:dd MMM yyyy}", planName!.Trim() }
                    : new object[] { farmerName, r.RequestedCapacityTons, g.Name, $"{s:dd MMM yyyy}", $"{t:dd MMM yyyy}" },
                LinkUrl = AppLinks.OwnerRequests("godown", newBooking.Id),
                DedupeKey = $"booking:godown:{newBooking.Id}:Requested",
                SendEmail = false
            });

            return (null, newBooking.Id);
        }

        // ---------------------------------------------------------------- Owner

        public async Task<GodownOwnerDashboardViewModel> GetOwnerDashboardAsync(string ownerId)
        {
            var godowns = await OwnerGodownsAsync(ownerId);
            var pending = await OwnerBookingsQuery(ownerId).Where(b => b.Status == BookingStatus.Pending).ToListAsync();

            var (rank, trustScore, totalRanked) = await _leaderboard.GetOwnerRankAsync(ownerId);
            var badgeWidget = await _badges.GetOwnerBadgeWidgetAsync(ownerId, rank, totalRanked);
            badgeWidget.TrustScore = trustScore;

            return new GodownOwnerDashboardViewModel
            {
                TotalGodowns = godowns.Count,
                TotalCapacityTons = godowns.Sum(g => g.TotalCapacityTons),
                OccupiedCapacityTons = godowns.Sum(g => g.OccupiedTons),
                Godowns = godowns,
                PendingRequestItems = pending.Select(ToRequestItem).OrderByDescending(r => r.RequestedOn).ToList(),
                BadgeWidget = badgeWidget
            };
        }

        public async Task<GodownBookingRequestsViewModel> GetOwnerRequestsAsync(string ownerId)
        {
            var bookings = await OwnerBookingsQuery(ownerId).ToListAsync();
            var items = bookings.Select(ToRequestItem).OrderByDescending(r => r.RequestedOn).ToList();

            var pendingItems = items.Where(i => i.Status == BookingStatus.Pending).ToList();
            if (pendingItems.Any())
            {
                var godownIds = pendingItems.Select(p => p.GodownId).Distinct().ToList();
                var allBlocked = await _blockedDates.Query()
                    .Where(d => godownIds.Contains(d.GodownId))
                    .ToListAsync();

                foreach (var pending in pendingItems)
                {
                    var source = bookings.First(b => b.Id == pending.Id);
                    var blocked = allBlocked
                        .Where(d => d.GodownId == source.GodownId && d.Date >= source.StartDate && d.Date <= source.EndDate)
                        .OrderBy(d => d.Date)
                        .FirstOrDefault();

                    if (blocked != null)
                    {
                        pending.HasConflict = true;
                        pending.ConflictHint = $"Overlaps a date you blocked ({blocked.Date:dd MMM yyyy})";
                    }
                    else if (source.Godown != null)
                    {
                        var conflict = await FindConflictAsync(source.Godown, source.StorageTons, source.StartDate, source.EndDate, excludeBookingId: source.Id);
                        if (conflict != null)
                        {
                            pending.HasConflict = true;
                            pending.ConflictHint = conflict;
                        }
                    }
                }
            }

            var farmerIds = items.Select(i => i.FarmerId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (farmerIds.Count > 0)
            {
                var summaries = await _farmerProfile.GetSummariesAsync(farmerIds);
                foreach (var item in items)
                {
                    if (!string.IsNullOrEmpty(item.FarmerId) && summaries.TryGetValue(item.FarmerId, out var s))
                    {
                        item.FarmerCompleted = s.Completed;
                        item.FarmerCancellationRate = s.CancellationRate;
                        item.FarmerMemberSince = s.MemberSince;
                        item.FarmerTrustLevel = s.TrustLevel;
                    }
                }
            }

            return new GodownBookingRequestsViewModel
            {
                Requests = items,
                Godowns = await OwnerGodownsAsync(ownerId)
            };
        }

        public Task<int> CountPendingAsync(string ownerId) =>
            _bookings.Query().CountAsync(b => b.Godown!.OwnerId == ownerId && b.Status == BookingStatus.Pending);

        public async Task<DecisionResult> RespondAsync(string ownerId, int bookingId, string decision, string? reason)
        {
            var booking = await _bookings.QueryTracked().Include(b => b.Godown).Include(b => b.Payment)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.Godown!.OwnerId == ownerId);
            if (booking is null) return DecisionResult.Fail("This booking request could not be found.");

            var next = BookingWorkflow.Next(booking.Status, decision);
            var guard = BookingWorkflow.Guard(booking, decision, next);
            if (guard is not null) return DecisionResult.Fail(guard);

            if (string.Equals(decision, "undo", StringComparison.OrdinalIgnoreCase) && booking.Status == BookingStatus.Completed)
            {
                var existingReview = await _reviews.GetReviewByBookingAsync("Godown", booking.Id);
                if (existingReview != null)
                {
                    return DecisionResult.Fail("This booking has been reviewed by the farmer and can no longer be reopened.");
                }
            }

            var autoRejected = new List<int>();
            if (next == BookingStatus.Accepted)
            {
                var godown = booking.Godown!;
                var clash = await FindConflictAsync(godown, booking.StorageTons, booking.StartDate, booking.EndDate, excludeBookingId: booking.Id);
                if (clash is not null) return DecisionResult.Fail(clash);

                // Overlapping pending requests that no longer fit once this one occupies capacity are declined with a reason.
                var overlapping = await _bookings.QueryTracked()
                    .Where(b => b.GodownId == booking.GodownId && b.Id != booking.Id && b.Status == BookingStatus.Pending
                        && b.StartDate <= booking.EndDate && booking.StartDate <= b.EndDate)
                    .ToListAsync();
                foreach (var other in overlapping)
                {
                    var occupied = await OccupiedTonsAsync(godown.Id, other.StartDate, other.EndDate) + booking.StorageTons;
                    if (occupied + other.StorageTons <= godown.CapacityInTons) continue;
                    other.Status = BookingStatus.Rejected;
                    other.RejectReason = BookingWorkflow.AutoRejectReason;
                    other.UpdatedOn = DateTime.Now;
                    autoRejected.Add(other.Id);

                    // Refund points if promo/points were redeemed on auto-rejected booking
                    if (other.PointsUsed > 0 || other.DiscountAmount > 0)
                    {
                        await _loyalty.RefundPointsForCancelledBookingAsync(other.FarmerId, "Godown", other.Id, $"#GD-{other.Id:D4}");
                    }

                    // Notify auto-rejected farmer
                    var otherUser = await _users.FirstOrDefaultAsync(u => u.Id == other.FarmerId);
                    await _notifications.NotifyAsync(new NotificationRequest
                    {
                        UserId = other.FarmerId,
                        Type = NotificationTypes.BookingRejected,
                        TitleKey = "Storage Request Declined",
                        MessageKey = "Your storage request for {0} tons in {1} ({2} - {3}) was declined due to capacity constraints.",
                        Args = new object[] { other.StorageTons, godown.Name, $"{other.StartDate:dd MMM yyyy}", $"{other.EndDate:dd MMM yyyy}" },
                        LinkUrl = AppLinks.FarmerBookings("godown", other.Id),
                        DedupeKey = $"booking:godown:{other.Id}:AutoRejected",
                        SendEmail = true,
                        RecipientEmail = otherUser?.Email
                    });
                }
            }

            var listing = booking.Godown!;
            BookingWorkflow.ApplyMoney(booking, next!, "Godown", ownerId, _ledger,
                listing.PricePerTonPerMonth, BookingPricing.GodownGross(booking.StartDate, booking.EndDate, booking.StorageTons, listing.PricePerTonPerMonth), _revenue.PlatformCommissionRate);
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
                    TitleKey = "Storage Request Accepted",
                    MessageKey = "Your storage request for {0} tons in {1} ({2} - {3}) was accepted. Please pay ৳{4} to confirm.",
                    Args = new object[] { booking.StorageTons, booking.Godown!.Name, $"{booking.StartDate:dd MMM yyyy}", $"{booking.EndDate:dd MMM yyyy}", $"{booking.AgreedGross ?? 0:N0}" },
                    LinkUrl = AppLinks.FarmerBookings("godown", booking.Id),
                    DedupeKey = $"booking:godown:{booking.Id}:Accepted",
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
                    TitleKey = "Storage Request Declined",
                    MessageKey = hasReason
                        ? "Your storage request for {0} tons in {1} ({2} - {3}) was declined. Reason: {4}"
                        : "Your storage request for {0} tons in {1} ({2} - {3}) was declined.",
                    Args = hasReason
                        ? new object[] { booking.StorageTons, booking.Godown!.Name, $"{booking.StartDate:dd MMM yyyy}", $"{booking.EndDate:dd MMM yyyy}", booking.RejectReason! }
                        : new object[] { booking.StorageTons, booking.Godown!.Name, $"{booking.StartDate:dd MMM yyyy}", $"{booking.EndDate:dd MMM yyyy}" },
                    LinkUrl = AppLinks.FarmerBookings("godown", booking.Id),
                    DedupeKey = $"booking:godown:{booking.Id}:Rejected",
                    SendEmail = true,
                    RecipientEmail = farmer?.Email
                });

                // Refund points if promo/points were redeemed
                if (booking.PointsUsed > 0 || booking.DiscountAmount > 0)
                {
                    await _loyalty.RefundPointsForCancelledBookingAsync(booking.FarmerId, "Godown", booking.Id, $"#GD-{booking.Id:D4}");
                }
            }
            else if (next == BookingStatus.Completed)
            {
                await _notifications.NotifyAsync(new NotificationRequest
                {
                    UserId = booking.FarmerId,
                    Type = NotificationTypes.BookingCompleted,
                    TitleKey = "Storage Booking Completed",
                    MessageKey = "Your storage booking at {0} is completed. Please take a moment to rate and review your experience!",
                    Args = new object[] { booking.Godown!.Name },
                    LinkUrl = AppLinks.FarmerBookingsReview("godown", booking.Id),
                    DedupeKey = $"booking:godown:{booking.Id}:Completed",
                    SendEmail = false
                });

                // Award loyalty points on what the farmer actually paid (snapshot), net of discounts
                decimal netSpent = Math.Max(0m, (booking.AgreedGross ?? 0) - booking.DiscountAmount);
                await _loyalty.AwardPointsForCompletedBookingAsync(
                    booking.FarmerId,
                    "Godown",
                    booking.Id,
                    netSpent,
                    $"#GD-{booking.Id:D4}"
                );
            }

            return DecisionResult.Ok(autoRejected);
        }

        public async Task<GodownListingViewModel?> GetListingAsync(string ownerId, int id)
        {
            var g = await _godowns.Query().FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId);
            if (g is null) return null;

            return new GodownListingViewModel
            {
                Id = g.Id,
                Name = g.Name,
                Category = g.StorageType,
                Location = g.Location,
                District = g.District ?? OnboardingOptions.GuessDistrict(g.Location) ?? string.Empty,
                Latitude = g.Latitude,
                Longitude = g.Longitude,
                TotalCapacity = g.CapacityInTons,
                CapacityUnit = "Tons",
                AvailableCapacity = Math.Max(0, g.CapacityInTons - await OccupiedTonsAsync(g.Id, DateTime.Today, DateTime.Today)),
                PriceAmount = g.PricePerTonPerMonth,
                PricePeriod = "Month",
                Description = g.Description,
                IsAvailable = g.IsActive,
                SelectedFacilities = ListingFormat.Split(g.Facilities),
                ExistingImageUrls = ListingFormat.Split(g.ImageUrls)
            };
        }

        public async Task<bool> SaveListingAsync(string ownerId, GodownListingViewModel model)
        {
            Godown entity;
            List<string> previousImagesToDelete = new();

            if (model.IsEditMode)
            {
                var existing = await _godowns.QueryTracked().FirstOrDefaultAsync(x => x.Id == model.Id && x.OwnerId == ownerId);
                if (existing is null) return false;
                entity = existing;

                // Identify removed images to delete after successful save
                var previousImages = ListingFormat.Split(entity.ImageUrls);
                var retainedExisting = model.ExistingImageUrls ?? new List<string>();
                previousImagesToDelete = previousImages.Where(img => !retainedExisting.Contains(img, StringComparer.OrdinalIgnoreCase)).ToList();
            }
            else
            {
                entity = new Godown { OwnerId = ownerId, CreatedAt = DateTime.UtcNow };
                await _godowns.AddAsync(entity);
            }

            var savedNewImages = await _files.SaveImagesAsync(model.ImageFiles, UploadFolder);
            try
            {
                var retainedUrls = model.ExistingImageUrls ?? new List<string>();
                var orderedImages = ArrangeImagesWithPrimary(retainedUrls, savedNewImages, model.PrimaryImageKey);

                entity.Name = model.Name.Trim();
                entity.StorageType = model.Category.Trim();
                entity.Location = model.Location.Trim();
                entity.District = string.IsNullOrWhiteSpace(model.District)
                    ? OnboardingOptions.GuessDistrict(model.Location)
                    : model.District.Trim();

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

                entity.Description = model.Description.Trim();
                entity.CapacityInTons = ToTons(model.TotalCapacity, model.CapacityUnit);
                entity.PricePerTonPerMonth = model.PricePeriod == "Day" ? model.PriceAmount * 30 : model.PriceAmount;
                entity.IsActive = model.IsAvailable;
                entity.Facilities = ListingFormat.Join(model.SelectedFacilities);
                entity.ImageUrls = ListingFormat.Join(orderedImages);

                await _godowns.SaveChangesAsync();

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

        public async Task<ManageAvailabilityViewModel?> GetAvailabilityAsync(string ownerId, int godownId, DateTime? month)
        {
            var g = await _godowns.Query().FirstOrDefaultAsync(x => x.Id == godownId && x.OwnerId == ownerId);
            if (g is null) return null;

            var first = new DateTime((month ?? DateTime.Today).Year, (month ?? DateTime.Today).Month, 1);
            var last = first.AddMonths(1).AddDays(-1);

            var blockedRows = await _blockedDates.Query()
                .Where(d => d.GodownId == godownId && d.Date >= first && d.Date <= last)
                .Select(d => new { d.Date, d.Reason })
                .ToListAsync();

            var today = DateTime.Today;
            var horizonEnd = today.AddMonths(12);
            var upcomingBlocked = await _blockedDates.Query()
                .Where(d => d.GodownId == godownId && d.Date >= today && d.Date <= horizonEnd)
                .OrderBy(d => d.Date)
                .Select(d => new { d.Date, d.Reason })
                .ToListAsync();

            var upcomingPeriods = DateRanges.Group(upcomingBlocked.Select(d => (d.Date, d.Reason)))
                .Select(g => new BlockedPeriodItem
                {
                    From = g.From,
                    To = g.To,
                    Reason = g.Reason
                })
                .ToList();

            return new ManageAvailabilityViewModel
            {
                ListingId = g.Id,
                ListingName = g.Name,
                Category = g.StorageType,
                RateText = $"{ListingFormat.Taka(g.PricePerTonPerMonth)} / Ton / Month",
                Location = g.Location,
                ThumbnailUrl = ListingFormat.Split(g.ImageUrls).FirstOrDefault() ?? string.Empty,
                Month = first,
                MonthName = first.ToString("MMMM yyyy"),
                FarmerBookedDates = (await StoredDaysAsync(godownId, first, last)).ToList(),
                OwnerBlockedDates = blockedRows.Select(d => d.Date).ToList(),
                BlockedReasonsByDate = blockedRows.ToDictionary(d => d.Date.ToString("yyyy-MM-dd"), d => d.Reason),
                UpcomingBlockedPeriods = upcomingPeriods
            };
        }

        public async Task<bool> SaveAvailabilityAsync(string ownerId, int godownId, DateTime month, IEnumerable<DateTime> blockedDates)
        {
            if (!await _godowns.Query().AnyAsync(x => x.Id == godownId && x.OwnerId == ownerId)) return false;

            var first = new DateTime(month.Year, month.Month, 1);
            var last = first.AddMonths(1).AddDays(-1);

            var current = await _blockedDates.QueryTracked()
                .Where(d => d.GodownId == godownId && d.Date >= first && d.Date <= last)
                .ToListAsync();

            var stored = await StoredDaysAsync(godownId, first, last);

            var targetDates = blockedDates
                .Select(d => d.Date)
                .Distinct()
                .Where(d => d >= first && d <= last && !stored.Contains(d))
                .ToHashSet();

            var toRemove = current.Where(c => !targetDates.Contains(c.Date)).ToList();
            _blockedDates.RemoveRange(toRemove);

            var currentDates = current.Select(c => c.Date).ToHashSet();
            foreach (var date in targetDates.Where(d => !currentDates.Contains(d)))
            {
                await _blockedDates.AddAsync(new GodownBlockedDate { GodownId = godownId, Date = date });
            }

            await _blockedDates.SaveChangesAsync();
            return true;
        }

        public async Task<BulkAvailabilityResult> BlockRangeAsync(string ownerId, int listingId, DateTime from, DateTime to, IReadOnlyCollection<DayOfWeek>? daysOfWeek, string? reason)
        {
            var f = from.Date;
            if (f < DateTime.Today) f = DateTime.Today;
            var t = to.Date;

            if (t < f)
                return new BulkAvailabilityResult(true, 0, 0, 0, "The end date must be on or after the start date.");

            if ((t - f).TotalDays + 1 > DateRanges.MaxBulkDays)
                return new BulkAvailabilityResult(true, 0, 0, 0, "You can block at most 366 days at a time.");

            var g = await _godowns.Query().FirstOrDefaultAsync(x => x.Id == listingId && x.OwnerId == ownerId);
            if (g is null)
                return new BulkAvailabilityResult(false, 0, 0, 0, null);

            var existingBlocked = (await _blockedDates.Query()
                .Where(d => d.GodownId == listingId && d.Date >= f && d.Date <= t)
                .Select(d => d.Date)
                .ToListAsync())
                .ToHashSet();

            var stored = await StoredDaysAsync(listingId, f, t);

            var days = DateRanges.Expand(f, t, daysOfWeek).ToList();
            int changed = 0, skippedBooked = 0, alreadyInState = 0;

            var cleanReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            if (cleanReason?.Length > 100) cleanReason = cleanReason[..100];

            foreach (var d in days)
            {
                if (stored.Contains(d))
                {
                    skippedBooked++;
                }
                else if (existingBlocked.Contains(d))
                {
                    alreadyInState++;
                }
                else
                {
                    await _blockedDates.AddAsync(new GodownBlockedDate
                    {
                        GodownId = listingId,
                        Date = d,
                        Reason = cleanReason
                    });
                    changed++;
                }
            }

            if (changed > 0)
            {
                try
                {
                    await _blockedDates.SaveChangesAsync();
                }
                catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
                {
                    return new BulkAvailabilityResult(true, 0, skippedBooked, alreadyInState, "A conflicting operation already modified blocked dates for these days. Please refresh and try again.");
                }
            }

            return new BulkAvailabilityResult(true, changed, skippedBooked, alreadyInState, null);
        }

        public async Task<BulkAvailabilityResult> UnblockRangeAsync(string ownerId, int listingId, DateTime from, DateTime to, IReadOnlyCollection<DayOfWeek>? daysOfWeek)
        {
            var f = from.Date;
            if (f < DateTime.Today) f = DateTime.Today;
            var t = to.Date;

            if (t < f)
                return new BulkAvailabilityResult(true, 0, 0, 0, "The end date must be on or after the start date.");

            if ((t - f).TotalDays + 1 > DateRanges.MaxBulkDays)
                return new BulkAvailabilityResult(true, 0, 0, 0, "You can block at most 366 days at a time.");

            var g = await _godowns.Query().FirstOrDefaultAsync(x => x.Id == listingId && x.OwnerId == ownerId);
            if (g is null)
                return new BulkAvailabilityResult(false, 0, 0, 0, null);

            var query = _blockedDates.QueryTracked()
                .Where(d => d.GodownId == listingId && d.Date >= f && d.Date <= t);

            var rows = await query.ToListAsync();
            var filterDays = daysOfWeek != null && daysOfWeek.Count > 0;
            if (filterDays)
            {
                rows = rows.Where(r => daysOfWeek!.Contains(r.Date.DayOfWeek)).ToList();
            }

            int count = rows.Count;
            if (count > 0)
            {
                _blockedDates.RemoveRange(rows);
                await _blockedDates.SaveChangesAsync();
            }

            return new BulkAvailabilityResult(true, count, 0, 0, null);
        }

        // ---------------------------------------------------------------- Helpers

        public async Task<string?> CheckAvailabilityAsync(int godownId, double tons, DateTime start, DateTime end, int? excludeBookingId = null)
        {
            var godown = await _godowns.GetByIdAsync(godownId);
            if (godown is null) return "Godown not found.";
            return await FindConflictAsync(godown, tons, start, end, excludeBookingId);
        }

        /// <summary>
        /// The single source of truth for godown over-booking: a blocked date inside [start, end], or requested tons
        /// exceeding the capacity left after accepted bookings in that window. Returns a message, or null when it fits.
        /// </summary>
        private async Task<string?> FindConflictAsync(Godown g, double tons, DateTime start, DateTime end, int? excludeBookingId = null)
        {
            var blocked = await _blockedDates.Query()
                .Where(d => d.GodownId == g.Id && d.Date >= start && d.Date <= end)
                .OrderBy(d => d.Date)
                .Select(d => (DateTime?)d.Date)
                .FirstOrDefaultAsync();
            if (blocked is not null)
                return $"The owner has closed this godown on {blocked:dd MMM yyyy}. Please choose different dates.";

            var free = g.CapacityInTons - await OccupiedTonsAsync(g.Id, start, end, excludeBookingId);
            return tons <= free
                ? null
                : $"Only {Math.Max(0, free):N0} tons are free for the selected period. Please reduce the quantity or change the dates.";
        }

        private async Task<HashSet<DateTime>> StoredDaysAsync(int godownId, DateTime from, DateTime to)
        {
            var accepted = await _bookings.Query()
                .Where(b => b.GodownId == godownId && BookingStatus.Confirmed.Contains(b.Status) && b.EndDate >= from && b.StartDate <= to)
                .Select(b => new { b.StartDate, b.EndDate })
                .ToListAsync();
            return accepted.SelectMany(b => EachDay(b.StartDate, b.EndDate)).Where(d => d >= from && d <= to).ToHashSet();
        }

        private static IEnumerable<DateTime> EachDay(DateTime start, DateTime end)
        {
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1)) yield return d;
        }

        private static double ToTons(double quantity, string unit) => unit switch
        {
            "Maunds" => quantity * 0.04,
            "Bags" => quantity * 0.05,
            "Quintals" => quantity * 0.1,
            _ => quantity
        };

        private async Task<double> OccupiedTonsAsync(int godownId, DateTime from, DateTime to, int? excludeBookingId = null) =>
            await _bookings.Query()
                .Where(b => b.GodownId == godownId && b.Id != excludeBookingId && BookingStatus.Confirmed.Contains(b.Status) && b.StartDate <= to && from <= b.EndDate)
                .SumAsync(b => (double?)b.StorageTons) ?? 0;

        private async Task<List<OwnerGodownItem>> OwnerGodownsAsync(string ownerId)
        {
            var today = DateTime.Today;
            var rows = await _godowns.Query()
                .Where(g => g.OwnerId == ownerId)
                .OrderByDescending(g => g.CreatedAt)
                .Select(g => new
                {
                    g.Id,
                    g.Name,
                    g.StorageType,
                    g.CapacityInTons,
                    g.IsActive,
                    Occupied = g.Bookings.Where(b => BookingStatus.Confirmed.Contains(b.Status) && b.StartDate <= today && today <= b.EndDate)
                        .Sum(b => (double?)b.StorageTons) ?? 0
                })
                .ToListAsync();

            return rows.Select(g =>
            {
                var available = Math.Max(0, g.CapacityInTons - g.Occupied);
                return new OwnerGodownItem
                {
                    Id = g.Id,
                    Name = g.Name,
                    StorageType = g.StorageType,
                    TotalCapacityTons = g.CapacityInTons,
                    AvailableCapacityTons = available,
                    Status = !g.IsActive ? "Inactive" : available <= 0 ? "Full" : "Active"
                };
            }).ToList();
        }

        private IQueryable<GodownBooking> OwnerBookingsQuery(string ownerId) =>
            _bookings.Query()
                .Include(b => b.Godown)
                .Include(b => b.Farmer)
                .Include(b => b.Payment)
                .Include(b => b.HarvestPlan)
                    .ThenInclude(p => p!.Items)
                .Where(b => b.Godown!.OwnerId == ownerId);

        private static GodownBookingRequestItem ToRequestItem(GodownBooking b)
        {
            var plan = b.HarvestPlan;
            string? otherItemsText = null;
            if (plan?.Items != null)
            {
                var others = plan.Items
                    .Where(i => i.BookingId != b.Id)
                    .Select(i => i.ItemType == HarvestPlanItemType.Equipment
                        ? $"{i.Units}x Equipment ({ListingFormat.DateRange(i.StartDate, i.EndDate)})"
                        : $"{i.Tons}T Storage ({ListingFormat.DateRange(i.StartDate, i.EndDate)})")
                    .ToList();
                otherItemsText = others.Count > 0
                    ? "Also in plan: " + string.Join(", ", others)
                    : "Single item in plan";
            }

            return new GodownBookingRequestItem
            {
                Id = b.Id,
                FarmerId = b.FarmerId,
                FarmerName = string.IsNullOrWhiteSpace(b.Farmer?.FullName) ? "Farmer" : b.Farmer!.FullName,
                GodownId = b.GodownId,
                GodownName = b.Godown?.Name ?? string.Empty,
                RequestedCapacityTons = b.StorageTons,
                DateRange = ListingFormat.DateRange(b.StartDate, b.EndDate),
                Note = b.Note,
                Status = b.Status,
                RejectReason = b.RejectReason,
                RequestedOn = b.RequestedOn,
                AgreedGross = b.AgreedGross ?? (b.Godown is null ? 0 : BookingPricing.GodownGross(b.StartDate, b.EndDate, b.StorageTons, b.Godown.PricePerTonPerMonth)),
                PaymentReference = b.Payment?.Status == PaymentStatus.Succeeded ? b.Payment.Reference : null,
                ModificationCount = b.ModificationCount,
                PreviousDetails = b.PreviousDetails,
                HarvestPlanId = b.HarvestPlanId,
                HarvestPlanName = plan?.Name,
                HarvestPlanItemCount = plan?.Items?.Count ?? 0,
                HarvestPlanOtherItems = otherItemsText
            };
        }
    }
}
