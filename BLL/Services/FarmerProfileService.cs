using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace KrishiLink.BLL.Services
{
    public interface IFarmerProfileService
    {
        /// <summary>Full profile; null when farmer not found or viewer not allowed.</summary>
        Task<FarmerProfileViewModel?> GetAsync(string farmerId, string viewerId, bool viewerIsAdmin);

        /// <summary>Compact stats for many farmers at once (request lists) — one grouped query per table, cached.</summary>
        Task<Dictionary<string, FarmerTrustSummary>> GetSummariesAsync(IEnumerable<string> farmerIds);
    }

    public class FarmerProfileService : IFarmerProfileService
    {
        private readonly IRepository<ApplicationUser> _users;
        private readonly IRepository<EquipmentBooking> _equipmentBookings;
        private readonly IRepository<GodownBooking> _godownBookings;
        private readonly IRepository<Review> _reviews;
        private readonly ILoyaltyService _loyalty;
        private readonly IMemoryCache _cache;
        private readonly ILogger<FarmerProfileService> _logger;

        public FarmerProfileService(
            IRepository<ApplicationUser> users,
            IRepository<EquipmentBooking> equipmentBookings,
            IRepository<GodownBooking> godownBookings,
            IRepository<Review> reviews,
            ILoyaltyService loyalty,
            IMemoryCache cache,
            ILogger<FarmerProfileService> logger)
        {
            _users = users;
            _equipmentBookings = equipmentBookings;
            _godownBookings = godownBookings;
            _reviews = reviews;
            _loyalty = loyalty;
            _cache = cache;
            _logger = logger;
        }

        public async Task<Dictionary<string, FarmerTrustSummary>> GetSummariesAsync(IEnumerable<string> farmerIds)
        {
            var result = new Dictionary<string, FarmerTrustSummary>(StringComparer.OrdinalIgnoreCase);
            var distinctIds = farmerIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctIds.Count == 0) return result;

            var missingIds = new List<string>();
            foreach (var id in distinctIds)
            {
                var cacheKey = $"farmer-trust:{id}";
                if (_cache.TryGetValue(cacheKey, out FarmerTrustSummary? cached) && cached != null)
                {
                    result[id] = cached;
                }
                else
                {
                    missingIds.Add(id);
                }
            }

            if (missingIds.Count > 0)
            {
                var usersMap = await _users.Query()
                    .Where(u => missingIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.CreatedAt })
                    .ToDictionaryAsync(u => u.Id, u => u.CreatedAt, StringComparer.OrdinalIgnoreCase);

                var eqStats = await _equipmentBookings.Query()
                    .Where(b => missingIds.Contains(b.FarmerId))
                    .GroupBy(b => b.FarmerId)
                    .Select(g => new
                    {
                        FarmerId = g.Key,
                        Completed = g.Count(b => b.Status == BookingStatus.Completed),
                        Cancelled = g.Count(b => b.Status == BookingStatus.Cancelled),
                        Decided = g.Count(b => b.Status == BookingStatus.Accepted ||
                                               b.Status == BookingStatus.Paid ||
                                               b.Status == BookingStatus.Completed ||
                                               b.Status == BookingStatus.Cancelled)
                    })
                    .ToDictionaryAsync(x => x.FarmerId, StringComparer.OrdinalIgnoreCase);

                var gdStats = await _godownBookings.Query()
                    .Where(b => missingIds.Contains(b.FarmerId))
                    .GroupBy(b => b.FarmerId)
                    .Select(g => new
                    {
                        FarmerId = g.Key,
                        Completed = g.Count(b => b.Status == BookingStatus.Completed),
                        Cancelled = g.Count(b => b.Status == BookingStatus.Cancelled),
                        Decided = g.Count(b => b.Status == BookingStatus.Accepted ||
                                               b.Status == BookingStatus.Paid ||
                                               b.Status == BookingStatus.Completed ||
                                               b.Status == BookingStatus.Cancelled)
                    })
                    .ToDictionaryAsync(x => x.FarmerId, StringComparer.OrdinalIgnoreCase);

                foreach (var id in missingIds)
                {
                    eqStats.TryGetValue(id, out var eq);
                    gdStats.TryGetValue(id, out var gd);

                    var completed = (eq?.Completed ?? 0) + (gd?.Completed ?? 0);
                    var cancelled = (eq?.Cancelled ?? 0) + (gd?.Cancelled ?? 0);
                    var decided = (eq?.Decided ?? 0) + (gd?.Decided ?? 0);
                    var cancellationRate = decided > 0 ? (double)cancelled / decided : 0.0;

                    var trustLevel = completed < 2
                        ? FarmerTrustLevel.New
                        : (decided >= 3 && cancellationRate > 0.30 ? FarmerTrustLevel.Caution : FarmerTrustLevel.Reliable);

                    var createdAt = usersMap.TryGetValue(id, out var dt) ? dt : DateTime.UtcNow;
                    var memberSince = ListingFormat.MemberSince(createdAt);

                    var summary = new FarmerTrustSummary
                    {
                        Completed = completed,
                        Cancelled = cancelled,
                        TotalDecided = decided,
                        CancellationRate = cancellationRate,
                        MemberSince = memberSince,
                        TrustLevel = trustLevel
                    };

                    _cache.Set($"farmer-trust:{id}", summary, TimeSpan.FromMinutes(5));
                    result[id] = summary;
                }
            }

            return result;
        }

        public async Task<FarmerProfileViewModel?> GetAsync(string farmerId, string viewerId, bool viewerIsAdmin)
        {
            if (string.IsNullOrWhiteSpace(farmerId)) return null;

            var farmer = await _users.Query().FirstOrDefaultAsync(u => u.Id == farmerId);
            if (farmer == null) return null;

            if (!viewerIsAdmin)
            {
                var hasEqBooking = await _equipmentBookings.Query()
                    .AnyAsync(b => b.FarmerId == farmerId && b.Equipment!.OwnerId == viewerId);

                var hasGdBooking = !hasEqBooking && await _godownBookings.Query()
                    .AnyAsync(b => b.FarmerId == farmerId && b.Godown!.OwnerId == viewerId);

                if (!hasEqBooking && !hasGdBooking)
                {
                    _logger.LogInformation("Viewer {ViewerId} is not authorized to view farmer trust profile {FarmerId}", viewerId, farmerId);
                    return null;
                }
            }

            var phoneVisible = viewerIsAdmin;
            if (!phoneVisible)
            {
                var hasConfirmedEq = await _equipmentBookings.Query()
                    .AnyAsync(b => b.FarmerId == farmerId && b.Equipment!.OwnerId == viewerId &&
                        (b.Status == BookingStatus.Accepted || b.Status == BookingStatus.Paid || b.Status == BookingStatus.Completed));

                var hasConfirmedGd = !hasConfirmedEq && await _godownBookings.Query()
                    .AnyAsync(b => b.FarmerId == farmerId && b.Godown!.OwnerId == viewerId &&
                        (b.Status == BookingStatus.Accepted || b.Status == BookingStatus.Paid || b.Status == BookingStatus.Completed));

                phoneVisible = hasConfirmedEq || hasConfirmedGd;
            }

            var eqList = await _equipmentBookings.Query()
                .Where(b => b.FarmerId == farmerId)
                .Select(b => new
                {
                    b.Id,
                    b.Status,
                    b.RequestedOn,
                    b.UpdatedOn,
                    b.PaidOn,
                    OwnerId = b.Equipment != null ? b.Equipment.OwnerId : null,
                    ListingName = b.Equipment != null ? b.Equipment.Name : "Equipment",
                    b.StartDate,
                    b.EndDate,
                    PaymentRefunded = b.Payment != null && (b.Payment.Status == PaymentStatus.Refunded || b.Payment.RefundedOn != null)
                })
                .ToListAsync();

            var gdList = await _godownBookings.Query()
                .Where(b => b.FarmerId == farmerId)
                .Select(b => new
                {
                    b.Id,
                    b.Status,
                    b.RequestedOn,
                    b.UpdatedOn,
                    b.PaidOn,
                    OwnerId = b.Godown != null ? b.Godown.OwnerId : null,
                    ListingName = b.Godown != null ? b.Godown.Name : "Storage",
                    b.StartDate,
                    b.EndDate,
                    PaymentRefunded = b.Payment != null && (b.Payment.Status == PaymentStatus.Refunded || b.Payment.RefundedOn != null)
                })
                .ToListAsync();

            var totalRequests = eqList.Count + gdList.Count;
            var completed = eqList.Count(b => b.Status == BookingStatus.Completed) + gdList.Count(b => b.Status == BookingStatus.Completed);
            var active = eqList.Count(b => b.Status == BookingStatus.Accepted || b.Status == BookingStatus.Paid) +
                         gdList.Count(b => b.Status == BookingStatus.Accepted || b.Status == BookingStatus.Paid);
            var cancelled = eqList.Count(b => b.Status == BookingStatus.Cancelled) + gdList.Count(b => b.Status == BookingStatus.Cancelled);
            var cancelledAfterPayment = eqList.Count(b => b.Status == BookingStatus.Cancelled && b.PaymentRefunded) +
                                        gdList.Count(b => b.Status == BookingStatus.Cancelled && b.PaymentRefunded);
            var rejected = eqList.Count(b => b.Status == BookingStatus.Rejected) + gdList.Count(b => b.Status == BookingStatus.Rejected);
            var decided = completed + active + cancelled;
            var cancellationRate = decided > 0 ? (double)cancelled / decided : 0.0;

            var trustLevel = completed < 2
                ? FarmerTrustLevel.New
                : (decided >= 3 && cancellationRate > 0.30 ? FarmerTrustLevel.Caution : FarmerTrustLevel.Reliable);

            var paymentHours = eqList
                .Where(b => b.PaidOn.HasValue && b.UpdatedOn.HasValue && b.PaidOn.Value >= b.UpdatedOn.Value)
                .Select(b => (b.PaidOn!.Value - b.UpdatedOn!.Value).TotalHours)
                .Concat(gdList
                    .Where(b => b.PaidOn.HasValue && b.UpdatedOn.HasValue && b.PaidOn.Value >= b.UpdatedOn.Value)
                    .Select(b => (b.PaidOn!.Value - b.UpdatedOn!.Value).TotalHours))
                .ToList();

            double? avgHoursToPay = paymentHours.Count > 0 ? Math.Round(paymentHours.Average(), 1) : null;

            var reviews = await _reviews.Query()
                .Where(r => r.FarmerId == farmerId)
                .Select(r => r.Rating)
                .ToListAsync();
            var reviewsGiven = reviews.Count;
            double? avgRatingGiven = reviews.Count > 0 ? Math.Round(reviews.Average(), 1) : null;

            var tier = _loyalty.CalculateTier(farmer.LoyaltyPoints);

            var eqWithViewer = eqList.Where(b => b.OwnerId == viewerId).ToList();
            var gdWithViewer = gdList.Where(b => b.OwnerId == viewerId).ToList();

            var withYouCompleted = eqWithViewer.Count(b => b.Status == BookingStatus.Completed) + gdWithViewer.Count(b => b.Status == BookingStatus.Completed);
            var withYouActive = eqWithViewer.Count(b => b.Status == BookingStatus.Accepted || b.Status == BookingStatus.Paid) +
                                gdWithViewer.Count(b => b.Status == BookingStatus.Accepted || b.Status == BookingStatus.Paid);
            var withYouCancelled = eqWithViewer.Count(b => b.Status == BookingStatus.Cancelled) + gdWithViewer.Count(b => b.Status == BookingStatus.Cancelled);

            var allWithViewerRequested = eqWithViewer.Select(b => b.RequestedOn)
                .Concat(gdWithViewer.Select(b => b.RequestedOn))
                .ToList();
            DateTime? firstBookingOn = allWithViewerRequested.Count > 0 ? allWithViewerRequested.Min() : null;

            var recentItems = eqWithViewer.Select(b => new
            {
                BookingType = "Equipment",
                BookingId = b.Id,
                b.ListingName,
                b.StartDate,
                b.EndDate,
                b.Status,
                b.RequestedOn
            })
                .Concat(gdWithViewer.Select(b => new
                {
                    BookingType = "Godown",
                    BookingId = b.Id,
                    b.ListingName,
                    b.StartDate,
                    b.EndDate,
                    b.Status,
                    b.RequestedOn
                }))
                .OrderByDescending(b => b.RequestedOn)
                .Take(5)
                .Select(b => new FarmerRecentBookingItem
                {
                    BookingType = b.BookingType,
                    BookingId = b.BookingId,
                    ListingName = b.ListingName,
                    StartDate = b.StartDate,
                    EndDate = b.EndDate,
                    Status = b.Status
                })
                .ToList();

            return new FarmerProfileViewModel
            {
                FarmerId = farmer.Id,
                FullName = string.IsNullOrWhiteSpace(farmer.FullName) ? "Farmer" : farmer.FullName,
                Location = farmer.Location,
                District = farmer.District,
                MainCrop = farmer.Specialization,
                MemberSince = ListingFormat.MemberSince(farmer.CreatedAt),
                LoyaltyTier = tier.TierName,
                TotalRequests = totalRequests,
                Completed = completed,
                Active = active,
                Cancelled = cancelled,
                CancelledAfterPayment = cancelledAfterPayment,
                Rejected = rejected,
                Decided = decided,
                CancellationRate = cancellationRate,
                AvgHoursToPay = avgHoursToPay,
                ReviewsGiven = reviewsGiven,
                AvgRatingGiven = avgRatingGiven,
                WithYou = new FarmerWithYouSummary
                {
                    Completed = withYouCompleted,
                    Active = withYouActive,
                    Cancelled = withYouCancelled,
                    FirstBookingOn = firstBookingOn
                },
                RecentWithYou = recentItems,
                TrustLevel = trustLevel,
                PhoneVisible = phoneVisible,
                Phone = phoneVisible ? farmer.PhoneNumber : null
            };
        }
    }
}
