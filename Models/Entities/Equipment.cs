namespace KrishiLink.Models.Entities
{
    public class Equipment
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public decimal DailyRate { get; set; }
        public decimal? HourlyRate { get; set; }

        /// <summary>Gallery image URLs separated by '|'; first entry is the primary image.</summary>
        public string ImageUrls { get; set; } = string.Empty;

        public string OwnerId { get; set; } = string.Empty;
        public ApplicationUser? Owner { get; set; }
        public bool IsAvailable { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<EquipmentBooking> Bookings { get; set; } = new List<EquipmentBooking>();
        public ICollection<EquipmentBlockedDate> BlockedDates { get; set; } = new List<EquipmentBlockedDate>();
    }
}
