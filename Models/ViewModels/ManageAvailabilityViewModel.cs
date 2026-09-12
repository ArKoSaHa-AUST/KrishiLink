namespace KrishiLink.Models.ViewModels
{
    /// <summary>
    /// ViewModel for the owner's Manage Availability page (shared by equipment and godowns).
    /// Colour-coded calendar dates: Available (Green), Blocked by Owner (Gray), Booked by Farmer (Blue - locked).
    /// </summary>
    public class ManageAvailabilityViewModel
    {
        public int ListingId { get; set; }
        public string ListingName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string RateText { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string MonthName { get; set; } = string.Empty;

        /// <summary>First day of the month being edited (yyyy-MM-dd); posted back so the save only touches that month.</summary>
        public DateTime Month { get; set; }

        // Dates color-coded states
        public List<DateTime> AvailableDates { get; set; } = new();
        public List<DateTime> OwnerBlockedDates { get; set; } = new();
        public List<DateTime> FarmerBookedDates { get; set; } = new();

        public int Quantity { get; set; } = 1;
        public Dictionary<string, int> BookedUnitsByDate { get; set; } = new();

        public bool IsSaved { get; set; } = false;
    }
}
