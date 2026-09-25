using System;
using System.Collections.Generic;

namespace KrishiLink.Models.Entities
{
    public static class HarvestPlanStatus
    {
        public const string Draft = "Draft";
        public const string Submitted = "Submitted";
        public const string Closed = "Closed";
    }

    public static class HarvestPlanItemType
    {
        public const string Equipment = "Equipment";
        public const string Godown = "Godown";
    }

    /// <summary>
    /// A bundled plan / cart of equipment rentals and godown storage facilities
    /// planned by a farmer for a specific crop harvest season.
    /// </summary>
    public class HarvestPlan
    {
        public int Id { get; set; }

        public string FarmerId { get; set; } = string.Empty;
        public ApplicationUser? Farmer { get; set; }

        /// <summary>Name of the plan (e.g. "Boro Harvest 2027", "Aman Field Preparation").</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional crop target (e.g. "Rice (Boro)", "Wheat", "Potato").</summary>
        public string? Crop { get; set; }

        /// <summary>The exact crop calendar entry, when known; <see cref="Crop"/> alone may name a group such as "Pulses".</summary>
        public int? CropCalendarEntryId { get; set; }
        public CropCalendarEntry? CropEntry { get; set; }

        /// <summary>Land under this crop, in decimals (100 = 1 acre). Drives cost per acre and expected yield (ECO-01).</summary>
        public double? LandSizeDecimal { get; set; }

        /// <summary>The farmer's own expected selling price per kg of harvest. Their estimate, not a market feed.</summary>
        public decimal? ExpectedPricePerKg { get; set; }

        /// <summary>Farmer's internal notes or instructions for the plan.</summary>
        public string? Note { get; set; }

        /// <summary>Draft | Submitted | Closed</summary>
        public string Status { get; set; } = HarvestPlanStatus.Draft;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Timestamp when the plan was submitted as individual booking requests.</summary>
        public DateTime? SubmittedOn { get; set; }

        public ICollection<HarvestPlanItem> Items { get; set; } = new List<HarvestPlanItem>();

        /// <summary>Off-platform costs (seed, fertilizer, labour) for the season cost sheet.</summary>
        public ICollection<SeasonCost> Costs { get; set; } = new List<SeasonCost>();
    }

    /// <summary>
    /// An individual equipment rental or godown storage item inside a harvest plan.
    /// </summary>
    public class HarvestPlanItem
    {
        public int Id { get; set; }

        public int HarvestPlanId { get; set; }
        public HarvestPlan? Plan { get; set; }

        /// <summary>"Equipment" or "Godown"</summary>
        public string ItemType { get; set; } = string.Empty;

        /// <summary>ID of the Equipment or Godown listing.</summary>
        public int ListingId { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        /// <summary>Number of equipment units requested (ignored for Godown).</summary>
        public int Units { get; set; } = 1;

        /// <summary>Storage capacity requested in metric tons (ignored for Equipment).</summary>
        public double Tons { get; set; } = 0;

        /// <summary>Specific requirements or note for this asset.</summary>
        public string? Note { get; set; }

        /// <summary>ID of the created EquipmentBooking or GodownBooking once submitted.</summary>
        public int? BookingId { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}
