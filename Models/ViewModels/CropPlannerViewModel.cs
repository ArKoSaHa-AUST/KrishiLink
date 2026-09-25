using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;

namespace KrishiLink.Models.ViewModels
{
    /// <summary>The Crop Planner tab (ADV-07): this week on the farm, a sowing-date simulator and a crop comparison.</summary>
    public class CropPlannerViewModel
    {
        public AdvisoryContextViewModel Context { get; set; } = new();
        public IReadOnlyList<CropCalendarEntry> Crops { get; set; } = Array.Empty<CropCalendarEntry>();

        public ThisWeekPlan? ThisWeek { get; set; }

        /// <summary>True when "this week" follows a saved Smart Advisor result rather than the profile crop.</summary>
        public bool ThisWeekFromSavedAdvice { get; set; }

        public int? SimCropId { get; set; }
        public DateTime SowDate { get; set; }
        public SowingSimulation? Simulation { get; set; }

        public IReadOnlyList<int> CompareIds { get; set; } = Array.Empty<int>();
        public List<CropComparisonRow> Comparison { get; set; } = new();
    }

    public class CropComparisonRow
    {
        public required CropCalendarEntry Crop { get; init; }
        public (int Min, int Max)? Duration { get; init; }

        /// <summary>Rules in the pest engine that cover this crop at all, and how many fire in the district right now.</summary>
        public int RulesCovering { get; init; }
        public int ActiveWarnings { get; init; }
    }
}
