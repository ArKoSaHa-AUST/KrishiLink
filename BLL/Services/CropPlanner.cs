using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KrishiLink.Models.Entities;

namespace KrishiLink.BLL.Services
{
    public enum CropStage
    {
        Sowing,
        Growing,
        Harvesting,
        OffSeason
    }

    /// <summary>"This week on your farm" for one crop (ADV-07 §1).</summary>
    public sealed record ThisWeekPlan(CropCalendarEntry Crop, CropStage Stage, DateTime Today, DateTime? NextSowing, IReadOnlyList<CropNeed> Needs);

    /// <summary>What sowing on a chosen date implies (ADV-07 §2).</summary>
    public sealed class SowingSimulation
    {
        public required CropCalendarEntry Crop { get; init; }
        public DateTime SowDate { get; init; }
        public DateTime HarvestFrom { get; init; }
        public DateTime HarvestTo { get; init; }

        /// <summary>Weeks outside the DAE sowing window: negative = early, positive = late, 0 = inside.</summary>
        public int WeeksOutsideWindow { get; init; }

        /// <summary>Harvest-window months the seasonal baseline treats as monsoon.</summary>
        public IReadOnlyList<int> MonsoonHarvestMonths { get; init; } = Array.Empty<int>();

        public IReadOnlyList<CropNeed> Needs { get; init; } = Array.Empty<CropNeed>();

        public bool InsideWindow => WeeksOutsideWindow == 0;
        public bool HarvestInMonsoon => MonsoonHarvestMonths.Count > 0;
    }

    /// <summary>
    /// Planning arithmetic over the seeded crop calendar: current stage, sowing-date simulation and a calendar file. Pure
    /// and deterministic — every date is derived from the seed's months and duration and the Asia/Dhaka date passed in.
    /// </summary>
    public static class CropPlanner
    {
        private static readonly Regex Range = new(@"(\d+)\s*[–-]\s*(\d+)", RegexOptions.CultureInvariant);
        private static readonly Regex Single = new(@"(\d+)", RegexOptions.CultureInvariant);

        /// <summary>"140 – 160 Days" → (140, 160); "60 Days" → (60, 60); null when no number is present.</summary>
        public static (int Min, int Max)? ParseDuration(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var range = Range.Match(text);
            if (range.Success)
            {
                var a = int.Parse(range.Groups[1].Value, CultureInfo.InvariantCulture);
                var b = int.Parse(range.Groups[2].Value, CultureInfo.InvariantCulture);
                return (Math.Min(a, b), Math.Max(a, b));
            }
            var single = Single.Match(text);
            return single.Success ? (int.Parse(single.Value, CultureInfo.InvariantCulture), int.Parse(single.Value, CultureInfo.InvariantCulture)) : null;
        }

        public static CropStage StageFor(CropCalendarEntry crop, int month) =>
            crop.SowingMonths.Contains(month) ? CropStage.Sowing
            : crop.HarvestingMonths.Contains(month) ? CropStage.Harvesting
            : crop.GrowingMonths.Contains(month) ? CropStage.Growing
            : CropStage.OffSeason;

        public static ThisWeekPlan ThisWeek(CropCalendarEntry crop, DateTime today)
        {
            var stage = StageFor(crop, today.Month);
            DateTime? nextSowing = crop.SowingMonths.Count == 0 ? null : crop.SowingMonths.Select(m => CropNeeds.NextStart(m, today)).Min();
            // Only what matters now: tillage around sowing, machines and storage around harvest.
            var needs = CropNeeds.For(crop, today).Where(n => stage switch
            {
                CropStage.Sowing => n.Stage == CropNeedStage.LandPreparation,
                CropStage.Harvesting => n.Stage != CropNeedStage.LandPreparation,
                _ => false
            }).ToList();
            return new ThisWeekPlan(crop, stage, today, nextSowing, needs);
        }

        public static SowingSimulation Simulate(CropCalendarEntry crop, DateTime sowDate, DateTime today)
        {
            var sow = sowDate.Date;
            var (min, max) = ParseDuration(crop.DurationDays) ?? (90, 120);
            var harvestFrom = sow.AddDays(min);
            var harvestTo = sow.AddDays(max);

            var monsoon = new List<int>();
            for (var month = new DateTime(harvestFrom.Year, harvestFrom.Month, 1); month <= harvestTo; month = month.AddMonths(1))
                if (WeatherService.IsMonsoonMonth(month.Month) && !monsoon.Contains(month.Month)) monsoon.Add(month.Month);

            // Land prep in the week before sowing; harvest machinery at the start of the window; storage straight after.
            var needs = new List<CropNeed>
            {
                new(CropNeedStage.LandPreparation, "Power Tiller", false, sow.AddDays(-7), sow.AddDays(-1))
            };
            var harvestNeeds = CropNeeds.For(crop, harvestFrom).Where(n => n.Stage != CropNeedStage.LandPreparation);
            foreach (var need in harvestNeeds)
            {
                needs.Add(need.Stage == CropNeedStage.Harvest
                    ? need with { From = harvestFrom, To = harvestFrom.AddDays(6) }
                    : need with { From = harvestTo.AddDays(1), To = harvestTo.AddDays(60) });
            }

            return new SowingSimulation
            {
                Crop = crop,
                SowDate = sow,
                HarvestFrom = harvestFrom,
                HarvestTo = harvestTo,
                WeeksOutsideWindow = WeeksOutside(crop.SowingMonths, sow),
                MonsoonHarvestMonths = monsoon,
                Needs = needs.Where(n => n.To >= today.Date).ToList()
            };
        }

        /// <summary>Signed distance in whole weeks from the nearest day of the sowing window (0 inside it).</summary>
        internal static int WeeksOutside(IReadOnlyCollection<int> sowingMonths, DateTime sow)
        {
            if (sowingMonths.Count == 0 || sowingMonths.Contains(sow.Month)) return 0;
            var best = int.MaxValue;
            foreach (var year in new[] { sow.Year - 1, sow.Year, sow.Year + 1 })
                foreach (var m in sowingMonths)
                {
                    var start = new DateTime(year, m, 1);
                    var end = start.AddMonths(1).AddDays(-1);
                    var days = sow < start ? (sow - start).Days : (sow - end).Days;   // negative = before the window
                    if (Math.Abs(days) < Math.Abs(best)) best = days;
                }
            if (best == int.MaxValue) return 0;
            var weeks = (int)Math.Round(best / 7.0, MidpointRounding.AwayFromZero);
            // A few days outside the window still counts as a week, so a late date is never shown as on time.
            return weeks != 0 ? weeks : Math.Sign(best);
        }

        /// <summary>An iCalendar file of the plan's key dates, so it lands in the farmer's phone calendar and works offline.</summary>
        public static string ToIcs(SowingSimulation plan, string cropName, Func<CropNeed, string> needTitle, string sowingTitle, string harvestTitle, DateTime stampUtc)
        {
            var ics = new StringBuilder();
            void Line(string text) => ics.Append(text).Append("\r\n");
            void Event(string uidPart, string summary, DateTime from, DateTime toInclusive)
            {
                Line("BEGIN:VEVENT");
                Line($"UID:{plan.Crop.Key}-{uidPart}-{plan.SowDate:yyyyMMdd}@krishilink");
                Line($"DTSTAMP:{stampUtc:yyyyMMdd'T'HHmmss'Z'}");
                Line($"DTSTART;VALUE=DATE:{from:yyyyMMdd}");
                Line($"DTEND;VALUE=DATE:{toInclusive.AddDays(1):yyyyMMdd}");
                Line($"SUMMARY:{Escape($"{cropName}: {summary}")}");
                Line("END:VEVENT");
            }

            Line("BEGIN:VCALENDAR");
            Line("VERSION:2.0");
            Line("PRODID:-//KrishiLink//Crop Planner//EN");
            Line("CALSCALE:GREGORIAN");
            Event("sow", sowingTitle, plan.SowDate, plan.SowDate);
            Event("harvest", harvestTitle, plan.HarvestFrom, plan.HarvestTo);
            foreach (var need in plan.Needs)
                Event(need.Stage.ToString().ToLowerInvariant(), needTitle(need), need.From, need.To);
            Line("END:VCALENDAR");
            return ics.ToString();
        }

        private static string Escape(string text) =>
            text.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r", string.Empty).Replace("\n", "\\n");
    }
}
