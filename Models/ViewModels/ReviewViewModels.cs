using System.ComponentModel.DataAnnotations;

namespace KrishiLink.Models.ViewModels
{
    public class SubmitReviewViewModel
    {
        [Required]
        public string BookingType { get; set; } = "Equipment"; // "Equipment" or "Godown"

        [Required]
        public int BookingId { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Please select a rating between 1 and 5 stars.")]
        public int Rating { get; set; } = 5;

        [MaxLength(1000, ErrorMessage = "Comment cannot exceed 1000 characters.")]
        public string? Comment { get; set; }

        // Contextual display properties for modal
        public string? ItemName { get; set; }
        public string? ImageUrl { get; set; }
        public string? OwnerName { get; set; }
        public string? BookingCode { get; set; }
    }

    public class ReviewItemViewModel
    {
        public int Id { get; set; }
        public string FarmerName { get; set; } = string.Empty;
        public string? FarmerLocation { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? OwnerReply { get; set; }
        public DateTime? OwnerRepliedAt { get; set; }
        public string? OwnerRepliedTimeAgo => OwnerRepliedAt.HasValue ? TimeAgoFormatter.Format(OwnerRepliedAt.Value) : null;
        public string FormattedDate => CreatedAt.ToString("dd MMM yyyy");
        public string TimeAgo { get; set; } = string.Empty;
        public string UserInitial => !string.IsNullOrWhiteSpace(FarmerName) ? FarmerName[0].ToString().ToUpperInvariant() : "F";
    }

    public class ReplyReviewViewModel
    {
        [Required]
        public int ReviewId { get; set; }

        [Required]
        [MaxLength(1000, ErrorMessage = "Reply cannot exceed 1000 characters.")]
        public string Reply { get; set; } = string.Empty;
    }

    public class RatingBreakdownViewModel
    {
        public int FiveStarCount { get; set; }
        public int FourStarCount { get; set; }
        public int ThreeStarCount { get; set; }
        public int TwoStarCount { get; set; }
        public int OneStarCount { get; set; }
        public int TotalReviews { get; set; }

        public int FiveStarPercent => TotalReviews > 0 ? (int)Math.Round((double)FiveStarCount / TotalReviews * 100) : 0;
        public int FourStarPercent => TotalReviews > 0 ? (int)Math.Round((double)FourStarCount / TotalReviews * 100) : 0;
        public int ThreeStarPercent => TotalReviews > 0 ? (int)Math.Round((double)ThreeStarCount / TotalReviews * 100) : 0;
        public int TwoStarPercent => TotalReviews > 0 ? (int)Math.Round((double)TwoStarCount / TotalReviews * 100) : 0;
        public int OneStarPercent => TotalReviews > 0 ? (int)Math.Round((double)OneStarCount / TotalReviews * 100) : 0;
    }

    public class ReviewsListViewModel
    {
        public double AverageRating { get; set; } = 0.0;
        public int TotalReviews { get; set; } = 0;
        public RatingBreakdownViewModel Breakdown { get; set; } = new();
        public List<ReviewItemViewModel> Reviews { get; set; } = new();
        public bool HasMoreReviews => TotalReviews > Reviews.Count;
    }
}
