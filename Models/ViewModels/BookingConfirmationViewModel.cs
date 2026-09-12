using System;
using System.Collections.Generic;

namespace KrishiLink.Models.ViewModels
{
    public class BookingConfirmationViewModel
    {
        public int BookingId { get; set; }
        public string BookingCode { get; set; } = string.Empty; // e.g. KL-EQ-2026-001
        public string BookingType { get; set; } = "Equipment"; // "Equipment" or "Godown"
        public int ListingId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string FormattedCoordinates => Latitude.HasValue && Longitude.HasValue ? $"{Latitude.Value:F5}, {Longitude.Value:F5}" : "";
        public string GoogleMapsUrl => Latitude.HasValue && Longitude.HasValue
            ? $"https://www.google.com/maps/dir/?api=1&destination={Latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)},{Longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            : $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(Location)}";

        // Dates & Duration
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string DateRangeDisplay => $"{StartDate:dd MMM yyyy} – {EndDate:dd MMM yyyy}";
        public int TotalDays => Math.Max(1, (EndDate - StartDate).Days);
        public double TotalMonths => Math.Max(0.5, Math.Round((EndDate - StartDate).TotalDays / 30.0, 1));
        public string DurationDisplay => BookingType == "Equipment"
            ? (TotalDays == 1 ? "1 Day" : $"{TotalDays} Days")
            : (TotalMonths == 1 ? "1 Month" : $"{TotalMonths:0.#} Months");

        // Financials
        public decimal TotalCost { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? AppliedPromoCode { get; set; }
        public int PointsUsed { get; set; }
        public int PointsEarned { get; set; }
        public decimal NetCost => Math.Max(0m, TotalCost - DiscountAmount);
        public string CostDisplay => $"৳{NetCost:N0}";
        public string OriginalCostDisplay => $"৳{TotalCost:N0}";
        public string RateDescription { get; set; } = string.Empty;
        public string QuantityDisplay { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = "Unpaid (Awaiting Confirmation)";

        // Status & Metadata
        public string Status { get; set; } = "Pending";
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? FarmerNotes { get; set; }
        public string? OwnerRemarks { get; set; }

        // Farmer Info
        public string FarmerId { get; set; } = string.Empty;
        public string FarmerName { get; set; } = string.Empty;
        public string FarmerPhone { get; set; } = string.Empty;
        public string FarmerEmail { get; set; } = string.Empty;
        public string FarmerAddress { get; set; } = string.Empty;

        // Owner Info
        public string OwnerId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerPhone { get; set; } = string.Empty;
        public string OwnerBusiness { get; set; } = string.Empty;
        public string OwnerLocation { get; set; } = string.Empty;
        public bool OwnerIsVerified { get; set; }

        // QR Code & Verification
        public string VerificationUrl { get; set; } = string.Empty;
        public string QrCodeSvg { get; set; } = string.Empty;
        public string QrCodeBase64 { get; set; } = string.Empty;

        // UI Helpers
        public bool JustCreated { get; set; }
        public bool CanCancel { get; set; }
        public List<BookingTimelineStep> Timeline { get; set; } = new();
    }

    public class BookingVerificationViewModel
    {
        public bool IsFound { get; set; }
        public bool IsValid { get; set; }
        public string VerificationStatusMessage { get; set; } = string.Empty;
        public string SecurityBadgeClass { get; set; } = "bg-success";

        public string BookingCode { get; set; } = string.Empty;
        public int BookingId { get; set; }
        public string BookingType { get; set; } = "Equipment";
        public int ListingId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string DateRangeDisplay => $"{StartDate:dd MMM yyyy} – {EndDate:dd MMM yyyy}";
        public string DurationDisplay { get; set; } = string.Empty;

        public decimal TotalCost { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? AppliedPromoCode { get; set; }
        public int PointsUsed { get; set; }
        public decimal NetCost => Math.Max(0m, TotalCost - DiscountAmount);
        public string CostDisplay => $"৳{NetCost:N0}";
        public string OriginalCostDisplay => $"৳{TotalCost:N0}";
        public string RateDescription { get; set; } = string.Empty;
        public string QuantityDisplay { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";

        public DateTime RequestedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Farmer Info
        public string FarmerId { get; set; } = string.Empty;
        public string FarmerName { get; set; } = string.Empty;
        public string FarmerPhone { get; set; } = string.Empty;
        public string FarmerLocation { get; set; } = string.Empty;

        // Owner Info
        public string OwnerId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerPhone { get; set; } = string.Empty;
        public string OwnerBusiness { get; set; } = string.Empty;

        // Interactive Owner Permissions
        public bool IsCurrentOwner { get; set; }
        public bool CanAccept { get; set; }
        public bool CanReject { get; set; }
        public bool CanConfirmPickup { get; set; }
        public bool CanComplete { get; set; }

        public string QrCodeSvg { get; set; } = string.Empty;
        public string QrCodeBase64 { get; set; } = string.Empty;
        public string VerificationUrl { get; set; } = string.Empty;
    }
}
