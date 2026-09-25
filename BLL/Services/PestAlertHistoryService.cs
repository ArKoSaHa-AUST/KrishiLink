using System.Text.Json;
using KrishiLink.BLL.Helpers;
using KrishiLink.DAL;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace KrishiLink.BLL.Services
{
    /// <summary>Maps a farmer's profile crop onto the pest-rule engine's crop filter.</summary>
    public static class PestAlertCrops
    {
        public static readonly IReadOnlyList<string> Filters = new[] { "All", "Rice", "Potato", "Wheat", "Maize", "Mustard", "Tomato", "Brinjal", "Chili" };

        /// <summary>"Rice (Boro)" → "Rice", "Oilseeds" → "Mustard", anything the rules do not cover → "All".</summary>
        public static string ForProfileCrop(string? crop)
        {
            if (string.IsNullOrWhiteSpace(crop)) return "All";
            var direct = Filters.FirstOrDefault(f => f.Equals(crop.Trim(), StringComparison.OrdinalIgnoreCase));
            if (direct is not null) return direct;
            if (crop.StartsWith("Rice", StringComparison.OrdinalIgnoreCase)) return "Rice";
            if (crop.Equals("Oilseeds", StringComparison.OrdinalIgnoreCase)) return "Mustard";
            return "All";
        }
    }

    public sealed record PestAlertDay(DateTime Day, string Severity);

    public sealed class PestAlertHistoryViewModel
    {
        /// <summary>The window, oldest first.</summary>
        public IReadOnlyList<DateTime> Days { get; init; } = Array.Empty<DateTime>();

        /// <summary>Days each rule fired in this district.</summary>
        public IReadOnlyDictionary<int, IReadOnlyList<PestAlertDay>> ByRule { get; init; } = new Dictionary<int, IReadOnlyList<PestAlertDay>>();

        /// <summary>Today's history row per rule — what a "was this accurate?" vote attaches to.</summary>
        public IReadOnlyDictionary<int, int> TodayIds { get; init; } = new Dictionary<int, int>();

        /// <summary>History rows the current user has already rated.</summary>
        public IReadOnlySet<int> RatedIds { get; init; } = new HashSet<int>();
    }

    /// <summary>Model for <c>Views/Advisory/_PestHistory.cshtml</c>: one rule's recent history and today's feedback.</summary>
    public sealed record PestHistoryStrip(PestAlertHistoryViewModel History, int RuleId);

    public interface IPestAlertHistoryService
    {
        /// <summary>Records today's firing rules for a district, once per (district, rule, day). Estimated weather is skipped.</summary>
        Task RecordAsync(string district, RegionalWeatherForecast weather, IEnumerable<EvaluatedPestAlert> alerts, CancellationToken cancellationToken = default);

        Task<PestAlertHistoryViewModel> GetRecentAsync(string district, string? userId, int days = 14, CancellationToken cancellationToken = default);

        Task<bool> RecordFeedbackAsync(string userId, int historyId, bool accurate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Scheduler pass: evaluates every district a farmer farms in, records the history, and notifies farmers of critical
        /// alerts for their crop. Returns the number of notifications created.
        /// </summary>
        Task<int> SweepAndNotifyAsync(CancellationToken cancellationToken = default);
    }

    public sealed class PestAlertHistoryService : IPestAlertHistoryService
    {
        public const int WindowDays = 14;
        private static readonly TimeSpan RecordedFlagFor = TimeSpan.FromMinutes(30);

        private readonly ApplicationDbContext _db;
        private readonly IPestAlertService _pests;
        private readonly INotificationService _notifications;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PestAlertHistoryService> _logger;

        public PestAlertHistoryService(ApplicationDbContext db, IPestAlertService pests, INotificationService notifications, IMemoryCache cache, ILogger<PestAlertHistoryService> logger)
        {
            _db = db;
            _pests = pests;
            _notifications = notifications;
            _cache = cache;
            _logger = logger;
        }

        public async Task RecordAsync(string district, RegionalWeatherForecast weather, IEnumerable<EvaluatedPestAlert> alerts, CancellationToken cancellationToken = default)
        {
            // A seasonal estimate is not an observation: recording it would teach the tuning data nothing true.
            if (weather.Source == WeatherSource.Estimated) return;

            var today = BangladeshClock.Today;
            var firing = alerts.GroupBy(a => a.RuleId).Select(g => g.First()).ToList();
            if (firing.Count == 0) return;

            var flag = $"pesthistory:{district}|{today:yyyyMMdd}|{string.Join(',', firing.Select(a => a.RuleId).OrderBy(id => id))}";
            if (_cache.TryGetValue(flag, out _)) return;

            var ruleIds = firing.Select(a => a.RuleId).ToList();
            var recorded = await _db.PestAlertHistories
                .Where(h => h.District == district && h.TriggeredOn == today && ruleIds.Contains(h.RuleId))
                .Select(h => h.RuleId)
                .ToListAsync(cancellationToken);

            var snapshot = JsonSerializer.Serialize(new
            {
                temperature = weather.Temperature,
                humidity = weather.Humidity,
                rainProbability = weather.RainProbability,
                condition = weather.Condition,
                source = weather.Source.ToString()
            });
            foreach (var alert in firing.Where(a => !recorded.Contains(a.RuleId)))
            {
                _db.PestAlertHistories.Add(new PestAlertHistory
                {
                    District = district,
                    RuleId = alert.RuleId,
                    Severity = alert.Severity,
                    RiskPercentage = alert.RiskPercentage,
                    TriggeredOn = today,
                    WeatherSnapshotJson = snapshot
                });
            }

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
            {
                // Another instance recorded the same (district, rule, day) first; the unique index makes that harmless.
                _db.ChangeTracker.Clear();
            }
            _cache.Set(flag, true, RecordedFlagFor);
        }

        public async Task<PestAlertHistoryViewModel> GetRecentAsync(string district, string? userId, int days = WindowDays, CancellationToken cancellationToken = default)
        {
            var today = BangladeshClock.Today;
            var from = today.AddDays(-(days - 1));
            var rows = await _db.PestAlertHistories.AsNoTracking()
                .Where(h => h.District == district && h.TriggeredOn >= from && h.TriggeredOn <= today)
                .Select(h => new { h.Id, h.RuleId, h.TriggeredOn, h.Severity })
                .ToListAsync(cancellationToken);

            var todayIds = rows.Where(r => r.TriggeredOn == today).ToDictionary(r => r.RuleId, r => r.Id);
            var rated = new HashSet<int>();
            if (userId is not null && todayIds.Count > 0)
            {
                var ids = todayIds.Values.ToList();
                rated = (await _db.PestAlertFeedback.AsNoTracking()
                    .Where(f => f.UserId == userId && ids.Contains(f.PestAlertHistoryId))
                    .Select(f => f.PestAlertHistoryId)
                    .ToListAsync(cancellationToken)).ToHashSet();
            }

            return new PestAlertHistoryViewModel
            {
                Days = Enumerable.Range(0, days).Select(i => from.AddDays(i)).ToList(),
                ByRule = rows.GroupBy(r => r.RuleId).ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<PestAlertDay>)g.OrderBy(r => r.TriggeredOn).Select(r => new PestAlertDay(r.TriggeredOn, r.Severity)).ToList()),
                TodayIds = todayIds,
                RatedIds = rated
            };
        }

        public async Task<bool> RecordFeedbackAsync(string userId, int historyId, bool accurate, CancellationToken cancellationToken = default)
        {
            if (!await _db.PestAlertHistories.AnyAsync(h => h.Id == historyId, cancellationToken)) return false;

            var existing = await _db.PestAlertFeedback.FirstOrDefaultAsync(f => f.PestAlertHistoryId == historyId && f.UserId == userId, cancellationToken);
            if (existing is null)
                _db.PestAlertFeedback.Add(new PestAlertFeedback { PestAlertHistoryId = historyId, UserId = userId, IsAccurate = accurate });
            else
                existing.IsAccurate = accurate;

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
            {
                _db.ChangeTracker.Clear();   // a double click raced itself; the first vote stands
            }
            return true;
        }

        public async Task<int> SweepAndNotifyAsync(CancellationToken cancellationToken = default)
        {
            var farmers = await _db.Users.AsNoTracking()
                .Where(u => u.UserRole == AppRoles.Farmer && u.District != null)
                .Select(u => new { u.Id, u.District, u.Specialization })
                .ToListAsync(cancellationToken);

            var today = BangladeshClock.Today;
            var created = 0;
            foreach (var group in farmers.GroupBy(f => BangladeshGeo.Canonical(f.District)!, StringComparer.OrdinalIgnoreCase))
            {
                if (!BangladeshGeo.AllDistricts.Contains(group.Key, StringComparer.OrdinalIgnoreCase)) continue;
                var weather = await _pests.GetRegionalWeatherAsync(group.Key);
                if (weather.Source == WeatherSource.Estimated) continue;

                await RecordAsync(group.Key, weather, await _pests.EvaluateAlertsAsync(weather, "All"), cancellationToken);

                // A critical alert reaches the farmer; the per-user dedupe key keeps it to one notification per rule per day.
                foreach (var cropGroup in group.GroupBy(f => PestAlertCrops.ForProfileCrop(f.Specialization)).Where(g => g.Key != "All"))
                {
                    var critical = (await _pests.EvaluateAlertsAsync(weather, cropGroup.Key)).Where(a => a.Severity == "Critical").ToList();
                    foreach (var farmer in cropGroup)
                        foreach (var alert in critical)
                        {
                            var sent = await _notifications.NotifyAsync(new NotificationRequest
                            {
                                UserId = farmer.Id,
                                Type = NotificationTypes.PestAlert,
                                TitleKey = "Pest alert: {0}",
                                MessageKey = "{0} risk is high in {1} today: {2}",
                                Args = new object[] { alert.DiseaseName, group.Key, alert.TriggerExplanation },
                                LinkUrl = AppLinks.PestAlerts(group.Key, cropGroup.Key),
                                DedupeKey = $"pest:{group.Key}:{alert.RuleId}:{today:yyyy-MM-dd}"
                            });
                            if (sent) created++;
                        }
                }
            }
            _logger.LogInformation("Pest alert sweep: {Districts} district(s), {Notifications} notification(s).", farmers.Select(f => f.District).Distinct().Count(), created);
            return created;
        }
    }
}
