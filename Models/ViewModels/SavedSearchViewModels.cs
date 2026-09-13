using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using KrishiLink.Models.Entities;

namespace KrishiLink.Models.ViewModels
{
    public class SavedSearchInput
    {
        [Required(ErrorMessage = "Please provide a name for this search")]
        [StringLength(80, ErrorMessage = "Search name cannot exceed 80 characters")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string ListingType { get; set; } = ListingTypes.Equipment;

        [StringLength(100)]
        public string? SearchTerm { get; set; }

        [StringLength(50)]
        public string? Category { get; set; }

        [StringLength(60)]
        public string? District { get; set; }

        public decimal? MaxRate { get; set; }

        public double? MinCapacityTons { get; set; }

        public DateTime? From { get; set; }

        public DateTime? To { get; set; }

        public bool AlertsEnabled { get; set; } = true;

        public string? ReturnUrl { get; set; }
    }

    public class SavedSearchItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ListingType { get; set; } = ListingTypes.Equipment;
        public string? SearchTerm { get; set; }
        public string? Category { get; set; }
        public string? District { get; set; }
        public decimal? MaxRate { get; set; }
        public double? MinCapacityTons { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public bool AlertsEnabled { get; set; }
        public DateTime? LastRunAt { get; set; }
        public DateTime? LastAlertedAt { get; set; }
        public int MatchedCount { get; set; }
        public string BrowseUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public bool IsExpired => To.HasValue && To.Value.Date < DateTime.Today;

        public string DateRangeDisplay => (From.HasValue && To.HasValue)
            ? $"{From.Value:dd MMM} – {To.Value:dd MMM yyyy}"
            : From.HasValue
                ? $"From {From.Value:dd MMM yyyy}"
                : To.HasValue
                    ? $"Until {To.Value:dd MMM yyyy}"
                    : string.Empty;
    }

    public class SavedSearchListViewModel
    {
        public List<SavedSearchItemViewModel> Searches { get; set; } = new();
        public int TotalCount => Searches.Count;
        public int MaxAllowed => 10;
        public bool CanAddMore => Searches.Count < MaxAllowed;
    }
}
