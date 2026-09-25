using System;

namespace KrishiLink.Models.Entities
{
    public static class CommunityReactionTypes
    {
        public const string Helpful = "Helpful"; // 🌾
        public const string Like = "Like";       // 👍
        public const string Love = "Love";       // ❤️
        public const string Insightful = "Insightful"; // 💡

        public static readonly string[] All = { Helpful, Like, Love, Insightful };
    }

    public class CommunityReaction
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser User { get; set; } = null!;

        public int? PostId { get; set; }
        public virtual CommunityPost? Post { get; set; }

        public int? CommentId { get; set; }
        public virtual CommunityComment? Comment { get; set; }

        public string ReactionType { get; set; } = CommunityReactionTypes.Helpful;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
