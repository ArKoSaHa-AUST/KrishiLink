namespace KrishiLink.Models.ViewModels
{
    /// <summary>Shared compact relative-time formatting for request rows, e.g. "2h ago".</summary>
    public static class TimeAgoFormatter
    {
        public static string Format(DateTime from)
        {
            if (from == default) return string.Empty;
            var span = DateTime.Now - from;
            if (span.TotalMinutes < 60) return $"{Math.Max(1, (int)span.TotalMinutes)}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            return $"{(int)span.TotalDays}d ago";
        }
    }

    /// <summary>
    /// ViewModel for the full Equipment Rental Requests page (owner decision view).
    /// </summary>
    public class RentalRequestsViewModel
    {
        public List<RentalRequestItem> Requests { get; set; } = new();
    }

    /// <summary>A rental request row with status and expandable equipment recap.</summary>
    public class RentalRequestItem
    {
        public int Id { get; set; }
        public string FarmerName { get; set; } = string.Empty;
        public string EquipmentName { get; set; } = string.Empty;
        public string DateRange { get; set; } = string.Empty;
        public string? Note { get; set; }

        /// <summary>Pending | Accepted | Paid | Rejected | Completed | Cancelled</summary>
        public string Status { get; set; } = "Pending";

        /// <summary>Total the farmer pays (snapshot at acceptance).</summary>
        public decimal AgreedGross { get; set; }

        /// <summary>Quote breakdown and notes (e.g. '৳1,500 × 3 days + ৳2,200 × 4 days (Boro harvest peak)').</summary>
        public string? PricingNote { get; set; }

        /// <summary>Set once the farmer's payment has succeeded; drives the "Paid ৳X" chip and the Mark Completed gate.</summary>
        public string? PaymentReference { get; set; }
        public bool IsPaid => PaymentReference is not null;

        /// <summary>When the farmer submitted the request (drives sorting and "time ago").</summary>
        public DateTime RequestedOn { get; set; }

        /// <summary>Requested rental window, used for conflict detection.</summary>
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        /// <summary>Set when a pending request's dates overlap an accepted rental of the same equipment.</summary>
        public bool HasConflict { get; set; }
        public string? ConflictHint { get; set; }

        public int Units { get; set; } = 1;
        public int Quantity { get; set; } = 1;
        public int ModificationCount { get; set; } = 0;
        public string? PreviousDetails { get; set; }

        /// <summary>Owner's reason shown on rejected requests.</summary>
        public string? RejectReason { get; set; }

        // Equipment recap shown when the row is expanded
        public string EquipmentCategory { get; set; } = string.Empty;
        public string DailyRate { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        /// <summary>Compact relative timestamp, e.g. "2h ago". Empty when RequestedOn is unset.</summary>
        public string TimeAgo => TimeAgoFormatter.Format(RequestedOn);
    }
}
