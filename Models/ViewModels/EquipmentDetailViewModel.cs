namespace KrishiLink.Models.ViewModels
{
    /// <summary>
    /// ViewModel for Equipment Details & Rental Request page.
    /// Includes equipment info, owner details, image gallery, booked availability dates, and request form.
    /// </summary>
    public class EquipmentDetailViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DailyRate { get; set; } = string.Empty;
        public string HourlyRate { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Status { get; set; } = "Available";

        // Owner Information
        public string OwnerName { get; set; } = string.Empty;
        public double OwnerRating { get; set; } = 4.8;
        public int TotalReviews { get; set; } = 18;
        public string OwnerPhone { get; set; } = string.Empty;
        public string OwnerMemberSince { get; set; } = "2024";

        // Image Gallery URLs
        public List<string> ImageUrls { get; set; } = new();

        // Already booked dates (for visual calendar blocking)
        public List<DateTime> BookedDates { get; set; } = new();

        // Form inputs & submission state
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Note { get; set; }
        public bool IsRequestSubmitted { get; set; } = false;
    }
}
