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
        public decimal DailyRateAmount { get; set; }
        public string HourlyRate { get; set; } = string.Empty;
        public int MinRentalDays { get; set; } = 1;
        public bool HasRateRules { get; set; }
        public string FromRateText { get; set; } = string.Empty;
        public List<EquipmentRateRuleViewModel> RateRules { get; set; } = new();
        public string Location { get; set; } = string.Empty;
        public string? District { get; set; }
        public string Status { get; set; } = "Available";

        // Geographic Coordinates & Navigation
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string FormattedCoordinates => (Latitude.HasValue && Longitude.HasValue)
            ? $"{Latitude.Value:F4}° N, {Longitude.Value:F4}° E"
            : string.Empty;
        public string GoogleMapsUrl => (Latitude.HasValue && Longitude.HasValue)
            ? $"https://www.google.com/maps/dir/?api=1&destination={Latitude.Value:F6},{Longitude.Value:F6}"
            : "https://www.google.com/maps";
        public string OpenStreetMapUrl => (Latitude.HasValue && Longitude.HasValue)
            ? $"https://www.openstreetmap.org/?mlat={Latitude.Value:F6}&mlon={Longitude.Value:F6}#map=14/{Latitude.Value:F6}/{Longitude.Value:F6}"
            : "https://www.openstreetmap.org";

        // Owner Information
        public string OwnerName { get; set; } = string.Empty;
        public bool OwnerIsVerified { get; set; } = false;
        public string OwnerVerificationStatus { get; set; } = "Unverified";
        public double OwnerRating { get; set; }
        public int TotalReviews { get; set; }
        public string OwnerPhone { get; set; } = string.Empty;
        public string OwnerMemberSince { get; set; } = string.Empty;
        public List<OwnerBadgeViewModel> OwnerBadges { get; set; } = new();
        public string? OwnerRankText { get; set; }

        // Image Gallery URLs
        public List<string> ImageUrls { get; set; } = new();

        // Already booked dates (for visual calendar blocking)
        public List<DateTime> BookedDates { get; set; } = new();

        // Form inputs & submission state
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Note { get; set; }
        public bool IsRequestSubmitted { get; set; } = false;

        // Ratings & Reviews
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public ReviewsListViewModel Reviews { get; set; } = new();

        // Equipment Health Tracker & Maintenance History
        public DateTime? LastServicedDate { get; set; }
        public int? LastServicedDaysAgo { get; set; }
        public string? LastServicedText { get; set; }
        public List<EquipmentMaintenanceItemViewModel> MaintenanceHistory { get; set; } = new();

        // Farmer Loyalty Points & Promo Discounts
        public int FarmerLoyaltyPoints { get; set; }
        public string FarmerTierName { get; set; } = string.Empty;
        public List<FixedConversionTierViewModel> AvailableConversionTiers { get; set; } = new();
        public string? AppliedPromoCode { get; set; }
        public decimal DiscountAmount { get; set; }
        public int PointsUsed { get; set; }
    }
}
