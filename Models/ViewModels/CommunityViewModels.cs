using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.Http;

namespace KrishiLink.Models.ViewModels
{
    public class CommunityFeedViewModel
    {
        public List<CommunityPostViewModel> Posts { get; set; } = new();

        public string CurrentCategory { get; set; } = "All"; // All, HelpNeeded, Experience, AgritechTip, Solved
        public string? CurrentDistrict { get; set; }
        public string? CurrentUrgency { get; set; }
        public string CurrentSort { get; set; } = "Latest"; // Latest, Popular, Urgent, Proximity
        public string? SearchQuery { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 15;
        public int TotalPostsCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalPostsCount / PageSize);

        // Sidebar category counters
        public int TotalCount { get; set; }
        public int UrgentNeedsCount { get; set; }
        public int ExperiencesCount { get; set; }
        public int TipsCount { get; set; }
        public int SolvedCount { get; set; }
        public int MyPostsCount { get; set; }
        public int MySavedCount { get; set; }

        // Current User Profile Context
        public ApplicationUser? CurrentUser { get; set; }
        public string? CurrentUserId { get; set; }
        public bool IsAuthenticated { get; set; }
        public string? UserBadgeTitle { get; set; }

        // Right Sidebar Widgets Data
        public List<EmergencyAlertItemViewModel> EmergencyAlerts { get; set; } = new();
        public CommunityWeatherAdvisorViewModel? WeatherAdvisor { get; set; }
        public List<TopMentorViewModel> TopMentors { get; set; } = new();

        // Helper Dropdowns
        public List<string> DistrictList { get; set; } = new();
        public List<string> CropCategories { get; set; } = new();
        public List<string> IssueCategories { get; set; } = new();
    }

    public class CommunityPostViewModel
    {
        public int Id { get; set; }
        public string AuthorId { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string? AuthorRole { get; set; }
        public string? AuthorAvatarUrl { get; set; }
        public bool AuthorIsVerified { get; set; }
        public string? AuthorDistrict { get; set; }

        public string Content { get; set; } = string.Empty;
        public string PostType { get; set; } = CommunityPostTypes.Experience;

        public string? CropCategory { get; set; }
        public string? IssueCategory { get; set; }
        public string UrgencyLevel { get; set; } = CommunityUrgencyLevels.Normal;
        public string? CropAge { get; set; }
        public string? AffectedArea { get; set; }

        public string? District { get; set; }
        public string? Upazila { get; set; }

        public bool IsHelpRequest { get; set; }
        public bool IsSolved { get; set; }

        public string? AudioRecordingUrl { get; set; }
        public int? AudioDurationSeconds { get; set; }

        public int ViewCount { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public int ShareCount { get; set; }
        public bool IsPinned { get; set; }

        public DateTime CreatedAt { get; set; }
        public string TimeAgo { get; set; } = string.Empty;

        // Media items
        public List<PostMediaViewModel> MediaList { get; set; } = new();

        // Accepted Solution (if any)
        public CommunityCommentViewModel? AcceptedComment { get; set; }

        // User specific state for this post
        public bool IsLikedByCurrentUser { get; set; }
        public string? CurrentUserReactionType { get; set; }
        public bool IsBookmarkedByCurrentUser { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool CanAcceptSolution { get; set; }

        // Comments
        public List<CommunityCommentViewModel> Comments { get; set; } = new();
    }

    public class PostMediaViewModel
    {
        public int Id { get; set; }
        public string MediaUrl { get; set; } = string.Empty;
        public string MediaType { get; set; } = PostMediaTypes.Image;
        public string? ThumbnailUrl { get; set; }
        public int SortOrder { get; set; }
    }

    public class CommunityCommentViewModel
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public string AuthorId { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string? AuthorRole { get; set; }
        public string? AuthorAvatarUrl { get; set; }
        public bool AuthorIsVerified { get; set; }
        public bool IsOfficialExpert { get; set; }

        public string Content { get; set; } = string.Empty;
        public string? AttachmentImageUrl { get; set; }
        public string? AudioRecordingUrl { get; set; }
        public int? AudioDurationSeconds { get; set; }

        public int? ParentCommentId { get; set; }
        public bool IsAcceptedSolution { get; set; }
        public int UpvoteCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public string TimeAgo { get; set; } = string.Empty;

        public bool IsUpvotedByCurrentUser { get; set; }
        public bool CanAcceptThisComment { get; set; }

        public List<CommunityCommentViewModel> Replies { get; set; } = new();
    }

    public class CommunityPostCreateViewModel
    {
        [Required(ErrorMessage = "পোস্টের বিবরণ আবশ্যক")]
        [StringLength(3000, MinimumLength = 5, ErrorMessage = "পোস্টটি ৫ থেকে ৩০০০ অক্ষরের মধ্যে হতে হবে")]
        public string Content { get; set; } = string.Empty;

        public string PostType { get; set; } = CommunityPostTypes.Experience;

        public string? CropCategory { get; set; }
        public string? IssueCategory { get; set; }
        public string UrgencyLevel { get; set; } = CommunityUrgencyLevels.Normal;
        public string? CropAge { get; set; }
        public string? AffectedArea { get; set; }

        public string? District { get; set; }
        public string? Upazila { get; set; }

        // Uploaded files
        public List<IFormFile>? ImageFiles { get; set; }
        public IFormFile? VideoFile { get; set; }
        public IFormFile? AudioFile { get; set; }
        public int? AudioDurationSeconds { get; set; }
    }

    public class CommunityCommentCreateViewModel
    {
        [Required]
        public int PostId { get; set; }

        public int? ParentCommentId { get; set; }

        [Required(ErrorMessage = "মন্তব্য বা সমাধান লিখুন")]
        [StringLength(1500, MinimumLength = 2, ErrorMessage = "মন্তব্যটি ২ থেকে ১৫০০ অক্ষরের মধ্যে হতে হবে")]
        public string Content { get; set; } = string.Empty;

        public IFormFile? ImageFile { get; set; }
        public IFormFile? AudioFile { get; set; }
        public int? AudioDurationSeconds { get; set; }
    }

    public class EmergencyAlertItemViewModel
    {
        public int PostId { get; set; }
        public string CropName { get; set; } = string.Empty;
        public string IssueTitle { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string TimeAgo { get; set; } = string.Empty;
        public string UrgencyLevel { get; set; } = CommunityUrgencyLevels.High;
    }

    public class CommunityWeatherAdvisorViewModel
    {
        public string District { get; set; } = "Dhaka";
        public double Temperature { get; set; }
        public int Humidity { get; set; }
        public string ConditionText { get; set; } = "Clear";
        public bool IsSprayFavorable { get; set; } = true;
        public string SprayAdviceText { get; set; } = string.Empty;
    }

    public class TopMentorViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Role { get; set; }
        public string? AvatarUrl { get; set; }
        public bool IsVerified { get; set; }
        public int Points { get; set; }
        public int SolutionsCount { get; set; }
        public int Rank { get; set; }
    }
}
