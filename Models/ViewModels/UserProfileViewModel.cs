using System.ComponentModel.DataAnnotations;

namespace KrishiLink.Models.ViewModels
{
    public class UserProfileViewModel
    {
        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone Number is required.")]
        [RegularExpression(@"^(?:\+?880|0)?1[3-9]\d{8}$", ErrorMessage = "Please enter a valid Bangladeshi phone number (e.g., 017XXXXXXXX).")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Email Address")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Location is required.")]
        [Display(Name = "Location (District / Upazila)")]
        public string Location { get; set; } = string.Empty;

        [Display(Name = "Business / Farm Name")]
        [StringLength(120, ErrorMessage = "Business or Farm Name cannot exceed 120 characters.")]
        public string? BusinessOrFarmName { get; set; }

        public string Role { get; set; } = "Farmer";

        public DateTime MemberSince { get; set; } = DateTime.UtcNow;

        public string Initials
        {
            get
            {
                if (string.IsNullOrWhiteSpace(FullName))
                    return "KL";

                var parts = FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1)
                    return parts[0].Length >= 2 ? parts[0][..2].ToUpper() : parts[0].ToUpper();

                return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
            }
        }
    }

    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Current Password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "New Password is required.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "New Password must be at least 6 characters long.")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your new password.")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        [Display(Name = "Confirm New Password")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}
