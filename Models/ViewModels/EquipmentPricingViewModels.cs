using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using KrishiLink.Models.Entities;

namespace KrishiLink.Models.ViewModels
{
    public class RateRuleInputModel
    {
        public int? Id { get; set; }

        public int EquipmentId { get; set; }

        [Required(ErrorMessage = "Please select a rule kind (Season or Weekend).")]
        public string Kind { get; set; } = RateRuleKind.Season;

        [Required(ErrorMessage = "Rule name is required.")]
        [StringLength(60, ErrorMessage = "Rule name cannot exceed 60 characters.")]
        public string Name { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Required(ErrorMessage = "Daily rate is required.")]
        [Range(1, 500000, ErrorMessage = "Daily rate must be between ৳1 and ৳500,000.")]
        public decimal DailyRate { get; set; } = 1500;

        public bool IsActive { get; set; } = true;
    }

    public class EquipmentRateRuleViewModel
    {
        public int Id { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string DateRangeText { get; set; } = string.Empty;
        public decimal DailyRate { get; set; }
        public string RateText { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class EquipmentPricingViewModel
    {
        public int EquipmentId { get; set; }
        public string ListingName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public decimal BaseDailyRate { get; set; }
        public string RateText { get; set; } = string.Empty;
        public int MinRentalDays { get; set; } = 1;
        public List<EquipmentRateRuleViewModel> Rules { get; set; } = new();
        public RateRuleInputModel NewRule { get; set; } = new();
    }

    public class EquipmentQuote
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public int Days { get; set; }
        public decimal Gross { get; set; }
        public int MinDays { get; set; }
        public int Units { get; set; } = 1;
        public int FreeUnits { get; set; } = 1;
        public int Quantity { get; set; } = 1;
        public List<RateSegmentBreakdown> Breakdown { get; set; } = new();
        public string Description { get; set; } = string.Empty;
    }

    public class RateSegmentBreakdown
    {
        public decimal Rate { get; set; }
        public int Days { get; set; }
        public string Label { get; set; } = string.Empty;
        public decimal Subtotal => Rate * Days;
    }
}
