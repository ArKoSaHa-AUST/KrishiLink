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
        public string Status { get; set; } = "Pending";
    }
}
