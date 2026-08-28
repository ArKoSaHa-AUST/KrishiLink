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

        // Owner Details
        public string OwnerName { get; set; } = string.Empty;
        public double OwnerRating { get; set; } = 4.8;
        public int TotalReviews { get; set; } = 18;
        public string OwnerPhone { get; set; } = "+880 1712-889900";
        public string OwnerMemberSince { get; set; } = "January 2024";

        // Media & Highlights
        public List<string> ImageUrls { get; set; } = new();
        public List<string> Facilities { get; set; } = new();

        // Booking Form Inputs
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public double RequestedCapacityTons { get; set; } = 5;
        public string? BookingNotes { get; set; }
    }
}
