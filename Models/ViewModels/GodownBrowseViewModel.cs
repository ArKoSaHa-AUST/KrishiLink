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
        public double DistanceKm { get; set; }
        public double TotalCapacityTons { get; set; }
        public double AvailableCapacityTons { get; set; }
        public decimal PricePerTonPerMonth { get; set; }
        public decimal? DailyRatePerTon => Math.Round(PricePerTonPerMonth / 30m, 2);
        public bool IsAvailable => AvailableCapacityTons > 0;
        public string Status => IsAvailable ? "Available" : "Fully Booked";
        public string ImageUrl { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public double Rating { get; set; } = 4.8;
        public int ReviewCount { get; set; } = 15;
        public List<string> Facilities { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Master view model for Godown Browse & Search page.
    /// </summary>
    public class GodownBrowseViewModel
    {
        // Filter & Search Parameters
        public string? SearchTerm { get; set; }
        public List<string> SelectedStorageTypes { get; set; } = new();
        public string? Location { get; set; }
        public double MinCapacityTons { get; set; } = 1;
        public double MaxCapacityTons { get; set; } = 500;
        public double? SelectedMinCapacity { get; set; }

        public decimal MinPrice { get; set; } = 100;
        public decimal MaxPrice { get; set; } = 2500;
        public decimal? SelectedMaxPrice { get; set; }

        public DateTime? AvailableStartDate { get; set; }
        public DateTime? AvailableEndDate { get; set; }
        public string SortBy { get; set; } = "newest"; // "price_asc", "price_desc", "distance", "capacity_desc", "newest"

        // Results
        public List<GodownItemViewModel> GodownList { get; set; } = new();
        public int TotalCount => GodownList.Count;

        // Filter Metadata
        public List<string> AvailableStorageTypes { get; set; } = new()
        {
            "Cold Storage (Vegetable & Fruit)",
            "Grain Warehouse (Paddy & Wheat)",
            "Multi-Chamber Cold Storage",
            "Seed & Fertilizer Godown",
            "Dry Goods Warehouse",
            "Jute & Crop Storage"
        };

        public List<string> AvailableLocations { get; set; } = new()
        {
            "Dinajpur Sadar, Dinajpur",
            "Bogra Sadar, Bogra",
            "Sherpur, Bogra",
            "Rangpur Sadar, Rangpur",
            "Mymensingh Sadar, Mymensingh",
            "Jessore Sadar, Jessore",
            "Rajshahi Sadar, Rajshahi",
            "Comilla Sadar, Comilla"
        };
    }
}
