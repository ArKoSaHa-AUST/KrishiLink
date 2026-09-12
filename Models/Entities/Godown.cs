namespace KrishiLink.Models.Entities
{
    public class Godown
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string StorageType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string? District { get; set; }

        /// <summary>Geographic latitude coordinate for map pin location.</summary>
        public double? Latitude { get; set; }

        /// <summary>Geographic longitude coordinate for map pin location.</summary>
        public double? Longitude { get; set; }

        public double CapacityInTons { get; set; }
        public decimal PricePerTonPerMonth { get; set; }

        /// <summary>Amenities separated by '|', e.g. "24/7 CCTV|Backup Generator".</summary>
        public string Facilities { get; set; } = string.Empty;

        /// <summary>Gallery image URLs separated by '|'; first entry is the primary image.</summary>
        public string ImageUrls { get; set; } = string.Empty;

        public string OwnerId { get; set; } = string.Empty;
        public ApplicationUser? Owner { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Cached average star rating (1.0 to 5.0); 0 when no reviews yet.</summary>
        public double AverageRating { get; set; } = 0.0;

        /// <summary>Total number of reviews received.</summary>
        public int ReviewCount { get; set; } = 0;

        public ICollection<GodownBooking> Bookings { get; set; } = new List<GodownBooking>();
        public ICollection<GodownBlockedDate> BlockedDates { get; set; } = new List<GodownBlockedDate>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}
