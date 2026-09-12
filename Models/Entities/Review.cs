using System.ComponentModel.DataAnnotations;

namespace KrishiLink.Models.Entities
{
    public class Review
    {
        public int Id { get; set; }

        public string FarmerId { get; set; } = string.Empty;
        public ApplicationUser? Farmer { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>"Equipment" or "Godown"</summary>
        [MaxLength(20)]
        public string BookingType { get; set; } = "Equipment";

        // Equipment Target
        public int? EquipmentId { get; set; }
        public Equipment? Equipment { get; set; }

        // Godown Target
        public int? GodownId { get; set; }
        public Godown? Godown { get; set; }

        // Booking linkage (1-to-1)
        public int? EquipmentBookingId { get; set; }
        public EquipmentBooking? EquipmentBooking { get; set; }

        public int? GodownBookingId { get; set; }
        public GodownBooking? GodownBooking { get; set; }
    }
}
