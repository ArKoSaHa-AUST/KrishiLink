using Microsoft.AspNetCore.Identity;

namespace KrishiLink.Models.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty; // "Farmer", "EquipmentOwner", "GodownOwner"
        public string? Location { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
