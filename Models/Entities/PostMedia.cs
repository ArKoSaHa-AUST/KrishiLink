using System;

namespace KrishiLink.Models.Entities
{
    public static class PostMediaTypes
    {
        public const string Image = "Image";
        public const string Video = "Video";
        public const string Audio = "Audio";

        public static readonly string[] All = { Image, Video, Audio };
    }

    public class PostMedia
    {
        public int Id { get; set; }

        public int PostId { get; set; }
        public virtual CommunityPost Post { get; set; } = null!;

        public string MediaUrl { get; set; } = string.Empty;
        public string MediaType { get; set; } = PostMediaTypes.Image;
        public string? ThumbnailUrl { get; set; }
        public int SortOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
