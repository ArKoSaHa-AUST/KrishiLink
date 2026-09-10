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

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
