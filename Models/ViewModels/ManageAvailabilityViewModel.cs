namespace KrishiLink.Models.ViewModels
{
    /// <summary>
    /// Outcome of a bulk date range block or unblock operation.
    /// </summary>
    public record BulkAvailabilityResult(bool Found, int Changed, int SkippedBooked, int AlreadyInState, string? Error);

    /// <summary>
    /// A contiguous blocked date interval with day count and optional reason.
    /// </summary>
    public class BlockedPeriodItem
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public string? Reason { get; set; }
        public int Days => (To.Date - From.Date).Days + 1;

        public DateTime Start => From;
        public DateTime End => To;
        public int DayCount => Days;
    }

    /// <summary>
    /// Binding model for bulk block/unblock form submissions.
    /// </summary>
    public class BulkAvailabilityInputModel
    {
        public int ListingId { get; set; }
        public DateTime From { get; set; } = DateTime.Today;
        public DateTime To { get; set; } = DateTime.Today;
        public List<DayOfWeek> DaysOfWeek { get; set; } = new();
        public string? Reason { get; set; }
        public DateTime Month { get; set; }
    }

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

        /// <summary>Reason for owner blockage keyed by date string ("yyyy-MM-dd").</summary>
        public Dictionary<string, string?> BlockedReasonsByDate { get; set; } = new();

        /// <summary>Upcoming blocked periods spanning the next 12 months.</summary>
        public List<BlockedPeriodItem> UpcomingBlockedPeriods { get; set; } = new();

        public bool IsSaved { get; set; } = false;
    }
}
