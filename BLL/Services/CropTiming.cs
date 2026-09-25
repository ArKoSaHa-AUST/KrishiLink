using KrishiLink.Models.Entities;

namespace KrishiLink.BLL.Services
{
    /// <summary>The field stage a rental or storage booking belongs to.</summary>
    public enum PlanItemStage
    {
        LandPreparation,
        InField,
        Harvest,
        Storage
    }

    /// <summary>
    /// A plan item booked outside the crop's calendar window for its stage (ECO-02). Only ever a warning: the farmer knows
    /// their field better than a seed table does, so nothing is blocked.
    /// </summary>
    public sealed record CropTimingWarning(int ItemId, string ItemLabel, PlanItemStage Stage, int BookedMonth, IReadOnlyList<int> ExpectedMonths, string CropName);

    /// <summary>
    /// Harvest-plan timing against the seeded crop calendar: which calendar entries a plan's crop means, which months each
    /// kind of rental belongs in, and which items fall outside them. Pure and deterministic.
    /// </summary>
    public static class CropTiming
    {
        // What each equipment category is rented for. Anything unlisted ("Other") is never second-guessed.
        private static readonly Dictionary<string, PlanItemStage> StageByCategory = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Tractor"] = PlanItemStage.LandPreparation,
            ["Power Tiller"] = PlanItemStage.LandPreparation,
            ["Seed Drill / Seeder"] = PlanItemStage.LandPreparation,
            ["Power Sprayer"] = PlanItemStage.InField,
            ["Irrigation Pump"] = PlanItemStage.InField,
            ["Combine Harvester"] = PlanItemStage.Harvest,
            ["Thresher"] = PlanItemStage.Harvest
        };

        /// <summary>
        /// The calendar entries a plan's crop refers to: the linked entry if there is one, else an exact calendar name
        /// ("Boro Rice (HYV & Hybrid)", as the calendar's own button saves it), else every entry for a profile crop group
        /// ("Pulses" → lentil, chickpea, mungbean).
        /// </summary>
        public static IReadOnlyList<CropCalendarEntry> Resolve(string? crop, int? entryId, IReadOnlyCollection<CropCalendarEntry> calendar)
        {
            if (entryId is { } id && calendar.FirstOrDefault(c => c.Id == id) is { } linked) return new[] { linked };
            var value = crop?.Trim();
            if (string.IsNullOrEmpty(value)) return Array.Empty<CropCalendarEntry>();
            var byName = calendar.FirstOrDefault(c => string.Equals(c.Name, value, StringComparison.OrdinalIgnoreCase));
            if (byName is not null) return new[] { byName };
            return calendar.Where(c => string.Equals(c.ProfileCropName, value, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
        }

        public static PlanItemStage? StageFor(string itemType, string? category) =>
            itemType == HarvestPlanItemType.Godown ? PlanItemStage.Storage
            : category is not null && StageByCategory.TryGetValue(category, out var stage) ? stage
            : null;

        /// <summary>Months (1-12) in which a booking for <paramref name="stage"/> fits any of the crops, with a month of slack.</summary>
        public static IReadOnlySet<int> ExpectedMonths(PlanItemStage stage, IEnumerable<CropCalendarEntry> crops)
        {
            var months = new HashSet<int>();
            foreach (var crop in crops)
            {
                switch (stage)
                {
                    case PlanItemStage.LandPreparation:
                        // Tillage happens in the sowing months and the month before them.
                        foreach (var m in crop.SowingMonths) { months.Add(m); months.Add(Shift(m, -1)); }
                        break;
                    case PlanItemStage.InField:
                        months.UnionWith(crop.SowingMonths);
                        months.UnionWith(crop.GrowingMonths);
                        break;
                    case PlanItemStage.Harvest:
                        // Threshing often runs into the month after reaping.
                        foreach (var m in crop.HarvestingMonths) { months.Add(m); months.Add(Shift(m, 1)); }
                        break;
                    case PlanItemStage.Storage:
                        // Storage starts when the harvest comes in, or within two months of it.
                        foreach (var m in crop.HarvestingMonths) { months.Add(m); months.Add(Shift(m, 1)); months.Add(Shift(m, 2)); }
                        break;
                }
            }
            months.RemoveWhere(m => m is < 1 or > 12);
            return months;
        }

        /// <summary>
        /// A warning when no day of the booking falls in its stage's months. Storage is judged by its first day only, because
        /// a store legitimately runs for many months after the harvest. Null when the item fits, or cannot be judged.
        /// </summary>
        public static CropTimingWarning? Check(int itemId, string itemLabel, string itemType, string? category, DateTime start, DateTime end,
            IReadOnlyList<CropCalendarEntry> crops, string cropName)
        {
            if (crops.Count == 0 || StageFor(itemType, category) is not { } stage) return null;
            var expected = ExpectedMonths(stage, crops);
            if (expected.Count == 0) return null;

            var booked = stage == PlanItemStage.Storage ? new[] { start.Month } : MonthsSpanned(start, end);
            if (booked.Any(expected.Contains)) return null;
            return new CropTimingWarning(itemId, itemLabel, stage, start.Month, expected.OrderBy(m => m).ToList(), cropName);
        }

        /// <summary>The calendar months a date range touches (at most all twelve).</summary>
        public static IReadOnlyList<int> MonthsSpanned(DateTime start, DateTime end)
        {
            var months = new List<int>();
            if (end < start) end = start;
            for (var m = new DateTime(start.Year, start.Month, 1); m <= end && months.Count < 12; m = m.AddMonths(1))
                months.Add(m.Month);
            return months;
        }

        private static int Shift(int month, int by) => ((month - 1 + by) % 12 + 12) % 12 + 1;
    }
}
