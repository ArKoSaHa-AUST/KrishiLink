using System;
using System.Collections.Generic;

namespace KrishiLink.Models.ViewModels
{
    /// <summary>
    /// Item view model for equipment browse/search results grid.
    /// </summary>
    public class EquipmentItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // Tractor, Power Tiller, Harvester, Seeder, Sprayer, etc.
        public decimal DailyRate { get; set; }
        public decimal? HourlyRate { get; set; }
        public string Location { get; set; } = string.Empty;
        public string? District { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double DistanceKm { get; set; }
        public bool IsAvailable { get; set; } = true;
        public bool HasRateRules { get; set; } = false;
        public string Status => IsAvailable ? "Available" : "Unavailable";
        public string ImageUrl { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public bool OwnerIsVerified { get; set; } = false;
        public string OwnerVerificationStatus { get; set; } = "Unverified";
        public double Rating { get; set; }
        public int ReviewCount { get; set; }
        public double OwnerRating { get; set; }
        public int OwnerReviewCount { get; set; }
        public int? LastServicedDaysAgo { get; set; }
        public string? LastServicedText { get; set; }
        public int Quantity { get; set; } = 1;
        public bool IsFavorite { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Query-string bound search/filter/sort parameters for the equipment browse page.</summary>
    public class EquipmentSearchCriteria
    {
        public string? SearchTerm { get; set; }
        public List<string>? SelectedCategories { get; set; }
        public string? District { get; set; }
        public string? Location { get; set; }
        public decimal? SelectedMinPrice { get; set; }
        public decimal? SelectedMaxPrice { get; set; }
        public DateTime? AvailabilityDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Units { get; set; } = 1;
        public string SortBy { get; set; } = "newest";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 24;

        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public string? CurrentUserId { get; set; }
    }

    /// <summary>
    /// Main view model for the Equipment Browse and Search page.
    /// </summary>
    public class EquipmentBrowseViewModel
    {
        // Filter & Search Parameters
        public string? SearchTerm { get; set; }
        public List<string> SelectedCategories { get; set; } = new();
        public string? District { get; set; }
        public string? Location { get; set; }
        public decimal MinPrice { get; set; } = 200;
        public decimal MaxPrice { get; set; } = 5000;
        public decimal? SelectedMinPrice { get; set; }
        public decimal? SelectedMaxPrice { get; set; }
        public DateTime? AvailabilityDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Units { get; set; } = 1;
        public string SortBy { get; set; } = "newest"; // "price_asc", "price_desc", "location", "rating_desc", "newest"

        // Results
        public List<EquipmentItemViewModel> EquipmentList { get; set; } = new();
        public HashSet<int> FavoriteIds { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 24;
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;

        // Meta lists for filters
        public List<string> AvailableCategories { get; set; } = new(OnboardingOptions.EquipmentCategories);

        public List<string> AvailableLocations { get; set; } = new(OnboardingOptions.Districts);
    }
}
