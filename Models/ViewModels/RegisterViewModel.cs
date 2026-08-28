using System.ComponentModel.DataAnnotations;

namespace KrishiLink.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Please select an account role.")]
        [Display(Name = "Account Role")]
        public string Role { get; set; } = "Farmer"; // "Farmer", "EquipmentOwner", "GodownOwner"

        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone Number is required.")]
        [RegularExpression(@"^(?:\+?880|0)?1[3-9]\d{8}$", ErrorMessage = "Please enter a valid Bangladeshi phone number (e.g., 017XXXXXXXX).")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Email (Optional)")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Please select or specify your location.")]
        [Display(Name = "Location (District / Upazila)")]
        public string Location { get; set; } = string.Empty;

        [Display(Name = "Business / Farm Name")]
        [StringLength(120, ErrorMessage = "Business or Farm Name cannot exceed 120 characters.")]
        public string? BusinessOrFarmName { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your password.")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
