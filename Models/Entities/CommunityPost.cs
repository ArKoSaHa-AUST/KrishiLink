using System;
using System.Collections.Generic;

namespace KrishiLink.Models.Entities
{
    public static class CommunityPostTypes
    {
        public const string Experience = "Experience";
        public const string HelpNeeded = "HelpNeeded";
        public const string AgritechTip = "AgritechTip";

        public static readonly string[] All = { Experience, HelpNeeded, AgritechTip };
    }

    public static class CommunityUrgencyLevels
    {
        public const string Normal = "Normal";
        public const string Moderate = "Moderate";
        public const string High = "High";

        public static readonly string[] All = { Normal, Moderate, High };
    }

    public class CommunityPost
    {
        public int Id { get; set; }

        public string AuthorId { get; set; } = string.Empty;
        public virtual ApplicationUser Author { get; set; } = null!;

        public string Content { get; set; } = string.Empty;
        public string PostType { get; set; } = CommunityPostTypes.Experience;

        // Structured Agricultural Context (Primarily for HelpNeeded posts)
        public string? CropCategory { get; set; }
        public string? IssueCategory { get; set; }
        public string UrgencyLevel { get; set; } = CommunityUrgencyLevels.Normal;
        public string? CropAge { get; set; }
        public string? AffectedArea { get; set; }

        // Geographic location tagging
        public string? District { get; set; }
        public string? Upazila { get; set; }

        // Crisis triage & resolution state
        public bool IsHelpRequest { get; set; } = false;
        public bool IsSolved { get; set; } = false;
        public int? AcceptedCommentId { get; set; }
        public virtual CommunityComment? AcceptedComment { get; set; }

        // Voice Note Audio attachment (Low-literacy rural speech recording)
        public string? AudioRecordingUrl { get; set; }
        public int? AudioDurationSeconds { get; set; }

        // Engagement counters
        public int ViewCount { get; set; } = 0;
        public int LikeCount { get; set; } = 0;
        public int CommentCount { get; set; } = 0;
        public int ShareCount { get; set; } = 0;

        // Governance & Moderation
        public bool IsPinned { get; set; } = false;
        public bool IsFlagged { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation Collections
        public virtual ICollection<PostMedia> MediaList { get; set; } = new List<PostMedia>();
        public virtual ICollection<CommunityComment> Comments { get; set; } = new List<CommunityComment>();
        public virtual ICollection<CommunityReaction> Reactions { get; set; } = new List<CommunityReaction>();
        public virtual ICollection<CommunityBookmark> Bookmarks { get; set; } = new List<CommunityBookmark>();
        public virtual ICollection<CommunityPostReport> Reports { get; set; } = new List<CommunityPostReport>();
    }
}
