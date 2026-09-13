using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KrishiLink.BLL.Services
{
    public interface ISavedSearchService
    {
        Task<(bool Success, string? Error, int? Id)> CreateAsync(string userId, SavedSearchInput input);
        Task<SavedSearchListViewModel> GetAsync(string userId);
        Task<bool> ToggleAlertsAsync(string userId, int id);
        Task<bool> DeleteAsync(string userId, int id);
        Task<int> RunAlertsAsync(CancellationToken cancellationToken = default, string? onlyUserId = null);
        Task EvaluateForListingAsync(string listingType, int listingId);
        string BuildBrowseUrl(SavedSearch search);
    }

    public class SavedSearchService : ISavedSearchService
    {
        private const int MaxSavedSearchesPerUser = 10;
        private const int MaxAlertsPerSearchPerRun = 5;

        private readonly IRepository<SavedSearch> _savedSearches;
        private readonly IRepository<ApplicationUser> _users;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEquipmentService _equipmentService;
        private readonly IGodownService _godownService;
        private readonly INotificationService _notifications;
        private readonly ILogger<SavedSearchService> _logger;

        public SavedSearchService(
            IRepository<SavedSearch> savedSearches,
            IRepository<ApplicationUser> users,
            UserManager<ApplicationUser> userManager,
            IEquipmentService equipmentService,
            IGodownService godownService,
            INotificationService notifications,
            ILogger<SavedSearchService> logger)
        {
            _savedSearches = savedSearches;
            _users = users;
            _userManager = userManager;
            _equipmentService = equipmentService;
            _godownService = godownService;
            _notifications = notifications;
            _logger = logger;
        }

        public async Task<(bool Success, string? Error, int? Id)> CreateAsync(string userId, SavedSearchInput input)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return (false, "User is required.", null);

            var count = await _savedSearches.Query().CountAsync(s => s.UserId == userId);
            if (count >= MaxSavedSearchesPerUser)
            {
                return (false, $"You cannot have more than {MaxSavedSearchesPerUser} saved searches.", null);
            }

            var normType = NormalizeListingType(input.ListingType) ?? ListingTypes.Equipment;

            var entity = new SavedSearch
            {
                UserId = userId,
                Name = input.Name.Trim(),
                ListingType = normType,
                SearchTerm = string.IsNullOrWhiteSpace(input.SearchTerm) ? null : input.SearchTerm.Trim(),
                Category = string.IsNullOrWhiteSpace(input.Category) ? null : input.Category.Trim(),
                District = string.IsNullOrWhiteSpace(input.District) ? null : input.District.Trim(),
                MaxRate = input.MaxRate > 0 ? input.MaxRate : null,
                MinCapacityTons = input.MinCapacityTons > 0 ? input.MinCapacityTons : null,
                From = input.From,
                To = input.To,
                AlertsEnabled = input.AlertsEnabled,
                KnownListingIds = string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            await _savedSearches.AddAsync(entity);
            await _savedSearches.SaveChangesAsync();

            _logger.LogInformation("Saved search {Id} ('{Name}') created by user {UserId}", entity.Id, entity.Name, userId);
            return (true, null, entity.Id);
        }

        public async Task<SavedSearchListViewModel> GetAsync(string userId)
        {
            var model = new SavedSearchListViewModel();
            if (string.IsNullOrWhiteSpace(userId)) return model;

            var searches = await _savedSearches.Query()
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            model.Searches = searches.Select(s =>
            {
                var knownCount = string.IsNullOrWhiteSpace(s.KnownListingIds)
                    ? 0
                    : s.KnownListingIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Length;

                return new SavedSearchItemViewModel
                {
                    Id = s.Id,
                    Name = s.Name,
                    ListingType = s.ListingType,
                    SearchTerm = s.SearchTerm,
                    Category = s.Category,
                    District = s.District,
                    MaxRate = s.MaxRate,
                    MinCapacityTons = s.MinCapacityTons,
                    From = s.From,
                    To = s.To,
                    AlertsEnabled = s.AlertsEnabled,
                    LastRunAt = s.LastRunAt,
                    LastAlertedAt = s.LastAlertedAt,
                    MatchedCount = knownCount,
                    BrowseUrl = BuildBrowseUrl(s),
                    CreatedAt = s.CreatedAt
                };
            }).ToList();

            return model;
        }

        public async Task<bool> ToggleAlertsAsync(string userId, int id)
        {
            var search = await _savedSearches.QueryTracked()
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (search == null) return false;

            search.AlertsEnabled = !search.AlertsEnabled;
            await _savedSearches.SaveChangesAsync();
            _logger.LogInformation("Toggled alerts for saved search {Id} to {AlertsEnabled} for user {UserId}", id, search.AlertsEnabled, userId);
            return true;
        }

        public async Task<bool> DeleteAsync(string userId, int id)
        {
            var search = await _savedSearches.QueryTracked()
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (search == null) return false;

            _savedSearches.Remove(search);
            await _savedSearches.SaveChangesAsync();
            _logger.LogInformation("Deleted saved search {Id} for user {UserId}", id, userId);
            return true;
        }

        public async Task<int> RunAlertsAsync(CancellationToken cancellationToken = default, string? onlyUserId = null)
        {
            var query = _savedSearches.QueryTracked().Where(s => s.AlertsEnabled);
            if (!string.IsNullOrWhiteSpace(onlyUserId))
            {
                query = query.Where(s => s.UserId == onlyUserId);
            }

            var searches = await query.ToListAsync(cancellationToken);
            var totalAlertsSent = 0;

            foreach (var search in searches)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var user = await _userManager.FindByIdAsync(search.UserId);
                    if (user == null) continue;

                    var isFarmer = await _userManager.IsInRoleAsync(user, AppRoles.Farmer);
                    if (!isFarmer) continue;

                    // Expiration check: if To is in the past, disable alerts and notify user once
                    if (search.To.HasValue && search.To.Value.Date < DateTime.Today)
                    {
                        search.AlertsEnabled = false;
                        await _savedSearches.SaveChangesAsync();

                        await _notifications.NotifyAsync(new NotificationRequest
                        {
                            UserId = search.UserId,
                            Type = NotificationTypes.System,
                            TitleKey = "Saved search expired",
                            MessageKey = "Your saved search {0} expired — its dates have passed.",
                            Args = new object[] { search.Name },
                            LinkUrl = AppLinks.SavedSearches,
                            DedupeKey = $"savedsearch:{search.Id}:expired",
                            SendEmail = true,
                            RecipientEmail = user.Email
                        });

                        _logger.LogInformation("Disabled expired saved search {Id} ('{Name}') for user {UserId}", search.Id, search.Name, search.UserId);
                        continue;
                    }

                    var sent = await EvaluateSearchAlertsAsync(search, user, cancellationToken);
                    totalAlertsSent += sent;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error evaluating saved search alerts for search {SearchId}", search.Id);
                }
            }

            return totalAlertsSent;
        }

        public async Task EvaluateForListingAsync(string listingType, int listingId)
        {
            var normType = NormalizeListingType(listingType);
            if (normType == null) return;

            try
            {
                var candidateSearches = await _savedSearches.QueryTracked()
                    .Where(s => s.AlertsEnabled && s.ListingType == normType)
                    .ToListAsync();

                if (!candidateSearches.Any()) return;

                foreach (var search in candidateSearches)
                {
                    var user = await _userManager.FindByIdAsync(search.UserId);
                    if (user == null) continue;

                    // If search has expired, skip
                    if (search.To.HasValue && search.To.Value.Date < DateTime.Today) continue;

                    var knownSet = ParseKnownIds(search.KnownListingIds);
                    if (knownSet.Contains(listingId)) continue;

                    // Run browse evaluation to ensure availability semantics match exactly
                    var (matchedIds, items) = await GetMatchingListingsAsync(search);
                    if (matchedIds.Contains(listingId))
                    {
                        var item = items.FirstOrDefault(x => x.Id == listingId);
                        if (item != null)
                        {
                            var windowText = (search.From.HasValue && search.To.HasValue)
                                ? $" and is free {search.From.Value:dd MMM} – {search.To.Value:dd MMM}"
                                : string.Empty;

                            var linkUrl = normType == ListingTypes.Equipment
                                ? AppLinks.EquipmentDetails(listingId)
                                : AppLinks.GodownDetails(listingId);

                            await _notifications.NotifyAsync(new NotificationRequest
                            {
                                UserId = search.UserId,
                                Type = NotificationTypes.SavedSearchAlert,
                                TitleKey = "New match for your saved search",
                                MessageKey = "{0} in {1} matches \"{2}\"{3}.",
                                Args = new object[] { item.Name, item.District ?? item.Location, search.Name, windowText },
                                LinkUrl = linkUrl,
                                DedupeKey = $"savedsearch:{search.Id}:{normType.ToLowerInvariant()}:{listingId}",
                                SendEmail = true,
                                RecipientEmail = user.Email
                            });

                            knownSet.Add(listingId);
                            search.KnownListingIds = string.Join(",", knownSet);
                            search.LastRunAt = DateTime.UtcNow;
                            search.LastAlertedAt = DateTime.UtcNow;
                            await _savedSearches.SaveChangesAsync();

                            _logger.LogInformation("Immediate alert sent for search {SearchId} on new {ListingType} listing {ListingId}",
                                search.Id, normType, listingId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate saved search alerts for listing {ListingType}:{ListingId}", listingType, listingId);
            }
        }

        public string BuildBrowseUrl(SavedSearch search) => AppLinks.BrowseWith(search);

        private async Task<int> EvaluateSearchAlertsAsync(SavedSearch search, ApplicationUser user, CancellationToken cancellationToken)
        {
            var (matchedIds, items) = await GetMatchingListingsAsync(search);
            var knownSet = ParseKnownIds(search.KnownListingIds);

            var newIds = matchedIds.Where(id => !knownSet.Contains(id)).ToList();
            var toNotify = newIds.Take(MaxAlertsPerSearchPerRun).ToList();
            var normType = NormalizeListingType(search.ListingType) ?? ListingTypes.Equipment;

            var alertsSent = 0;
            foreach (var id in toNotify)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var item = items.FirstOrDefault(x => x.Id == id);
                if (item == null) continue;

                var windowText = (search.From.HasValue && search.To.HasValue)
                    ? $" and is free {search.From.Value:dd MMM} – {search.To.Value:dd MMM}"
                    : string.Empty;

                var linkUrl = normType == ListingTypes.Equipment
                    ? AppLinks.EquipmentDetails(id)
                    : AppLinks.GodownDetails(id);

                await _notifications.NotifyAsync(new NotificationRequest
                {
                    UserId = search.UserId,
                    Type = NotificationTypes.SavedSearchAlert,
                    TitleKey = "New match for your saved search",
                    MessageKey = "{0} in {1} matches \"{2}\"{3}.",
                    Args = new object[] { item.Name, item.District ?? item.Location, search.Name, windowText },
                    LinkUrl = linkUrl,
                    DedupeKey = $"savedsearch:{search.Id}:{normType.ToLowerInvariant()}:{id}",
                    SendEmail = true,
                    RecipientEmail = user.Email
                });

                alertsSent++;
            }

            // Update KnownListingIds:
            // Keep existing known IDs that are still matching, plus the newly notified IDs.
            // Items that are no longer matching drop out so they can trigger again if re-available.
            // Items in newIds beyond the cap are not added to knownSet so they are processed in the next run.
            var retainedKnown = knownSet.Intersect(matchedIds).ToHashSet();
            foreach (var id in toNotify)
            {
                retainedKnown.Add(id);
            }

            search.KnownListingIds = string.Join(",", retainedKnown);
            search.LastRunAt = DateTime.UtcNow;
            if (alertsSent > 0)
            {
                search.LastAlertedAt = DateTime.UtcNow;
            }

            await _savedSearches.SaveChangesAsync();
            return alertsSent;
        }

        private async Task<(List<int> MatchedIds, List<ListingMatchItem> Items)> GetMatchingListingsAsync(SavedSearch search)
        {
            var isEquipment = search.ListingType == ListingTypes.Equipment;
            var effectiveFrom = search.From.HasValue && search.From.Value.Date < DateTime.Today
                ? DateTime.Today
                : search.From;

            if (isEquipment)
            {
                var criteria = new EquipmentSearchCriteria
                {
                    SearchTerm = search.SearchTerm,
                    District = search.District,
                    SelectedMaxPrice = search.MaxRate,
                    StartDate = effectiveFrom,
                    EndDate = search.To,
                    Page = 1,
                    PageSize = 50
                };

                if (!string.IsNullOrWhiteSpace(search.Category))
                {
                    criteria.SelectedCategories = new List<string> { search.Category };
                }

                var results = await _equipmentService.BrowseAsync(criteria);
                var matched = results.EquipmentList.Select(e => e.Id).ToList();
                var items = results.EquipmentList.Select(e => new ListingMatchItem
                {
                    Id = e.Id,
                    Name = e.Name,
                    Location = e.Location,
                    District = e.District
                }).ToList();

                return (matched, items);
            }
            else
            {
                var criteria = new GodownSearchCriteria
                {
                    SearchTerm = search.SearchTerm,
                    District = search.District,
                    SelectedMaxPrice = search.MaxRate,
                    SelectedMinCapacity = search.MinCapacityTons,
                    AvailableStartDate = effectiveFrom,
                    AvailableEndDate = search.To,
                    Page = 1,
                    PageSize = 50
                };

                if (!string.IsNullOrWhiteSpace(search.Category))
                {
                    criteria.SelectedStorageTypes = new List<string> { search.Category };
                }

                var results = await _godownService.BrowseAsync(criteria);
                var matched = results.GodownList.Select(g => g.Id).ToList();
                var items = results.GodownList.Select(g => new ListingMatchItem
                {
                    Id = g.Id,
                    Name = g.Name,
                    Location = g.Location,
                    District = g.District
                }).ToList();

                return (matched, items);
            }
        }

        private static HashSet<int> ParseKnownIds(string? knownListingIds)
        {
            if (string.IsNullOrWhiteSpace(knownListingIds)) return new HashSet<int>();
            return knownListingIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var id) ? (int?)id : null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();
        }

        private static string? NormalizeListingType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type)) return null;
            if (type.Equals("Equipment", StringComparison.OrdinalIgnoreCase)) return ListingTypes.Equipment;
            if (type.Equals("Godown", StringComparison.OrdinalIgnoreCase)) return ListingTypes.Godown;
            return null;
        }

        private class ListingMatchItem
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Location { get; set; } = string.Empty;
            public string? District { get; set; }
        }
    }
}
