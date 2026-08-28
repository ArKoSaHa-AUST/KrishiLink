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

        // Equipment recap shown when the row is expanded
        public string EquipmentCategory { get; set; } = string.Empty;
        public string DailyRate { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }
}
