using System;

namespace KrishiLink.Models.Entities
{
    public class CommunityBookmark
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser User { get; set; } = null!;

        public int PostId { get; set; }
        public virtual CommunityPost Post { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CommunityPostReport
    {
        public int Id { get; set; }

        public string ReporterId { get; set; } = string.Empty;
        public virtual ApplicationUser Reporter { get; set; } = null!;

        public int PostId { get; set; }
        public virtual CommunityPost Post { get; set; } = null!;

        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // Pending, Reviewed, Dismissed

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
