using System.Collections.Generic;
using System.Threading.Tasks;
using KrishiLink.Models.ViewModels;

namespace KrishiLink.BLL.Services
{
    public interface ICommunityService
    {
        Task<CommunityFeedViewModel> GetFeedAsync(
            string? currentUserId,
            string? category = "All",
            string? district = null,
            string? urgency = null,
            string? sort = "Latest",
            string? searchQuery = null,
            int pageNumber = 1,
            int pageSize = 15);

        Task<CommunityPostViewModel?> GetPostByIdAsync(int postId, string? currentUserId);

        Task<int> CreatePostAsync(string authorId, CommunityPostCreateViewModel model);

        Task<CommunityCommentViewModel?> AddCommentAsync(string authorId, CommunityCommentCreateViewModel model);

        Task<(bool Success, string Action, int LikeCount, string ReactionType)> ToggleReactionAsync(
            string userId,
            int? postId,
            int? commentId,
            string reactionType);

        Task<(bool Success, string Message)> AcceptSolutionAsync(string currentUserId, int postId, int commentId);

        Task<(bool Success, bool IsBookmarked)> ToggleBookmarkAsync(string userId, int postId);

        Task<bool> ReportPostAsync(string reporterId, int postId, string reason);

        Task<bool> DeletePostAsync(string userId, bool isAdmin, int postId);

        Task<List<EmergencyAlertItemViewModel>> GetEmergencyAlertsAsync(string? district, int limit = 3);

        Task<List<TopMentorViewModel>> GetTopMentorsAsync(int limit = 3);

        Task<CommunityWeatherAdvisorViewModel> GetWeatherAdvisorAsync(string? district);
    }
}
