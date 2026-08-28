namespace KrishiLink.Models.ViewModels
{
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

        /// <summary>Pending | Accepted | Rejected | Completed</summary>
        public string Status { get; set; } = "Pending";

        /// <summary>When the farmer submitted the request (drives sorting and "time ago").</summary>
        public DateTime RequestedOn { get; set; }

        /// <summary>Requested rental window, used for conflict detection.</summary>
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        /// <summary>Set when a pending request's dates overlap an accepted rental of the same equipment.</summary>
        public bool HasConflict { get; set; }
        public string? ConflictHint { get; set; }

        /// <summary>Owner's reason shown on rejected requests.</summary>
        public string? RejectReason { get; set; }

        // Equipment recap shown when the row is expanded
        public string EquipmentCategory { get; set; } = string.Empty;
        public string DailyRate { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        /// <summary>Compact relative timestamp, e.g. "2h ago". Empty when RequestedOn is unset.</summary>
        public string TimeAgo
        {
            get
            {
                if (RequestedOn == default) return string.Empty;
                var span = DateTime.Now - RequestedOn;
                if (span.TotalMinutes < 60) return $"{Math.Max(1, (int)span.TotalMinutes)}m ago";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
                return $"{(int)span.TotalDays}d ago";
            }
        }
    }
}
