using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace KrishiLink.Models.ViewModels
{
    /// <summary>
    /// ViewModel for owner NID and document verification submissions (/Account/Verification).
    /// </summary>
    public class OwnerVerificationViewModel
    {
        // Owner Profile Details
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? BusinessOrFarmName { get; set; }
        public string? Location { get; set; }

        // NID Input & Validation
        [Required(ErrorMessage = "National ID (NID) Number is required.")]
        [RegularExpression(@"^(?:\d{10}|\d{13}|\d{17})$", ErrorMessage = "Please enter a valid Bangladeshi NID Number (10-digit Smart NID, or 13/17-digit legacy NID).")]
        [Display(Name = "National ID (NID) Number")]
        public string NidNumber { get; set; } = string.Empty;
        public string? MaskedNidNumber { get; set; }
        public string? NidLast4 { get; set; }

        // Document Uploads
        [Display(Name = "NID Card Front Photo")]
        public IFormFile? NidFrontImage { get; set; }

        [Display(Name = "NID Card Back Photo")]
        public IFormFile? NidBackImage { get; set; }

        [Display(Name = "Trade License / Business Document (Optional)")]
        public IFormFile? TradeLicenseImage { get; set; }

        // Existing Saved Document Paths
        public string? ExistingNidFrontUrl { get; set; }
        public string? ExistingNidBackUrl { get; set; }
        public string? ExistingTradeLicenseUrl { get; set; }

        // Verification Status & Timestamps
        public string VerificationStatus { get; set; } = "Unverified"; // "Unverified", "Pending", "Verified", "Rejected"
        public bool IsVerified { get; set; } = false;
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? RejectionReason { get; set; }
        public string? AdminNotes { get; set; }

        // Helper presentation properties
        public bool IsPending => VerificationStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase);
        public bool IsRejected => VerificationStatus.Equals("Rejected", StringComparison.OrdinalIgnoreCase);
        public bool IsUnverified => VerificationStatus.Equals("Unverified", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ViewModel for admin review or demo simulated approval/rejection.
    /// </summary>
    public class VerificationReviewSubmitModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string Decision { get; set; } = "Approve"; // "Approve", "Reject", "Reset"

        [StringLength(500)]
        public string? Reason { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
