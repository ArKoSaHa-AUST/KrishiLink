using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KrishiLink.Models.Entities
{
    public class SavedSearch
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }

        [Required]
        [StringLength(80)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string ListingType { get; set; } = string.Empty; // "Equipment" or "Godown"

        [StringLength(100)]
        public string? SearchTerm { get; set; }

        [StringLength(50)]
        public string? Category { get; set; } // Equipment Category or Godown StorageType

        [StringLength(60)]
        public string? District { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaxRate { get; set; }

        public double? MinCapacityTons { get; set; }

        public DateTime? From { get; set; }

        public DateTime? To { get; set; }

        public bool AlertsEnabled { get; set; } = true;

        public DateTime? LastRunAt { get; set; }

        public DateTime? LastAlertedAt { get; set; }

        [StringLength(2000)]
        public string KnownListingIds { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
