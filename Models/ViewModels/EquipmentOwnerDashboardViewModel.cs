namespace KrishiLink.Models.ViewModels
{
    /// <summary>
    /// ViewModel for the Equipment Owner Dashboard page:
    /// summary counts, listings preview, and pending rental requests widget.
    /// </summary>
    public class EquipmentOwnerDashboardViewModel
    {
        public string OwnerName { get; set; } = "Owner";

        public int TotalListings { get; set; }
        public int ActiveRentals { get; set; }
        public int PendingRequests => PendingRequestItems.Count;
        public decimal ThisMonthRevenue { get; set; }

        public bool IsVerified { get; set; } = false;
        public string VerificationStatus { get; set; } = "Unverified";

        public List<OwnerListingItem> Listings { get; set; } = new();
        public List<RentalRequestItem> PendingRequestItems { get; set; } = new();

        public OwnerBadgeDashboardWidgetViewModel BadgeWidget { get; set; } = new();
    }

    /// <summary>A single equipment listing preview card on the owner dashboard.</summary>
    public class OwnerListingItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string DailyRate { get; set; } = string.Empty;

        /// <summary>Available | Rented | Unavailable</summary>
        public string Status { get; set; } = "Available";

        public int Quantity { get; set; } = 1;

        public int? LastServicedDaysAgo { get; set; }
        public string? LastServicedText { get; set; }
    }
}
