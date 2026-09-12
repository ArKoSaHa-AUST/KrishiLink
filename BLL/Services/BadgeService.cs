using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace KrishiLink.BLL.Services
{
    public interface IBadgeService
    {
        Task<List<OwnerBadgeViewModel>> GetOwnerBadgesAsync(string ownerId);
        Task<OwnerBadgeDashboardWidgetViewModel> GetOwnerBadgeWidgetAsync(string ownerId, int? rank = null, int totalRanked = 0);
    }

    public class BadgeService : IBadgeService
    {
        private readonly ApplicationDbContext _db;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public BadgeService(ApplicationDbContext db, IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _localizer = localizer;
        }

        public async Task<List<OwnerBadgeViewModel>> GetOwnerBadgesAsync(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                return new List<OwnerBadgeViewModel>();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == ownerId);
            if (user == null)
                return new List<OwnerBadgeViewModel>();

            // Calculate raw owner activity metrics
            var eqCompletedBookings = await _db.EquipmentBookings
                .Where(b => b.Equipment != null && b.Equipment.OwnerId == ownerId && b.Status == "Completed")
                .CountAsync();

            var gdCompletedBookings = await _db.GodownBookings
                .Where(b => b.Godown != null && b.Godown.OwnerId == ownerId && b.Status == "Completed")
                .CountAsync();

            var totalCompletedBookings = eqCompletedBookings + gdCompletedBookings;

            var eqTotalRequests = await _db.EquipmentBookings
                .Where(b => b.Equipment != null && b.Equipment.OwnerId == ownerId)
                .CountAsync();

            var gdTotalRequests = await _db.GodownBookings
                .Where(b => b.Godown != null && b.Godown.OwnerId == ownerId)
                .CountAsync();

            var totalRequests = eqTotalRequests + gdTotalRequests;

            var eqConfirmedOrCompleted = await _db.EquipmentBookings
                .Where(b => b.Equipment != null && b.Equipment.OwnerId == ownerId && (b.Status == "Completed" || b.Status == "Confirmed"))
                .CountAsync();

            var gdAcceptedOrCompleted = await _db.GodownBookings
                .Where(b => b.Godown != null && b.Godown.OwnerId == ownerId && (b.Status == "Completed" || b.Status == "Accepted"))
                .CountAsync();

            var totalAcceptedOrCompleted = eqConfirmedOrCompleted + gdAcceptedOrCompleted;
            var fulfillmentRate = totalRequests > 0 ? (double)totalAcceptedOrCompleted / totalRequests * 100.0 : 0.0;

            // Rating & Reviews
            var eqRatings = await _db.Equipment
                .Where(e => e.OwnerId == ownerId && e.ReviewCount > 0)
                .Select(e => new { e.AverageRating, e.ReviewCount })
                .ToListAsync();

            var gdRatings = await _db.Godowns
                .Where(g => g.OwnerId == ownerId && g.ReviewCount > 0)
                .Select(g => new { g.AverageRating, g.ReviewCount })
                .ToListAsync();

            var totalReviews = eqRatings.Sum(r => r.ReviewCount) + gdRatings.Sum(r => r.ReviewCount);
            var totalWeightedRating = eqRatings.Sum(r => r.AverageRating * r.ReviewCount) + gdRatings.Sum(r => r.AverageRating * r.ReviewCount);
            var avgRating = totalReviews > 0 ? Math.Round(totalWeightedRating / totalReviews, 1) : 0.0;

            // Maintenance Records
            var maintenanceCount = await _db.EquipmentMaintenanceRecords
                .Where(m => m.Equipment != null && m.Equipment.OwnerId == ownerId)
                .CountAsync();

            var registrationDays = (DateTime.UtcNow - user.CreatedAt).TotalDays;

            var badges = new List<OwnerBadgeViewModel>();

            // 1. Verified Owner Badge
            var isVerified = user.IsVerified;
            badges.Add(new OwnerBadgeViewModel
            {
                Code = "VERIFIED_OWNER",
                Title = _localizer["Verified Owner"],
                Description = _localizer["Identity and official credentials authenticated by KrishiLink."],
                CriteriaDescription = _localizer["Submit NID or Trade License and receive approval."],
                Category = "Trust",
                IconClass = "bi-patch-check-fill",
                BadgeColorClass = "bg-success text-white",
                GradientClass = "gradient-success",
                IsEarned = isVerified,
                ProgressPercentage = isVerified ? 100 : (user.VerificationStatus == "Pending" ? 50 : 0),
                ProgressText = isVerified ? _localizer["Verified"] : (user.VerificationStatus == "Pending" ? _localizer["Pending Review"] : _localizer["Unverified"]),
                DisplayOrder = 1
            });

            // 2. Century Host (100+ Bookings)
            var isCentury = totalCompletedBookings >= 100;
            badges.Add(new OwnerBadgeViewModel
            {
                Code = "CENTURY_HOST",
                Title = _localizer["Century Host"],
                Description = _localizer["Completed 100 or more successful rental and storage bookings."],
                CriteriaDescription = _localizer["Complete 100 successful bookings on KrishiLink."],
                Category = "Volume",
                IconClass = "bi-trophy-fill",
                BadgeColorClass = "bg-warning text-dark",
                GradientClass = "gradient-gold",
                IsEarned = isCentury,
                CurrentValue = totalCompletedBookings,
                TargetValue = 100,
                ProgressPercentage = Math.Min(100, (int)Math.Round((double)totalCompletedBookings / 100 * 100)),
                ProgressText = $"{totalCompletedBookings} / 100 {_localizer["Bookings"]}",
                DisplayOrder = 2
            });

            // 3. Pro Host (50+ Bookings)
            var isPro = totalCompletedBookings >= 50;
            badges.Add(new OwnerBadgeViewModel
            {
                Code = "PRO_HOST",
                Title = _localizer["Pro Host"],
                Description = _localizer["Completed 50 or more successful bookings."],
                CriteriaDescription = _localizer["Complete 50 successful bookings."],
                Category = "Volume",
                IconClass = "bi-stars",
                BadgeColorClass = "bg-purple text-white",
                GradientClass = "gradient-purple",
                IsEarned = isPro,
                CurrentValue = totalCompletedBookings,
                TargetValue = 50,
                ProgressPercentage = Math.Min(100, (int)Math.Round((double)totalCompletedBookings / 50 * 100)),
                ProgressText = $"{totalCompletedBookings} / 50 {_localizer["Bookings"]}",
                DisplayOrder = 3
            });

            // 4. Active Host (10+ Bookings)
            var isActive = totalCompletedBookings >= 10;
            badges.Add(new OwnerBadgeViewModel
            {
                Code = "ACTIVE_HOST",
                Title = _localizer["Active Host"],
                Description = _localizer["Completed 10 or more successful bookings."],
                CriteriaDescription = _localizer["Complete 10 successful bookings."],
                Category = "Volume",
                IconClass = "bi-lightning-charge-fill",
                BadgeColorClass = "bg-primary text-white",
                GradientClass = "gradient-primary",
                IsEarned = isActive,
                CurrentValue = totalCompletedBookings,
                TargetValue = 10,
                ProgressPercentage = Math.Min(100, (int)Math.Round((double)totalCompletedBookings / 10 * 100)),
                ProgressText = $"{totalCompletedBookings} / 10 {_localizer["Bookings"]}",
                DisplayOrder = 4
            });

            // 5. Top Rated Host (4.7+ Rating with >= 3 Reviews)
            var isTopRated = avgRating >= 4.7 && totalReviews >= 3;
            var ratingProgress = totalReviews >= 3
                ? Math.Min(100, (int)Math.Round(avgRating / 4.7 * 100))
                : Math.Min(100, (int)Math.Round((double)totalReviews / 3 * 50));
            badges.Add(new OwnerBadgeViewModel
            {
                Code = "TOP_RATED",
                Title = _localizer["Top Rated Host"],
                Description = _localizer["Maintains an average rating of 4.7★ or higher with trusted farmer reviews."],
                CriteriaDescription = _localizer["Achieve 4.7+ star rating with at least 3 customer reviews."],
                Category = "Quality",
                IconClass = "bi-star-fill",
                BadgeColorClass = "bg-amber text-dark",
                GradientClass = "gradient-amber",
                IsEarned = isTopRated,
                ProgressPercentage = ratingProgress,
                ProgressText = $"{avgRating:F1} ★ ({totalReviews} {_localizer["Reviews"]})",
                DisplayOrder = 5
            });

            // 6. Quick Responder (80%+ Fulfillment)
            var isQuickResponder = totalRequests >= 3 && fulfillmentRate >= 80.0;
            badges.Add(new OwnerBadgeViewModel
            {
                Code = "QUICK_RESPONDER",
                Title = _localizer["Quick Responder"],
                Description = _localizer["Demonstrates exceptional reliability with 80%+ request acceptance rate."],
                CriteriaDescription = _localizer["Maintain an 80%+ booking acceptance rate across at least 3 requests."],
                Category = "Reliability",
                IconClass = "bi-stopwatch-fill",
                BadgeColorClass = "bg-info text-dark",
                GradientClass = "gradient-cyan",
                IsEarned = isQuickResponder,
                ProgressPercentage = totalRequests >= 3 ? Math.Min(100, (int)Math.Round(fulfillmentRate)) : Math.Min(100, totalRequests * 30),
                ProgressText = totalRequests >= 3 ? $"{fulfillmentRate:F0}% {_localizer["Fulfillment"]}" : $"{totalRequests}/3 {_localizer["Requests"]}",
                DisplayOrder = 6
            });

            // 7. Machinery Master (Maintenance Logger)
            var isMachineryMaster = maintenanceCount >= 1;
            badges.Add(new OwnerBadgeViewModel
            {
                Code = "MACHINERY_MASTER",
                Title = _localizer["Machinery Master"],
                Description = _localizer["Maintains peak equipment health with verified service records."],
                CriteriaDescription = _localizer["Log at least 1 verified maintenance service event."],
                Category = "Maintenance",
                IconClass = "bi-tools",
                BadgeColorClass = "bg-secondary text-white",
                GradientClass = "gradient-slate",
                IsEarned = isMachineryMaster,
                CurrentValue = maintenanceCount,
                TargetValue = 1,
                ProgressPercentage = isMachineryMaster ? 100 : 0,
                ProgressText = $"{maintenanceCount} {_localizer["Service Records"]}",
                DisplayOrder = 7
            });

            // 8. Rising Star (New host <= 90 days with good ratings)
            var isRisingStar = registrationDays <= 90 && totalCompletedBookings >= 2 && avgRating >= 4.5;
            badges.Add(new OwnerBadgeViewModel
            {
                Code = "RISING_STAR",
                Title = _localizer["Rising Star"],
                Description = _localizer["Newly registered host achieving outstanding farmer satisfaction within 90 days."],
                CriteriaDescription = _localizer["Joined within 90 days with 2+ bookings and 4.5+ star rating."],
                Category = "Special",
                IconClass = "bi-rocket-takeoff-fill",
                BadgeColorClass = "bg-danger text-white",
                GradientClass = "gradient-rose",
                IsEarned = isRisingStar,
                ProgressPercentage = registrationDays <= 90
                    ? Math.Min(100, (int)Math.Round((double)totalCompletedBookings / 2 * 50 + (avgRating >= 4.5 ? 50 : 25)))
                    : 0,
                ProgressText = registrationDays <= 90 ? $"{totalCompletedBookings}/2 {_localizer["Bookings"]} • {avgRating:F1}★" : _localizer["Expired (>90d)"],
                DisplayOrder = 8
            });

            return badges.OrderBy(b => b.DisplayOrder).ToList();
        }

        public async Task<OwnerBadgeDashboardWidgetViewModel> GetOwnerBadgeWidgetAsync(string ownerId, int? rank = null, int totalRanked = 0)
        {
            var badges = await GetOwnerBadgesAsync(ownerId);
            var earned = badges.Where(b => b.IsEarned).ToList();
            var unearned = badges.Where(b => !b.IsEarned).OrderByDescending(b => b.ProgressPercentage).ToList();

            var nextBadge = unearned.FirstOrDefault();

            return new OwnerBadgeDashboardWidgetViewModel
            {
                UserId = ownerId,
                CurrentRank = rank,
                TotalRankedOwners = totalRanked,
                EarnedBadges = earned,
                UnearnedBadges = unearned,
                AllBadges = badges,
                NextBadgeToUnlock = nextBadge
            };
        }
    }
}
