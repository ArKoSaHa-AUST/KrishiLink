using System;
using System.Collections.Generic;

namespace KrishiLink.Models.ViewModels
{
    public class FavoriteToggleRequest
    {
        public string Type { get; set; } = string.Empty;
        public int Id { get; set; }
    }

    public class FavoriteItemViewModel
    {
        public int FavoriteId { get; set; }
        public int ListingId { get; set; }
        public string ListingType { get; set; } = string.Empty; // "Equipment" or "Godown"
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? Location { get; set; }
        public string? District { get; set; }
        public string? ImageUrl { get; set; }
        public string PriceFormatted { get; set; } = string.Empty;
        public string Status { get; set; } = "Available";
        public string DetailUrl { get; set; } = string.Empty;
        public string AvailabilityHint { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public double Rating { get; set; }
        public int ReviewCount { get; set; }
        public double OwnerRating { get; set; }
        public int OwnerReviewCount { get; set; }
        public DateTime AddedAt { get; set; }
    }

    public class FavoritesViewModel
    {
        public List<FavoriteItemViewModel> EquipmentFavorites { get; set; } = new();
        public List<FavoriteItemViewModel> GodownFavorites { get; set; } = new();
        public int TotalCount => EquipmentFavorites.Count + GodownFavorites.Count;
    }
}
