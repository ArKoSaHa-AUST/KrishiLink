namespace KrishiLink.Models.ViewModels
{
    /// <summary>
    /// ViewModel for the Farmer Dashboard page.
    /// Provides data for active bookings, crop recommendations, and recent activity.
    /// </summary>
    public class FarmerDashboardViewModel
    {
        public string FarmerName { get; set; } = "Farmer";

        /// <summary>Active equipment rentals and godown bookings.</summary>
        public List<BookingSummaryItem> ActiveBookings { get; set; } = new();

        /// <summary>Last crop advisory recommendation (null if the farmer has never used the service).</summary>
        public CropRecommendation? SavedRecommendation { get; set; }

        /// <summary>Active real-time agrometeorological disease/weather warning for the farmer's district.</summary>
        public WeatherNoteItem? WeatherAlert { get; set; }

        /// <summary>Proactive weather-triggered crop & machinery suggestion for farmer.</summary>
        public WeatherSuggestionViewModel? WeatherSuggestion { get; set; }

        /// <summary>Recent activity feed entries, newest first.</summary>
        public List<ActivityFeedItem> RecentActivity { get; set; } = new();

        /// <summary>Farmer Loyalty Points (KrishiPoints) summary widget.</summary>
        public FarmerDashboardLoyaltyWidgetViewModel LoyaltyWidget { get; set; } = new();

        /// <summary>Monthly crop calendar advisory strip customized to farmer's crop and district.</summary>
        public FarmerCropAdvisoryViewModel? CropCalendarAdvisory { get; set; }

        /// <summary>Number of draft harvest plans currently saved by the farmer.</summary>
        public int DraftHarvestPlansCount { get; set; }

        /// <summary>Next active/upcoming submitted harvest plan.</summary>
        public HarvestPlanSummaryViewModel? NextSubmittedHarvestPlan { get; set; }

        /// <summary>Count of active favorite listings saved by farmer.</summary>
        public int FavoritesCount { get; set; }

        /// <summary>Count of active saved searches configured by farmer.</summary>
        public int SavedSearchesCount { get; set; }
    }

    /// <summary>
    /// A single row in the Active Bookings summary table.
    /// </summary>
    public class BookingSummaryItem
    {
        public int Id { get; set; }
        public string ItemName { get; set; } = string.Empty;

        /// <summary>"Equipment" or "Godown"</summary>
        public string BookingType { get; set; } = "Equipment";

        public string? Location { get; set; }
        public string DateRange { get; set; } = string.Empty;

        /// <summary>Pending | Accepted | Completed | Rejected</summary>
        public string Status { get; set; } = "Pending";

        /// <summary>URL to the booking detail page.</summary>
        public string? DetailUrl { get; set; }
    }

    /// <summary>
    /// A saved crop advisory recommendation shown on the dashboard widget.
    /// </summary>
    public class CropRecommendation
    {
        public string CropName { get; set; } = string.Empty;
        public string Season { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string? GuideUrl { get; set; }
    }

    /// <summary>
    /// A single entry in the recent activity feed.
    /// </summary>
    public class ActivityFeedItem
    {
        public string Description { get; set; } = string.Empty;

        /// <summary>Bootstrap icon class, e.g. "bi-tools", "bi-building".</summary>
        public string IconClass { get; set; } = "bi-clock-history";

        /// <summary>CSS class for the icon background accent, e.g. "text-success", "text-warning".</summary>
        public string IconColor { get; set; } = "text-success";

        public string TimeAgo { get; set; } = string.Empty;
    }
}
