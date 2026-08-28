namespace KrishiLink.Models.Entities
{
    public class GodownBooking
    {
        public int Id { get; set; }
        public int GodownId { get; set; }
        public Godown? Godown { get; set; }
        public string FarmerId { get; set; } = string.Empty;
        public ApplicationUser? Farmer { get; set; }
        public double StorageTons { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = "Pending";
    }
}
