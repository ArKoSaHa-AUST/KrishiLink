using System;
using System.Collections.Generic;
using KrishiLink.BLL.Services;

namespace KrishiLink.Models.ViewModels
{
    public static class FarmerTrustLevel
    {
        public const string New = "New";
        public const string Reliable = "Reliable";
        public const string Caution = "Caution";
    }

    /// <summary>
    /// Compact metrics computed across all bookings for a farmer, used for fast batch summary lookups.
    /// </summary>
    public class FarmerTrustSummary
    {
        public int Completed { get; set; }
        public int Cancelled { get; set; }
        public int TotalDecided { get; set; }
        public double CancellationRate { get; set; }
        public string MemberSince { get; set; } = string.Empty;
        public string TrustLevel { get; set; } = FarmerTrustLevel.New;
    }

    /// <summary>
    /// Recent booking item between the farmer and the viewing owner.
    /// </summary>
    public class FarmerRecentBookingItem
    {
        public string BookingType { get; set; } = string.Empty; // "Equipment" | "Godown"
        public int BookingId { get; set; }
        public string ListingName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string DateRange => ListingFormat.DateRange(StartDate, EndDate);
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Historical interaction metrics between the farmer and the viewing owner.
    /// </summary>
    public class FarmerWithYouSummary
    {
        public int Completed { get; set; }
        public int Active { get; set; }
        public int Cancelled { get; set; }
        public DateTime? FirstBookingOn { get; set; }
    }

    /// <summary>
    /// Full trust profile view model for owners reviewing a requesting farmer.
    /// </summary>
    public class FarmerProfileViewModel
    {
        public string FarmerId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Location { get; set; }
        public string? District { get; set; }
        public string? MainCrop { get; set; }
        public string MemberSince { get; set; } = string.Empty;
        public string LoyaltyTier { get; set; } = string.Empty;
        public char AvatarLetter => string.IsNullOrWhiteSpace(FullName) ? 'F' : FullName[0];

        // Overall booking metrics
        public int TotalRequests { get; set; }
        public int Completed { get; set; }
        public int Active { get; set; }
        public int Cancelled { get; set; }
        public int CancelledAfterPayment { get; set; }
        public int Rejected { get; set; }
        public int Decided { get; set; }

        public double CancellationRate { get; set; }
        public double? AvgHoursToPay { get; set; }

        // Review metrics
        public int ReviewsGiven { get; set; }
        public double? AvgRatingGiven { get; set; }

        // Interaction with viewing owner
        public FarmerWithYouSummary WithYou { get; set; } = new();
        public List<FarmerRecentBookingItem> RecentWithYou { get; set; } = new();

        public string TrustLevel { get; set; } = FarmerTrustLevel.New;
        public bool PhoneVisible { get; set; }
        public string? Phone { get; set; }
    }
}
