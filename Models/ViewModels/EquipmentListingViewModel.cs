using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KrishiLink.Models.ViewModels
{
    public class EquipmentListingViewModel
    {
        public int? Id { get; set; }

        public bool IsEditMode => Id.HasValue && Id.Value > 0;

        [Required(ErrorMessage = "Equipment name is required.")]
        [StringLength(100, ErrorMessage = "Equipment name cannot exceed 100 characters.")]
        [Display(Name = "Equipment Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select an equipment category/type.")]
        [Display(Name = "Equipment Category")]
        public string Category { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required to help farmers understand specifications.")]
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
        [Display(Name = "Description & Specifications")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Location is required.")]
        [Display(Name = "Location / District")]
        public string Location { get; set; } = string.Empty;

        [Required(ErrorMessage = "Daily rental rate is required.")]
        [Range(1, 500000, ErrorMessage = "Daily rate must be a positive number greater than 0.")]
        [Display(Name = "Daily Rental Rate (৳)")]
        public decimal DailyRate { get; set; } = 1500;

        [Range(0, 100000, ErrorMessage = "Hourly rate must be a positive number.")]
        [Display(Name = "Hourly Rental Rate (৳, Optional)")]
        public decimal? HourlyRate { get; set; }

        [Display(Name = "Active Availability Status")]
        public bool IsAvailable { get; set; } = true;

        public List<string> ExistingImageUrls { get; set; } = new();

        public string FormTitle => IsEditMode ? "Edit Equipment Listing" : "Add New Equipment";
        public string SubmitButtonText => IsEditMode ? "Update Listing" : "Save Listing";
    }
}
