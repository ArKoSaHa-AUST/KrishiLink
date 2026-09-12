using System;

namespace KrishiLink.Models.Entities
{
    public static class RateRuleKind
    {
        public const string Season = "Season";
        public const string Weekend = "Weekend";
    }

    public class EquipmentRateRule
    {
        public int Id { get; set; }
        public int EquipmentId { get; set; }
        public Equipment? Equipment { get; set; }

        /// <summary>Season | Weekend</summary>
        public string Kind { get; set; } = RateRuleKind.Season;

        /// <summary>Display name for the rule, e.g. 'Boro harvest peak' or 'Weekend Surge'.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Applicable starting date (inclusive) for seasonal rules; null for weekend rules.</summary>
        public DateTime? StartDate { get; set; }

        /// <summary>Applicable ending date (inclusive) for seasonal rules; null for weekend rules.</summary>
        public DateTime? EndDate { get; set; }

        /// <summary>Absolute daily rate in BDT (৳) per day.</summary>
        public decimal DailyRate { get; set; }

        /// <summary>Whether this rule is currently active and applied during pricing calculation.</summary>
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
