using System;
using System.Collections.Generic;

namespace KrishiLink.Models.Entities
{
    public class CommunityComment
    {
        public int Id { get; set; }

        public int PostId { get; set; }
        public virtual CommunityPost Post { get; set; } = null!;

        public string AuthorId { get; set; } = string.Empty;
        public virtual ApplicationUser Author { get; set; } = null!;

        public string Content { get; set; } = string.Empty;
        public string? AttachmentImageUrl { get; set; }
        public string? AudioRecordingUrl { get; set; }
        public int? AudioDurationSeconds { get; set; }

        // Threaded reply support (1-level nesting)
        public int? ParentCommentId { get; set; }
        public virtual CommunityComment? ParentComment { get; set; }
        public virtual ICollection<CommunityComment> Replies { get; set; } = new List<CommunityComment>();

        public bool IsAcceptedSolution { get; set; } = false;
        public int UpvoteCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<CommunityReaction> Reactions { get; set; } = new List<CommunityReaction>();
    }
}
