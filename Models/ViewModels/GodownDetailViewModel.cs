using System;
using System.Collections.Generic;

namespace KrishiLink.Models.ViewModels
{
    public class GodownDetailViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string StorageType { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public double TotalCapacityTons { get; set; }
        public double AvailableCapacityTons { get; set; }
        public string PricePerTonPerMonth { get; set; } = string.Empty;
        public string DailyRatePerTon { get; set; } = string.Empty;
        public string Status { get; set; } = "Available";
        public string Description { get; set; } = string.Empty;

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

        // Owner Details
        public string OwnerName { get; set; } = string.Empty;
        public bool OwnerIsVerified { get; set; } = false;
        public string OwnerVerificationStatus { get; set; } = "Unverified";
        public double OwnerRating { get; set; }
        public int TotalReviews { get; set; }
        public string OwnerPhone { get; set; } = string.Empty;
        public string OwnerMemberSince { get; set; } = string.Empty;
        public List<OwnerBadgeViewModel> OwnerBadges { get; set; } = new();
        public string? OwnerRankText { get; set; }

        // Media & Highlights
        public List<string> ImageUrls { get; set; } = new();
        public List<string> Facilities { get; set; } = new();

        /// <summary>Days the farmer cannot book: owner-blocked dates plus days already at full capacity.</summary>
        public List<DateTime> UnavailableDates { get; set; } = new();

        // Booking Form Inputs
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public double RequestedCapacityTons { get; set; } = 5;
        public string? BookingNotes { get; set; }

        // Ratings & Reviews
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public ReviewsListViewModel Reviews { get; set; } = new();
    }
}
