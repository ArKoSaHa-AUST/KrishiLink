namespace KrishiLink.Models.Entities
{
    public class EquipmentBooking
    {
        public int Id { get; set; }
        public int EquipmentId { get; set; }
        public Equipment? Equipment { get; set; }
        public string FarmerId { get; set; } = string.Empty;
        public ApplicationUser? Farmer { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Note { get; set; }
        public string Status { get; set; } = BookingStatus.Pending;
        public string? RejectReason { get; set; }
        public DateTime RequestedOn { get; set; } = DateTime.UtcNow;

        /// <summary>When the owner last changed the status (accept/reject/complete).</summary>
        public DateTime? UpdatedOn { get; set; }
    }
}
