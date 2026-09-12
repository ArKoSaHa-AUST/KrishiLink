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

        /// <summary>Cached average star rating (1.0 to 5.0); 0 when no reviews yet.</summary>
        public double AverageRating { get; set; } = 0.0;

        /// <summary>Total number of reviews received.</summary>
        public int ReviewCount { get; set; } = 0;

        public ICollection<EquipmentBooking> Bookings { get; set; } = new List<EquipmentBooking>();
        public ICollection<EquipmentBlockedDate> BlockedDates { get; set; } = new List<EquipmentBlockedDate>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}
