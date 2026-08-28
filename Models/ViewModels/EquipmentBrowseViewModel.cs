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
        public double DistanceKm { get; set; }
        public bool IsAvailable { get; set; } = true;
        public string Status => IsAvailable ? "Available" : "Unavailable";
        public string ImageUrl { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public double Rating { get; set; } = 4.8;
        public int ReviewCount { get; set; } = 12;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Main view model for the Equipment Browse and Search page.
    /// </summary>
    public class EquipmentBrowseViewModel
    {
        // Filter & Search Parameters
        public string? SearchTerm { get; set; }
        public List<string> SelectedCategories { get; set; } = new();
        public string? Location { get; set; }
        public decimal MinPrice { get; set; } = 200;
        public decimal MaxPrice { get; set; } = 5000;
        public decimal? SelectedMaxPrice { get; set; }
        public DateTime? AvailabilityDate { get; set; }
        public string SortBy { get; set; } = "newest"; // "price_asc", "price_desc", "distance", "newest"

        // Results
        public List<EquipmentItemViewModel> EquipmentList { get; set; } = new();
        public int TotalCount => EquipmentList.Count;

        // Meta lists for filters
        public List<string> AvailableCategories { get; set; } = new()
        {
            "Tractor",
            "Power Tiller",
            "Combine Harvester",
            "Seed Drill / Seeder",
            "Power Sprayer",
            "Irrigation Pump",
            "Thresher"
        };

        public List<string> AvailableLocations { get; set; } = new()
        {
            "Bogra Sadar, Bogra",
            "Sherpur, Bogra",
            "Dinajpur Sadar, Dinajpur",
            "Rangpur Sadar, Rangpur",
            "Mymensingh Sadar, Mymensingh",
            "Comilla Sadar, Comilla",
            "Jessore Sadar, Jessore",
            "Natore Sadar, Natore"
        };
    }
}
