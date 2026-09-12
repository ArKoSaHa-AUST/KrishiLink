using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace KrishiLink.BLL.Services
{
    public interface ILeaderboardService
    {
        Task<LeaderboardPageViewModel> GetLeaderboardAsync(string category = "All", string sortBy = "trust", string? district = null, bool forceRefresh = false);
        Task<(int? Rank, double TrustScore, int TotalRanked)> GetOwnerRankAsync(string ownerId);
        void InvalidateCache();
    }

    public class LeaderboardService : ILeaderboardService
    {
        private const string CacheKeyPrefix = "krishilink_leaderboard_";
        private static readonly ConcurrentBag<string> KnownCacheKeys = new();
        private static readonly TimeSpan CacheSlidingExpiration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan CacheAbsoluteExpiration = TimeSpan.FromMinutes(30);

        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly IBadgeService _badgeService;
        private readonly ILogger<LeaderboardService> _logger;

        public LeaderboardService(
            ApplicationDbContext db,
            IMemoryCache cache,
            IBadgeService badgeService,
            ILogger<LeaderboardService> logger)
        {
            _db = db;
            _cache = cache;
            _badgeService = badgeService;
            _logger = logger;
        }

        public async Task<LeaderboardPageViewModel> GetLeaderboardAsync(
            string category = "All",
            string sortBy = "trust",
            string? district = null,
            bool forceRefresh = false)
        {
            var cleanCategory = string.IsNullOrWhiteSpace(category) ? "All" : category.Trim();
            var cleanSortBy = string.IsNullOrWhiteSpace(sortBy) ? "trust" : sortBy.Trim().ToLowerInvariant();
            var cleanDistrict = string.IsNullOrWhiteSpace(district) || district.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? null
                : district.Trim();

            var cacheKey = $"{CacheKeyPrefix}{cleanCategory.ToLowerInvariant()}_{cleanSortBy}_{cleanDistrict?.ToLowerInvariant() ?? "all"}";

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out LeaderboardPageViewModel? cachedModel) && cachedModel != null)
            {
                _logger.LogInformation("Leaderboard served from MemoryCache (Key: {CacheKey})", cacheKey);
                cachedModel.IsCached = true;
                return cachedModel;
            }

            _logger.LogInformation("Recalculating Leaderboard (Key: {CacheKey}, Force: {Force})", cacheKey, forceRefresh);

            // 1. Fetch all owners (users with role EquipmentOwner / GodownOwner or who have listings)
            var ownerUsers = await _db.Users
                .Where(u => u.UserRole == "EquipmentOwner" || u.UserRole == "GodownOwner" ||
                            _db.Equipment.Any(e => e.OwnerId == u.Id) ||
                            _db.Godowns.Any(g => g.OwnerId == u.Id))
                .AsNoTracking()
                .ToListAsync();

            // 2. Fetch aggregated stats in bulk
            var eqBookings = await _db.EquipmentBookings
                .Where(b => b.Equipment != null)
                .Select(b => new { OwnerId = b.Equipment!.OwnerId, b.Status })
                .AsNoTracking()
                .ToListAsync();

            var gdBookings = await _db.GodownBookings
                .Where(b => b.Godown != null)
                .Select(b => new { OwnerId = b.Godown!.OwnerId, b.Status })
                .AsNoTracking()
                .ToListAsync();

            var eqListings = await _db.Equipment
                .Select(e => new { e.OwnerId, e.IsAvailable, e.AverageRating, e.ReviewCount })
                .AsNoTracking()
                .ToListAsync();

            var gdListings = await _db.Godowns
                .Select(g => new { g.OwnerId, g.IsActive, g.AverageRating, g.ReviewCount })
                .AsNoTracking()
                .ToListAsync();

            var maintenanceCounts = await _db.EquipmentMaintenanceRecords
                .Where(m => m.Equipment != null)
                .GroupBy(m => m.Equipment!.OwnerId)
                .Select(g => new { OwnerId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.OwnerId, x => x.Count);

            var entries = new List<OwnerLeaderboardEntryViewModel>();

            foreach (var user in ownerUsers)
            {
                var userEqBookings = eqBookings.Where(b => b.OwnerId == user.Id).ToList();
                var userGdBookings = gdBookings.Where(b => b.OwnerId == user.Id).ToList();

                var completedEq = userEqBookings.Count(b => b.Status == "Completed");
                var completedGd = userGdBookings.Count(b => b.Status == "Completed");
                var totalCompleted = completedEq + completedGd;

                var activeEq = userEqBookings.Count(b => b.Status == "Confirmed");
                var activeGd = userGdBookings.Count(b => b.Status == "Accepted");
                var totalActive = activeEq + activeGd;

                var userEqListings = eqListings.Where(e => e.OwnerId == user.Id).ToList();
                var userGdListings = gdListings.Where(g => g.OwnerId == user.Id).ToList();

                var activeListings = userEqListings.Count(e => e.IsAvailable) + userGdListings.Count(g => g.IsActive);

                var totalReviews = userEqListings.Sum(e => e.ReviewCount) + userGdListings.Sum(g => g.ReviewCount);
                var totalWeightedRating = userEqListings.Sum(e => e.AverageRating * e.ReviewCount) + userGdListings.Sum(g => g.AverageRating * g.ReviewCount);
                var avgRating = totalReviews > 0 ? Math.Round(totalWeightedRating / totalReviews, 1) : 0.0;

                maintenanceCounts.TryGetValue(user.Id, out var maintenanceCount);

                // Calculate KrishiLink Trust Score
                // Formula: (CompletedBookings * 10) + (AvgRating * 20) + (IsVerified ? 50 : 0) + (Reviews * 5) + (MaintenanceLogs * 5)
                var trustScore = Math.Round(
                    (totalCompleted * 10.0) +
                    (avgRating * 20.0) +
                    (user.IsVerified ? 50.0 : 0.0) +
                    (totalReviews * 5.0) +
                    (maintenanceCount * 5.0),
                    1
                );

                // Fetch badges
                var badges = await _badgeService.GetOwnerBadgesAsync(user.Id);
                var earnedBadges = badges.Where(b => b.IsEarned).ToList();

                var hasEquipment = userEqListings.Any() || user.UserRole == "EquipmentOwner";
                var hasGodown = userGdListings.Any() || user.UserRole == "GodownOwner";
                var effectiveRole = (hasEquipment && hasGodown) ? "Both" : (hasEquipment ? "EquipmentOwner" : "GodownOwner");

                entries.Add(new OwnerLeaderboardEntryViewModel
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    BusinessOrFarmName = user.BusinessOrFarmName ?? string.Empty,
                    District = user.District ?? string.Empty,
                    Location = user.Location ?? string.Empty,
                    UserRole = effectiveRole,
                    IsVerified = user.IsVerified,
                    VerificationStatus = user.VerificationStatus ?? "Unverified",
                    CompletedBookingsCount = totalCompleted,
                    ActiveBookingsCount = totalActive,
                    AverageRating = avgRating,
                    ReviewCount = totalReviews,
                    ActiveListingsCount = activeListings,
                    MaintenanceRecordsCount = maintenanceCount,
                    TrustScore = trustScore,
                    Badges = earnedBadges
                });
            }

            // Apply Category Filtering
            var filtered = entries.AsEnumerable();
            if (cleanCategory.Equals("Equipment", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(e => e.UserRole == "EquipmentOwner" || e.UserRole == "Both");
            }
            else if (cleanCategory.Equals("Godown", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(e => e.UserRole == "GodownOwner" || e.UserRole == "Both");
            }

            // Apply District Filtering
            if (!string.IsNullOrWhiteSpace(cleanDistrict))
            {
                filtered = filtered.Where(e =>
                    e.District.Equals(cleanDistrict, StringComparison.OrdinalIgnoreCase) ||
                    e.Location.IndexOf(cleanDistrict, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            // Apply Sorting
            filtered = cleanSortBy switch
            {
                "bookings" => filtered
                    .OrderByDescending(e => e.CompletedBookingsCount)
                    .ThenByDescending(e => e.TrustScore)
                    .ThenByDescending(e => e.AverageRating),
                "rating" => filtered
                    .OrderByDescending(e => e.AverageRating)
                    .ThenByDescending(e => e.ReviewCount)
                    .ThenByDescending(e => e.TrustScore),
                _ => filtered // "trust"
                    .OrderByDescending(e => e.TrustScore)
                    .ThenByDescending(e => e.CompletedBookingsCount)
                    .ThenByDescending(e => e.AverageRating)
            };

            var rankedEntries = filtered.ToList();

            // Assign ranks (1-indexed)
            for (int i = 0; i < rankedEntries.Count; i++)
            {
                rankedEntries[i].Rank = i + 1;
            }

            var topPodium = rankedEntries.Take(3).ToList();
            var remainingList = rankedEntries.Skip(3).ToList();

            var viewModel = new LeaderboardPageViewModel
            {
                Category = cleanCategory,
                SortBy = cleanSortBy,
                District = cleanDistrict,
                TopPodium = topPodium,
                RankedList = remainingList,
                AllEntries = rankedEntries,
                GeneratedAt = DateTime.UtcNow,
                IsCached = false
            };

            // Save in MemoryCache
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(CacheSlidingExpiration)
                .SetAbsoluteExpiration(CacheAbsoluteExpiration);

            _cache.Set(cacheKey, viewModel, cacheEntryOptions);
            KnownCacheKeys.Add(cacheKey);

            return viewModel;
        }

        public async Task<(int? Rank, double TrustScore, int TotalRanked)> GetOwnerRankAsync(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                return (null, 0.0, 0);

            var leaderboard = await GetLeaderboardAsync("All", "trust", null);
            var entry = leaderboard.AllEntries.FirstOrDefault(e => e.UserId == ownerId);

            if (entry != null)
                return (entry.Rank, entry.TrustScore, leaderboard.TotalOwnersCount);

            return (null, 0.0, leaderboard.TotalOwnersCount);
        }

        public void InvalidateCache()
        {
            _logger.LogInformation("Invalidating Leaderboard MemoryCache");
            while (KnownCacheKeys.TryTake(out var key))
            {
                _cache.Remove(key);
            }
        }
    }
}
