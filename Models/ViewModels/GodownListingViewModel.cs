using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace KrishiLink.Models.ViewModels
{
    public class GodownListingViewModel
    {
        public int Id { get; set; }

        public bool IsEditMode => Id > 0;

        public string FormTitle => IsEditMode ? "Edit Storage Facility" : "Add Storage Facility";

        public string SubmitButtonText => IsEditMode ? "Update Listing" : "Save Listing";

        [Required(ErrorMessage = "Godown / Warehouse title is required.")]
        [StringLength(120, ErrorMessage = "Godown name cannot exceed 120 characters.")]
        [Display(Name = "Facility Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a storage category.")]
        [Display(Name = "Storage Category")]
        public string Category { get; set; } = "Cold Storage"; // Cold Storage, Grain Warehouse, Seed Vault, Silo Facility, Dry Godown

        [Required(ErrorMessage = "Location is required.")]
        [Display(Name = "Location (District / Upazila)")]
        public string Location { get; set; } = string.Empty;

        [Required(ErrorMessage = "Total storage capacity is required.")]
        [Range(0.1, 100000, ErrorMessage = "Total capacity must be a positive number greater than 0.")]
        [Display(Name = "Total Capacity")]
        public double TotalCapacity { get; set; } = 100;

        [Required(ErrorMessage = "Capacity unit is required.")]
        [Display(Name = "Capacity Unit")]
        public string CapacityUnit { get; set; } = "Tons"; // Tons, Maunds, Bags (50kg), Quintals

        [Range(0, 100000, ErrorMessage = "Available capacity cannot be negative.")]
        [Display(Name = "Available Capacity")]
        public double AvailableCapacity { get; set; } = 100;

        [Required(ErrorMessage = "Price amount is required.")]
        [Range(1, 1000000, ErrorMessage = "Rental price must be at least 1 BDT.")]
        [Display(Name = "Rental Rate (BDT ৳)")]
        public decimal PriceAmount { get; set; } = 450;

        [Display(Name = "Pricing Period")]
        public string PricePeriod { get; set; } = "Month"; // "Month" or "Day"

        [Required(ErrorMessage = "Please provide facility specifications and description.")]
        [StringLength(2000, MinimumLength = 15, ErrorMessage = "Description must be between 15 and 2000 characters.")]
        [Display(Name = "Description & Specifications")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Mark Listing as Active & Bookable")]
        public bool IsAvailable { get; set; } = true;

        [Display(Name = "Selected Facility Amenities")]
        public List<string> SelectedFacilities { get; set; } = new();

        public List<string> ExistingImageUrls { get; set; } = new();

        public List<IFormFile>? ImageFiles { get; set; }
    }
}
