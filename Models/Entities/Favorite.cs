using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KrishiLink.Models.Entities
{
    public static class ListingTypes
    {
        public const string Equipment = "Equipment";
        public const string Godown = "Godown";
    }

    public class Favorite
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }

        [Required]
        [StringLength(20)]
        public string ListingType { get; set; } = string.Empty; // "Equipment" or "Godown"

        public int ListingId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
