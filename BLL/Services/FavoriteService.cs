using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KrishiLink.BLL.Helpers;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KrishiLink.BLL.Services
{
    public interface IFavoriteService
    {
        Task<(bool IsFavorite, int Count, string? Error)> ToggleAsync(string userId, string listingType, int listingId);
        Task<FavoritesViewModel> GetAsync(string userId);
        Task<HashSet<int>> GetIdsAsync(string userId, string listingType);
        Task<int> CountAsync(string userId);
    }

    public class FavoriteService : IFavoriteService
    {
        private const int MaxFavoritesPerUser = 100;
        private readonly IRepository<Favorite> _favorites;
        private readonly IRepository<Equipment> _equipment;
        private readonly IRepository<Godown> _godowns;
        private readonly IRepository<EquipmentBooking> _equipmentBookings;
        private readonly IRepository<GodownBooking> _godownBookings;
        private readonly IRepository<EquipmentBlockedDate> _equipmentBlockedDates;
        private readonly IRepository<GodownBlockedDate> _godownBlockedDates;
        private readonly ILogger<FavoriteService> _logger;

        public FavoriteService(
            IRepository<Favorite> favorites,
            IRepository<Equipment> equipment,
            IRepository<Godown> godowns,
            IRepository<EquipmentBooking> equipmentBookings,
            IRepository<GodownBooking> godownBookings,
            IRepository<EquipmentBlockedDate> equipmentBlockedDates,
            IRepository<GodownBlockedDate> godownBlockedDates,
            ILogger<FavoriteService> logger)
        {
            _favorites = favorites;
            _equipment = equipment;
            _godowns = godowns;
            _equipmentBookings = equipmentBookings;
            _godownBookings = godownBookings;
            _equipmentBlockedDates = equipmentBlockedDates;
            _godownBlockedDates = godownBlockedDates;
            _logger = logger;
        }

        public async Task<(bool IsFavorite, int Count, string? Error)> ToggleAsync(string userId, string listingType, int listingId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return (false, 0, "User is required.");

            var normType = NormalizeListingType(listingType);
            if (normType == null)
                return (false, 0, "Invalid listing type.");

            // Verify listing exists before favoriting
            if (normType == ListingTypes.Equipment)
            {
                var exists = await _equipment.Query().AnyAsync(e => e.Id == listingId);
                if (!exists) return (false, 0, "Equipment listing does not exist.");
            }
            else
            {
                var exists = await _godowns.Query().AnyAsync(g => g.Id == listingId);
                if (!exists) return (false, 0, "Godown facility does not exist.");
            }

            var existing = await _favorites.QueryTracked()
                .FirstOrDefaultAsync(f => f.UserId == userId && f.ListingType == normType && f.ListingId == listingId);

            if (existing != null)
            {
                _favorites.Remove(existing);
                await _favorites.SaveChangesAsync();
                var countAfterRemove = await CountAsync(userId);
                _logger.LogInformation("Removed favorite {ListingType}:{ListingId} for user {UserId}", normType, listingId, userId);
                return (false, countAfterRemove, null);
            }

            var currentCount = await CountAsync(userId);
            if (currentCount >= MaxFavoritesPerUser)
            {
                return (false, currentCount, $"You cannot have more than {MaxFavoritesPerUser} favorites.");
            }

            var newFav = new Favorite
            {
                UserId = userId,
                ListingType = normType,
                ListingId = listingId,
                CreatedAt = DateTime.UtcNow
            };

            await _favorites.AddAsync(newFav);

            try
            {
                await _favorites.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
            {
                // Unique index violation on race condition: treat as successfully favorited
                _logger.LogWarning("Race condition: Favorite {ListingType}:{ListingId} already existed for user {UserId}", normType, listingId, userId);
                return (true, currentCount, null);
            }

            var countAfterAdd = await CountAsync(userId);
            _logger.LogInformation("Added favorite {ListingType}:{ListingId} for user {UserId}", normType, listingId, userId);
            return (true, countAfterAdd, null);
        }

        public async Task<FavoritesViewModel> GetAsync(string userId)
        {
            var model = new FavoritesViewModel();
            if (string.IsNullOrWhiteSpace(userId)) return model;

            var allFavs = await _favorites.QueryTracked()
                .Where(f => f.UserId == userId)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            if (!allFavs.Any()) return model;

            var today = DateTime.Today;
            var windowEnd = today.AddDays(30);

            // 1. Process Equipment Favorites
            var eqFavs = allFavs.Where(f => f.ListingType == ListingTypes.Equipment).ToList();
            if (eqFavs.Any())
            {
                var eqIds = eqFavs.Select(f => f.ListingId).Distinct().ToList();
                var equipments = await _equipment.Query()
                    .Include(e => e.Owner)
                    .Where(e => eqIds.Contains(e.Id))
                    .ToDictionaryAsync(e => e.Id);

                var orphanedEq = eqFavs.Where(f => !equipments.ContainsKey(f.ListingId)).ToList();
                if (orphanedEq.Any())
                {
                    _favorites.RemoveRange(orphanedEq);
                    await _favorites.SaveChangesAsync();
                    eqFavs = eqFavs.Except(orphanedEq).ToList();
                }

                if (eqFavs.Any())
                {
                    var validEqIds = eqFavs.Select(f => f.ListingId).Distinct().ToList();
                    var activeBookings = await _equipmentBookings.Query()
                        .Where(b => validEqIds.Contains(b.EquipmentId) && BookingStatus.Confirmed.Contains(b.Status) && b.EndDate >= today && b.StartDate <= windowEnd)
                        .Select(b => new { b.EquipmentId, b.StartDate, b.EndDate, b.Units })
                        .ToListAsync();

                    var blockedDates = await _equipmentBlockedDates.Query()
                        .Where(d => validEqIds.Contains(d.EquipmentId) && d.Date >= today && d.Date <= windowEnd)
                        .Select(d => new { d.EquipmentId, d.Date })
                        .ToListAsync();

                    foreach (var fav in eqFavs)
                    {
                        var eq = equipments[fav.ListingId];
                        var eqBookings = activeBookings.Where(b => b.EquipmentId == eq.Id).ToList();
                        var eqBlocked = blockedDates.Where(d => d.EquipmentId == eq.Id).Select(d => d.Date.Date).ToHashSet();

                        string hint;
                        bool isAvailableNow = eq.IsAvailable;
                        if (!eq.IsAvailable)
                        {
                            hint = "Unavailable";
                        }
                        else
                        {
                            var todayBlocked = eqBlocked.Contains(today);
                            var todayUnitsBooked = eqBookings.Where(b => b.StartDate <= today && today <= b.EndDate).Sum(b => b.Units);
                            if (!todayBlocked && todayUnitsBooked < eq.Quantity)
                            {
                                hint = "Available today";
                            }
                            else
                            {
                                // Find next free date within 30 days
                                DateTime? nextFree = null;
                                for (var d = today.AddDays(1); d <= windowEnd; d = d.AddDays(1))
                                {
                                    if (eqBlocked.Contains(d)) continue;
                                    var bookedOnDay = eqBookings.Where(b => b.StartDate <= d && d <= b.EndDate).Sum(b => b.Units);
                                    if (bookedOnDay < eq.Quantity)
                                    {
                                        nextFree = d;
                                        break;
                                    }
                                }

                                hint = nextFree.HasValue
                                    ? $"Free from {nextFree.Value:dd MMM}"
                                    : "Check availability";
                                isAvailableNow = false;
                            }
                        }

                        model.EquipmentFavorites.Add(new FavoriteItemViewModel
                        {
                            FavoriteId = fav.Id,
                            ListingId = eq.Id,
                            ListingType = ListingTypes.Equipment,
                            Name = eq.Name,
                            Category = eq.Category,
                            Location = eq.Location,
                            District = eq.District,
                            ImageUrl = ListingFormat.Split(eq.ImageUrls).FirstOrDefault() ?? string.Empty,
                            PriceFormatted = $"৳{eq.DailyRate:N0} / day",
                            Status = isAvailableNow ? "Available" : "Booked",
                            DetailUrl = AppLinks.EquipmentDetails(eq.Id),
                            AvailabilityHint = hint,
                            IsAvailable = isAvailableNow,
                            Rating = eq.AverageRating,
                            ReviewCount = eq.ReviewCount,
                            OwnerRating = eq.Owner?.OwnerAverageRating ?? 0,
                            OwnerReviewCount = eq.Owner?.OwnerReviewCount ?? 0,
                            AddedAt = fav.CreatedAt
                        });
                    }
                }
            }

            // 2. Process Godown Favorites
            var gdFavs = allFavs.Where(f => f.ListingType == ListingTypes.Godown).ToList();
            if (gdFavs.Any())
            {
                var gdIds = gdFavs.Select(f => f.ListingId).Distinct().ToList();
                var godowns = await _godowns.Query()
                    .Include(g => g.Owner)
                    .Where(g => gdIds.Contains(g.Id))
                    .ToDictionaryAsync(g => g.Id);

                var orphanedGd = gdFavs.Where(f => !godowns.ContainsKey(f.ListingId)).ToList();
                if (orphanedGd.Any())
                {
                    _favorites.RemoveRange(orphanedGd);
                    await _favorites.SaveChangesAsync();
                    gdFavs = gdFavs.Except(orphanedGd).ToList();
                }

                if (gdFavs.Any())
                {
                    var validGdIds = gdFavs.Select(f => f.ListingId).Distinct().ToList();
                    var activeGdBookings = await _godownBookings.Query()
                        .Where(b => validGdIds.Contains(b.GodownId) && BookingStatus.Confirmed.Contains(b.Status) && b.EndDate >= today && b.StartDate <= windowEnd)
                        .Select(b => new { b.GodownId, b.StartDate, b.EndDate, b.StorageTons })
                        .ToListAsync();

                    var gdBlockedDates = await _godownBlockedDates.Query()
                        .Where(d => validGdIds.Contains(d.GodownId) && d.Date >= today && d.Date <= windowEnd)
                        .Select(d => new { d.GodownId, d.Date })
                        .ToListAsync();

                    foreach (var fav in gdFavs)
                    {
                        var gd = godowns[fav.ListingId];
                        var bookings = activeGdBookings.Where(b => b.GodownId == gd.Id).ToList();
                        var blocked = gdBlockedDates.Where(d => d.GodownId == gd.Id).Select(d => d.Date.Date).ToHashSet();

                        string hint;
                        bool isAvailableNow = gd.IsActive;
                        if (!gd.IsActive)
                        {
                            hint = "Unavailable";
                        }
                        else
                        {
                            var todayBlocked = blocked.Contains(today);
                            var todayTonsBooked = bookings.Where(b => b.StartDate <= today && today <= b.EndDate).Sum(b => b.StorageTons);
                            var availableTonsToday = Math.Max(0, gd.CapacityInTons - todayTonsBooked);

                            if (!todayBlocked && availableTonsToday > 0)
                            {
                                hint = $"{availableTonsToday:0.#} tons available today";
                            }
                            else
                            {
                                DateTime? nextFree = null;
                                for (var d = today.AddDays(1); d <= windowEnd; d = d.AddDays(1))
                                {
                                    if (blocked.Contains(d)) continue;
                                    var tonsOnDay = bookings.Where(b => b.StartDate <= d && d <= b.EndDate).Sum(b => b.StorageTons);
                                    if (tonsOnDay < gd.CapacityInTons)
                                    {
                                        nextFree = d;
                                        break;
                                    }
                                }

                                hint = nextFree.HasValue
                                    ? $"Free from {nextFree.Value:dd MMM}"
                                    : "Fully Booked";
                                isAvailableNow = false;
                            }
                        }

                        model.GodownFavorites.Add(new FavoriteItemViewModel
                        {
                            FavoriteId = fav.Id,
                            ListingId = gd.Id,
                            ListingType = ListingTypes.Godown,
                            Name = gd.Name,
                            Category = gd.StorageType,
                            Location = gd.Location,
                            District = gd.District,
                            ImageUrl = ListingFormat.Split(gd.ImageUrls).FirstOrDefault() ?? string.Empty,
                            PriceFormatted = $"৳{gd.PricePerTonPerMonth:N0} / ton / mo",
                            Status = isAvailableNow ? "Available" : "Fully Booked",
                            DetailUrl = AppLinks.GodownDetails(gd.Id),
                            AvailabilityHint = hint,
                            IsAvailable = isAvailableNow,
                            Rating = gd.AverageRating,
                            ReviewCount = gd.ReviewCount,
                            OwnerRating = gd.Owner?.OwnerAverageRating ?? 0,
                            OwnerReviewCount = gd.Owner?.OwnerReviewCount ?? 0,
                            AddedAt = fav.CreatedAt
                        });
                    }
                }
            }

            return model;
        }

        public async Task<HashSet<int>> GetIdsAsync(string userId, string listingType)
        {
            if (string.IsNullOrWhiteSpace(userId)) return new HashSet<int>();
            var normType = NormalizeListingType(listingType);
            if (normType == null) return new HashSet<int>();

            var ids = await _favorites.Query()
                .Where(f => f.UserId == userId && f.ListingType == normType)
                .Select(f => f.ListingId)
                .ToListAsync();

            return ids.ToHashSet();
        }

        public async Task<int> CountAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return 0;
            return await _favorites.Query().CountAsync(f => f.UserId == userId);
        }

        private static string? NormalizeListingType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type)) return null;
            if (type.Equals("Equipment", StringComparison.OrdinalIgnoreCase)) return ListingTypes.Equipment;
            if (type.Equals("Godown", StringComparison.OrdinalIgnoreCase)) return ListingTypes.Godown;
            return null;
        }
    }
}
