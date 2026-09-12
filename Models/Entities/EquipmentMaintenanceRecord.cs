using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KrishiLink.Models.Entities
{
    /// <summary>
    /// Represents a maintenance or servicing event logged for a piece of agricultural equipment.
    /// Used to track maintenance history, parts replaced, costs, and calculate public trust indicators.
    /// </summary>
    public class EquipmentMaintenanceRecord
    {
        public int Id { get; set; }

        public int EquipmentId { get; set; }
        public Equipment? Equipment { get; set; }

        /// <summary>Date the servicing or repair was performed.</summary>
        public DateTime ServiceDate { get; set; }

        /// <summary>Type or category of maintenance (e.g., Engine Oil Change, Hydraulics, Blades, General Overhaul).</summary>
        [Required]
        [MaxLength(80)]
        public string ServiceType { get; set; } = string.Empty;

        /// <summary>Detailed notes describing what work was done, parts replaced, or diagnostic details.</summary>
        [Required]
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        /// <summary>Total cost of the maintenance event in Bangladeshi Taka (BDT ৳).</summary>
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 10000000)]
        public decimal Cost { get; set; }

        /// <summary>Workshop, service center, technician name, or 'Self' where maintenance was performed.</summary>
        [MaxLength(150)]
        public string? ServicedBy { get; set; }

        /// <summary>Timestamp when this log was created in the system.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
