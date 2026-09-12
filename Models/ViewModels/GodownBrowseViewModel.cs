using System;
using System.Collections.Generic;

namespace KrishiLink.Models.ViewModels
{
    /// <summary>
    /// Item view model for individual godown / storage facility in the browse grid.
    /// </summary>
    public class GodownItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string StorageType { get; set; } = "Grain Warehouse"; // Cold Storage, Grain Warehouse, Silo Facility, Dry Godown, Pest Controlled
        public string Location { get; set; } = string.Empty;
        public string? District { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double DistanceKm { get; set; }
        public double TotalCapacityTons { get; set; }
        public double AvailableCapacityTons { get; set; }
        public decimal PricePerTonPerMonth { get; set; }
        public decimal? DailyRatePerTon => Math.Round(PricePerTonPerMonth / 30m, 2);
        public bool IsAvailable => AvailableCapacityTons > 0;
        public string Status => IsAvailable ? "Available" : "Fully Booked";
        public string ImageUrl { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public bool OwnerIsVerified { get; set; } = false;
        public string OwnerVerificationStatus { get; set; } = "Unverified";
        public double Rating { get; set; }
        public int ReviewCount { get; set; }
        public double OwnerRating { get; set; }
        public int OwnerReviewCount { get; set; }
        public List<string> Facilities { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Query-string bound search/filter/sort parameters for the godown browse page.</summary>
    public class GodownSearchCriteria
    {
        public string? SearchTerm { get; set; }
        public List<string>? SelectedStorageTypes { get; set; }
        public string? District { get; set; }
        public string? Location { get; set; }
        public double? SelectedMinCapacity { get; set; }
        public decimal? SelectedMinPrice { get; set; }
        public decimal? SelectedMaxPrice { get; set; }
        public DateTime? AvailableStartDate { get; set; }
        public DateTime? AvailableEndDate { get; set; }
        public string SortBy { get; set; } = "newest";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 24;
    }

    /// <summary>
    /// Master view model for Godown Browse & Search page.
    /// </summary>
    public class GodownBrowseViewModel
    {
        // Filter & Search Parameters
        public string? SearchTerm { get; set; }
        public List<string> SelectedStorageTypes { get; set; } = new();
        public string? District { get; set; }
        public string? Location { get; set; }
        public double MinCapacityTons { get; set; } = 1;
        public double MaxCapacityTons { get; set; } = 500;
        public double? SelectedMinCapacity { get; set; }

        public decimal MinPrice { get; set; } = 100;
        public decimal MaxPrice { get; set; } = 2500;
        public decimal? SelectedMinPrice { get; set; }
        public decimal? SelectedMaxPrice { get; set; }

        public DateTime? AvailableStartDate { get; set; }
        public DateTime? AvailableEndDate { get; set; }
        public string SortBy { get; set; } = "newest"; // "price_asc", "price_desc", "location", "capacity_desc", "rating_desc", "newest"

        // Results
        public List<GodownItemViewModel> GodownList { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 24;
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;

        // Filter Metadata
        public List<string> AvailableStorageTypes { get; set; } = new(OnboardingOptions.StorageTypes);

        public List<string> AvailableLocations { get; set; } = new(OnboardingOptions.Districts);
    }
}
