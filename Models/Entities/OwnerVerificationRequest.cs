using System;

namespace KrishiLink.Models.Entities
{
    public class OwnerVerificationRequest
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public string NidNumber { get; set; } = string.Empty;
        public string NidFrontImagePath { get; set; } = string.Empty;
        public string? NidBackImagePath { get; set; }
        public string? TradeLicenseImagePath { get; set; }

        public string Status { get; set; } = "Pending"; // "Pending", "Approved", "Rejected"
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedByAdminId { get; set; }
        public string? RejectionReason { get; set; }
        public string? AdminNotes { get; set; }
    }
}
