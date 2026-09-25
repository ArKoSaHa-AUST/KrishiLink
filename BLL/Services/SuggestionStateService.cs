using System.Globalization;
using System.Text.RegularExpressions;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.BLL.Services
{
    /// <summary>
    /// Orders weather nudges by urgency = stage criticality × how soon the weather event arrives (ADV-08), so
    /// "harvest before Thursday's rain" outranks a general tip. Pure and deterministic.
    /// </summary>
    public static class SuggestionUrgency
    {
        // How much it costs to miss this kind of nudge.
        private static readonly Dictionary<string, int> Criticality = new(StringComparer.Ordinal)
        {
            ["FastHarvesting"] = 5,
            ["StorageProtection"] = 4,
            ["DiseasePrevention"] = 4,
            ["TillageIrrigation"] = 2,
            ["EquipmentRental"] = 2
        };

        // Nudges that race a rain forecast.
        private static readonly HashSet<string> RainDriven = new(StringComparer.Ordinal) { "FastHarvesting", "StorageProtection", "DiseasePrevention" };

        public static int Score(WeatherSuggestionItem item, string cropStage, bool rainAlert, int rainWindowDays)
        {
            var criticality = Criticality.GetValueOrDefault(item.NudgeType, 1) * (item.IsCritical ? 2 : 1);
            if (cropStage == "Harvesting" && item.NudgeType is "FastHarvesting" or "StorageProtection") criticality += 2;
            // Rain tomorrow multiplies by 5, rain in five days or more by 1.
            var timeFactor = rainAlert && RainDriven.Contains(item.NudgeType) ? Math.Clamp(6 - Math.Max(1, rainWindowDays), 1, 5) : 1;
            return criticality * timeFactor;
        }

        public static List<WeatherSuggestionItem> Rank(IEnumerable<WeatherSuggestionItem> items, string cropStage, bool rainAlert, int rainWindowDays,
            string district, string crop, DateTime today)
        {
            var month = today.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            foreach (var item in items)
            {
                item.Urgency = Score(item, cropStage, rainAlert, rainWindowDays);
                item.DaysUntilWeatherEvent = rainAlert && RainDriven.Contains(item.NudgeType) ? Math.Max(1, rainWindowDays) : null;
                item.StateKey = SuggestionStateService.KeyFor(item.Id, district, crop, month);
            }
            return items.OrderByDescending(i => i.Urgency).ThenBy(i => i.Priority).ThenBy(i => i.Id, StringComparer.Ordinal).ToList();
        }
    }

    public interface ISuggestionStateService
    {
        /// <summary>Removes the nudges this user marked done, or snoozed within the snooze window, and counts them.</summary>
        Task ApplyAsync(string userId, WeatherSuggestionViewModel suggestion, CancellationToken cancellationToken = default);

        /// <summary>Records done / snoozed, or clears the state with <see cref="SuggestionStateKind.New"/>. False for a malformed key.</summary>
        Task<bool> SetAsync(string userId, string key, SuggestionStateKind state, CancellationToken cancellationToken = default);
    }

    public sealed class SuggestionStateService : ISuggestionStateService
    {
        public static readonly TimeSpan SnoozeFor = TimeSpan.FromDays(3);
        private static readonly Regex KeyShape = new(@"^[a-z0-9-]{1,40}\|[^|]{1,40}\|[^|]{1,60}\|\d{4}-\d{2}$", RegexOptions.CultureInvariant);

        private readonly ApplicationDbContext _db;

        public SuggestionStateService(ApplicationDbContext db) => _db = db;

        /// <summary>Month-scoped, so a nudge marked done in September can return next season.</summary>
        public static string KeyFor(string suggestionId, string district, string crop, string month) =>
            $"{suggestionId}|{district.Replace('|', ' ')}|{crop.Replace('|', ' ')}|{month}";

        public async Task ApplyAsync(string userId, WeatherSuggestionViewModel suggestion, CancellationToken cancellationToken = default)
        {
            var keys = suggestion.Suggestions.Select(s => s.StateKey).Where(k => k.Length > 0).ToList();
            if (keys.Count == 0) return;

            var snoozeCutoff = DateTime.UtcNow - SnoozeFor;
            var hidden = await _db.SuggestionStates.AsNoTracking()
                .Where(s => s.UserId == userId && keys.Contains(s.SuggestionKey)
                    && (s.State == SuggestionStateKind.Done || (s.State == SuggestionStateKind.Snoozed && s.UpdatedAt > snoozeCutoff)))
                .Select(s => s.SuggestionKey)
                .ToListAsync(cancellationToken);

            suggestion.HiddenCount = suggestion.Suggestions.RemoveAll(s => hidden.Contains(s.StateKey));
        }

        public async Task<bool> SetAsync(string userId, string key, SuggestionStateKind state, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Length > 160 || !KeyShape.IsMatch(key)) return false;

            var existing = await _db.SuggestionStates.FirstOrDefaultAsync(s => s.UserId == userId && s.SuggestionKey == key, cancellationToken);
            if (state == SuggestionStateKind.New)
            {
                if (existing is not null) _db.SuggestionStates.Remove(existing);
            }
            else if (existing is null)
            {
                _db.SuggestionStates.Add(new SuggestionState { UserId = userId, SuggestionKey = key, State = state });
            }
            else
            {
                existing.State = state;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
