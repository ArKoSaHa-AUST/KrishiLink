namespace KrishiLink.Models.ViewModels
{
    /// <summary>
    /// ViewModel for Equipment Owner Manage Availability page.
    /// Supports color-coded calendar dates: Available (Green), Blocked by Owner (Gray), Booked by Farmer (Blue - locked).
    /// </summary>
    public class ManageAvailabilityViewModel
    {
        public int EquipmentId { get; set; }
        public string EquipmentName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string DailyRate { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string MonthName { get; set; } = string.Empty;

        // Dates color-coded states
        public List<DateTime> AvailableDates { get; set; } = new();
        public List<DateTime> OwnerBlockedDates { get; set; } = new();
        public List<DateTime> FarmerBookedDates { get; set; } = new();

        public bool IsSaved { get; set; } = false;
    }
}
