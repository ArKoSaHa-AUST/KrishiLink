namespace KrishiLink.Models.ViewModels
{
    /// <summary>
    /// ViewModel for the Godown Owner Dashboard page:
    /// summary counts, godown listings with capacity utilization, and pending booking requests.
    /// </summary>
    public class GodownOwnerDashboardViewModel
    {
        public string OwnerName { get; set; } = "Owner";

        public int TotalGodowns { get; set; }
        public double OccupiedCapacityTons { get; set; }
        public double TotalCapacityTons { get; set; }
        public int PendingRequests => PendingRequestItems.Count;
        public decimal ThisMonthRevenue { get; set; }

        public bool IsVerified { get; set; } = false;
        public string VerificationStatus { get; set; } = "Unverified";

        public List<OwnerGodownItem> Godowns { get; set; } = new();
        public List<GodownBookingRequestItem> PendingRequestItems { get; set; } = new();

        public OwnerBadgeDashboardWidgetViewModel BadgeWidget { get; set; } = new();
    }

    /// <summary>A godown preview card with capacity utilization for the owner dashboard.</summary>
    public class OwnerGodownItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string StorageType { get; set; } = string.Empty;
        public double TotalCapacityTons { get; set; }
        public double AvailableCapacityTons { get; set; }

        /// <summary>Active | Full | Inactive</summary>
        public string Status { get; set; } = "Active";

        public double OccupiedTons => TotalCapacityTons - AvailableCapacityTons;
        public int UtilizationPercent => TotalCapacityTons > 0 ? (int)Math.Round(OccupiedTons / TotalCapacityTons * 100) : 0;
    }

    /// <summary>An incoming storage booking request row in the pending requests widget.</summary>
    public class GodownBookingRequestItem
    {
        public int Id { get; set; }
        public string FarmerId { get; set; } = string.Empty;
        public string FarmerName { get; set; } = string.Empty;
        public int FarmerCompleted { get; set; }
        public double FarmerCancellationRate { get; set; }
        public string FarmerMemberSince { get; set; } = string.Empty;
        public string FarmerTrustLevel { get; set; } = string.Empty;
        public int GodownId { get; set; }
        public string GodownName { get; set; } = string.Empty;
        public double RequestedCapacityTons { get; set; }
        public string DateRange { get; set; } = string.Empty;
        public string? Note { get; set; }

        /// <summary>Pending | Accepted | Paid | Rejected | Completed | Cancelled</summary>
        public string Status { get; set; } = "Pending";

        /// <summary>Total the farmer pays (snapshot at acceptance).</summary>
        public decimal AgreedGross { get; set; }

        /// <summary>Set once the farmer's payment has succeeded; drives the "Paid ৳X" chip and the Mark Completed gate.</summary>
        public string? PaymentReference { get; set; }
        public bool IsPaid => PaymentReference is not null;

        /// <summary>Owner's reason shown on rejected requests.</summary>
        public string? RejectReason { get; set; }

        /// <summary>When the farmer submitted the request (drives sorting and "time ago").</summary>
        public DateTime RequestedOn { get; set; }

        /// <summary>Set when a pending request's dates overlap an accepted booking or blocked date.</summary>
        public bool HasConflict { get; set; }
        public string? ConflictHint { get; set; }
        public int ModificationCount { get; set; } = 0;
        public string? PreviousDetails { get; set; }

        public int? HarvestPlanId { get; set; }
        public string? HarvestPlanName { get; set; }
        public int HarvestPlanItemCount { get; set; }
        public string? HarvestPlanOtherItems { get; set; }

        /// <summary>Compact relative timestamp, e.g. "2h ago". Empty when RequestedOn is unset.</summary>
        public string TimeAgo => TimeAgoFormatter.Format(RequestedOn);
    }

    /// <summary>ViewModel for the full Godown Booking Requests page (owner decision view).</summary>
    public class GodownBookingRequestsViewModel
    {
        public List<GodownBookingRequestItem> Requests { get; set; } = new();

        /// <summary>Owner's godowns, used for live capacity/fit calculations.</summary>
        public List<OwnerGodownItem> Godowns { get; set; } = new();
    }
}
