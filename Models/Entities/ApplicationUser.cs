using Microsoft.AspNetCore.Identity;

namespace KrishiLink.Models.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty; // "Farmer", "EquipmentOwner", "GodownOwner"
        public string? Location { get; set; }
        public string? BusinessOrFarmName { get; set; }

        // Captured during post-registration onboarding
        public string? District { get; set; }

        /// <summary>Main crop for farmers; type of equipment or storage offered for owners.</summary>
        public string? Specialization { get; set; }
        public DateTime? OnboardingCompletedAt { get; set; }

        /// <summary>First day of the last month whose statement was emailed by the scheduler (owners only).</summary>
        public DateTime? LastStatementSentMonth { get; set; }

        // Owner & Identity Verification (NID / Trade License)
        public bool IsVerified { get; set; } = false;
        public string VerificationStatus { get; set; } = "Unverified"; // "Unverified", "Pending", "Verified", "Rejected"
        public string? NidNumber { get; set; }
        public string? NidFrontImagePath { get; set; }
        public string? NidBackImagePath { get; set; }
        public string? TradeLicenseImagePath { get; set; }
        public DateTime? VerificationSubmittedAt { get; set; }
        public DateTime? VerificationReviewedAt { get; set; }
        public string? VerificationRejectionReason { get; set; }
        public string? VerificationNotes { get; set; }

        // Loyalty Rewards & Points (KrishiPoints for Farmers)
        public int LoyaltyPoints { get; set; } = 0;

        // Owner Rating Aggregates (Equipment & Godown Owners)
        public double OwnerAverageRating { get; set; } = 0.0;
        public int OwnerReviewCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Returns true if the user has a real, deliverable email address (excluding .local phone placeholders).</summary>
        public bool HasRealEmail() => HasRealEmail(this);

        /// <summary>Returns true if the given user or email is deliverable (excluding null/empty and @krishilink.local).</summary>
        public static bool HasRealEmail(ApplicationUser? u) => u != null && HasRealEmail(u.Email);

        /// <summary>Returns true if the email is deliverable (excluding null/empty and @krishilink.local).</summary>
        public static bool HasRealEmail(string? email) =>
            !string.IsNullOrWhiteSpace(email) && !email.Trim().EndsWith("@krishilink.local", StringComparison.OrdinalIgnoreCase);
    }
}
