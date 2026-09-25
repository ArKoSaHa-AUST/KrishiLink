using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KrishiLink.BLL.Helpers;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.BLL.Services
{
    public class CommunityService : ICommunityService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly INotificationService _notifications;
        private readonly IWeatherService _weatherService;

        public CommunityService(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env,
            INotificationService notifications,
            IWeatherService weatherService)
        {
            _db = db;
            _userManager = userManager;
            _env = env;
            _notifications = notifications;
            _weatherService = weatherService;
        }

        public async Task<CommunityFeedViewModel> GetFeedAsync(
            string? currentUserId,
            string? category = "All",
            string? district = null,
            string? urgency = null,
            string? sort = "Latest",
            string? searchQuery = null,
            int pageNumber = 1,
            int pageSize = 15)
        {
            category = string.IsNullOrWhiteSpace(category) ? "All" : category;
            sort = string.IsNullOrWhiteSpace(sort) ? "Latest" : sort;

            var user = !string.IsNullOrEmpty(currentUserId)
                ? await _userManager.FindByIdAsync(currentUserId)
                : null;

            var query = _db.CommunityPosts
                .Include(p => p.Author)
                .Include(p => p.MediaList)
                .Include(p => p.AcceptedComment)
                    .ThenInclude(c => c!.Author)
                .AsNoTracking()
                .AsQueryable();

            // Calculate overall counters across the entire platform
            var totalAll = await _db.CommunityPosts.CountAsync();
            var totalUrgent = await _db.CommunityPosts.CountAsync(p => p.IsHelpRequest && !p.IsSolved);
            var totalExp = await _db.CommunityPosts.CountAsync(p => p.PostType == CommunityPostTypes.Experience);
            var totalTips = await _db.CommunityPosts.CountAsync(p => p.PostType == CommunityPostTypes.AgritechTip);
            var totalSolved = await _db.CommunityPosts.CountAsync(p => p.IsSolved);
            var myPostsCount = user != null ? await _db.CommunityPosts.CountAsync(p => p.AuthorId == user.Id) : 0;
            var mySavedCount = user != null ? await _db.CommunityBookmarks.CountAsync(b => b.UserId == user.Id) : 0;

            // Apply Category Filters
            switch (category.ToLowerInvariant())
            {
                case "helpneeded":
                case "urgent":
                    query = query.Where(p => p.IsHelpRequest && !p.IsSolved);
                    break;
                case "experience":
                    query = query.Where(p => p.PostType == CommunityPostTypes.Experience);
                    break;
                case "agritechtip":
                case "tips":
                    query = query.Where(p => p.PostType == CommunityPostTypes.AgritechTip);
                    break;
                case "solved":
                    query = query.Where(p => p.IsSolved);
                    break;
                case "myposts":
                    if (user != null) query = query.Where(p => p.AuthorId == user.Id);
                    break;
                case "saved":
                    if (user != null)
                    {
                        var savedPostIds = _db.CommunityBookmarks.Where(b => b.UserId == user.Id).Select(b => b.PostId);
                        query = query.Where(p => savedPostIds.Contains(p.Id));
                    }
                    break;
                default:
                    // "All" - no primary type filter
                    break;
            }

            // Apply District Filter
            if (!string.IsNullOrWhiteSpace(district) && !district.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.District == district || p.Author.District == district);
            }

            // Apply Urgency Filter
            if (!string.IsNullOrWhiteSpace(urgency) && !urgency.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.UrgencyLevel == urgency);
            }

            // Apply Search Query
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var term = searchQuery.Trim();
                query = query.Where(p => p.Content.Contains(term)
                    || (p.CropCategory != null && p.CropCategory.Contains(term))
                    || (p.IssueCategory != null && p.IssueCategory.Contains(term))
                    || p.Author.FullName.Contains(term));
            }

            // Apply Sorting
            query = sort.ToLowerInvariant() switch
            {
                "popular" => query.OrderByDescending(p => p.IsPinned)
                                  .ThenByDescending(p => p.LikeCount + p.CommentCount * 2 + p.ShareCount * 3)
                                  .ThenByDescending(p => p.CreatedAt),
                "urgent" => query.OrderByDescending(p => p.IsPinned)
                                 .ThenByDescending(p => p.IsHelpRequest && !p.IsSolved && p.UrgencyLevel == CommunityUrgencyLevels.High)
                                 .ThenByDescending(p => p.CreatedAt),
                "proximity" when user != null && !string.IsNullOrEmpty(user.District) =>
                    query.OrderByDescending(p => p.IsPinned)
                         .ThenByDescending(p => p.District == user.District || p.Author.District == user.District)
                         .ThenByDescending(p => p.CreatedAt),
                _ => query.OrderByDescending(p => p.IsPinned).ThenByDescending(p => p.CreatedAt)
            };

            var totalFiltered = await query.CountAsync();

            var rawPosts = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var postIds = rawPosts.Select(p => p.Id).ToList();

            // Load comments for these posts
            var comments = await _db.CommunityComments
                .Include(c => c.Author)
                .Where(c => postIds.Contains(c.PostId))
                .OrderBy(c => c.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            // Load reactions & bookmarks for current user
            var userReactions = new HashSet<int>();
            var userReactionTypes = new Dictionary<int, string>();
            var userBookmarks = new HashSet<int>();

            if (user != null)
            {
                var reactions = await _db.CommunityReactions
                    .Where(r => r.UserId == user.Id && r.PostId != null && postIds.Contains(r.PostId.Value))
                    .ToListAsync();

                foreach (var r in reactions)
                {
                    userReactions.Add(r.PostId!.Value);
                    userReactionTypes[r.PostId.Value] = r.ReactionType;
                }

                var bookmarks = await _db.CommunityBookmarks
                    .Where(b => b.UserId == user.Id && postIds.Contains(b.PostId))
                    .Select(b => b.PostId)
                    .ToListAsync();

                userBookmarks = new HashSet<int>(bookmarks);
            }

            var postViewModels = rawPosts.Select(p => MapToPostViewModel(
                p,
                comments.Where(c => c.PostId == p.Id).ToList(),
                user,
                userReactions.Contains(p.Id),
                userReactionTypes.GetValueOrDefault(p.Id),
                userBookmarks.Contains(p.Id)
            )).ToList();

            // Load auxiliary widgets
            var activeDistrict = district ?? user?.District ?? "Dhaka";
            var emergencyAlerts = await GetEmergencyAlertsAsync(activeDistrict, 3);
            var weatherAdvisor = await GetWeatherAdvisorAsync(activeDistrict);
            var topMentors = await GetTopMentorsAsync(3);

            return new CommunityFeedViewModel
            {
                Posts = postViewModels,
                CurrentCategory = category,
                CurrentDistrict = district,
                CurrentUrgency = urgency,
                CurrentSort = sort,
                SearchQuery = searchQuery,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPostsCount = totalFiltered,

                TotalCount = totalAll,
                UrgentNeedsCount = totalUrgent,
                ExperiencesCount = totalExp,
                TipsCount = totalTips,
                SolvedCount = totalSolved,
                MyPostsCount = myPostsCount,
                MySavedCount = mySavedCount,

                CurrentUser = user,
                CurrentUserId = user?.Id,
                IsAuthenticated = user != null,
                UserBadgeTitle = user != null ? GetUserRoleBadgeTitle(user) : null,

                EmergencyAlerts = emergencyAlerts,
                WeatherAdvisor = weatherAdvisor,
                TopMentors = topMentors,

                DistrictList = BangladeshGeo.GetDistrictsForDivision("All"),
                CropCategories = new List<string>
                {
                    "আমন ধান (Aman Rice)", "বোরো ধান (Boro Rice)", "গোল আলু (Potato)",
                    "গম (Wheat)", "ভুট্টা (Maize)", "পাট (Jute)", "বেগুন (Brinjal)",
                    "টমেটো (Tomato)", "মরিচ (Chilli)", "পেঁয়াজ (Onion)", "রসুন (Garlic)",
                    "সরিষা (Mustard)", "আম (Mango)", "পেয়ারা (Guava)", "অন্যান্য (Other)"
                },
                IssueCategories = new List<string>
                {
                    "রোগবালাই ও ছত্রাক (Fungal/Blight Disease)",
                    "ক্ষতিকর কীটপতঙ্গ (Insect Pest Attack)",
                    "পুষ্টি ও সারের ঘাটতি (Nutrient Deficiency)",
                    "সেচ ও জলাবদ্ধতা (Waterlogging/Irrigation)",
                    "মাটি ও লবণাক্ততা (Soil/Salinity Issue)",
                    "যন্ত্রপাতি সমস্যা (Machinery Breakdown)",
                    "গুদাম সংরক্ষণ সংকট (Storage Preservation)",
                    "অন্যান্য সমস্যা (General Help)"
                }
            };
        }

        public async Task<CommunityPostViewModel?> GetPostByIdAsync(int postId, string? currentUserId)
        {
            var p = await _db.CommunityPosts
                .Include(x => x.Author)
                .Include(x => x.MediaList)
                .Include(x => x.AcceptedComment)
                    .ThenInclude(c => c!.Author)
                .FirstOrDefaultAsync(x => x.Id == postId);

            if (p == null) return null;

            // Increment view count asynchronously
            p.ViewCount++;
            await _db.SaveChangesAsync();

            var user = !string.IsNullOrEmpty(currentUserId)
                ? await _userManager.FindByIdAsync(currentUserId)
                : null;

            var comments = await _db.CommunityComments
                .Include(c => c.Author)
                .Where(c => c.PostId == postId)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            var isLiked = user != null && await _db.CommunityReactions.AnyAsync(r => r.UserId == user.Id && r.PostId == postId);
            var reaction = user != null ? (await _db.CommunityReactions.FirstOrDefaultAsync(r => r.UserId == user.Id && r.PostId == postId))?.ReactionType : null;
            var isBookmarked = user != null && await _db.CommunityBookmarks.AnyAsync(b => b.UserId == user.Id && b.PostId == postId);

            return MapToPostViewModel(p, comments, user, isLiked, reaction, isBookmarked);
        }

        public async Task<int> CreatePostAsync(string authorId, CommunityPostCreateViewModel model)
        {
            var author = await _userManager.FindByIdAsync(authorId)
                ?? throw new InvalidOperationException("User not found.");

            var isHelp = model.PostType == CommunityPostTypes.HelpNeeded;

            var post = new CommunityPost
            {
                AuthorId = authorId,
                Content = model.Content.Trim(),
                PostType = model.PostType,
                CropCategory = model.CropCategory,
                IssueCategory = model.IssueCategory,
                UrgencyLevel = model.UrgencyLevel ?? CommunityUrgencyLevels.Normal,
                CropAge = model.CropAge,
                AffectedArea = model.AffectedArea,
                District = !string.IsNullOrWhiteSpace(model.District) ? model.District : author.District,
                Upazila = !string.IsNullOrWhiteSpace(model.Upazila) ? model.Upazila : author.Location,
                IsHelpRequest = isHelp,
                IsSolved = false,
                AudioDurationSeconds = model.AudioDurationSeconds,
                CreatedAt = DateTime.UtcNow
            };

            // Process Audio Note if present
            var voiceFile = model.AudioFile ?? model.AudioRecordingFile;
            if (voiceFile != null && voiceFile.Length > 0)
            {
                post.AudioRecordingUrl = await SaveMediaFileAsync(voiceFile, "community/audio");
            }

            _db.CommunityPosts.Add(post);
            await _db.SaveChangesAsync();

            // Process Image Files (Up to 5)
            if (model.ImageFiles != null && model.ImageFiles.Any())
            {
                int sortOrder = 0;
                foreach (var img in model.ImageFiles.Take(5))
                {
                    if (img != null && img.Length > 0)
                    {
                        var url = await SaveMediaFileAsync(img, "community/images");
                        if (!string.IsNullOrEmpty(url))
                        {
                            _db.PostMedia.Add(new PostMedia
                            {
                                PostId = post.Id,
                                MediaUrl = url,
                                MediaType = PostMediaTypes.Image,
                                SortOrder = sortOrder++
                            });
                        }
                    }
                }
            }

            // Process Video File (Max 1)
            if (model.VideoFile != null && model.VideoFile.Length > 0)
            {
                var videoUrl = await SaveMediaFileAsync(model.VideoFile, "community/videos");
                if (!string.IsNullOrEmpty(videoUrl))
                {
                    _db.PostMedia.Add(new PostMedia
                    {
                        PostId = post.Id,
                        MediaUrl = videoUrl,
                        MediaType = PostMediaTypes.Video,
                        SortOrder = 99
                    });
                }
            }

            await _db.SaveChangesAsync();

            // Award community contribution points (+10 for publishing a post)
            await AwardCommunityPointsAsync(author, 10, "Community Post Published");

            return post.Id;
        }

        public async Task<CommunityCommentViewModel?> AddCommentAsync(string authorId, CommunityCommentCreateViewModel model)
        {
            var author = await _userManager.FindByIdAsync(authorId);
            if (author == null) return null;

            var post = await _db.CommunityPosts
                .Include(p => p.Author)
                .FirstOrDefaultAsync(p => p.Id == model.PostId);

            if (post == null) return null;

            var comment = new CommunityComment
            {
                PostId = model.PostId,
                AuthorId = authorId,
                ParentCommentId = model.ParentCommentId,
                Content = model.Content.Trim(),
                AudioDurationSeconds = model.AudioDurationSeconds,
                CreatedAt = DateTime.UtcNow
            };

            // Process attachment photo in comment
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                comment.AttachmentImageUrl = await SaveMediaFileAsync(model.ImageFile, "community/comments");
            }

            // Process voice note in comment
            var commentVoiceFile = model.AudioFile ?? model.AudioRecordingFile;
            if (commentVoiceFile != null && commentVoiceFile.Length > 0)
            {
                comment.AudioRecordingUrl = await SaveMediaFileAsync(commentVoiceFile, "community/audio");
            }

            _db.CommunityComments.Add(comment);
            post.CommentCount++;
            await _db.SaveChangesAsync();

            // Award community participation points (+15 for commenting)
            await AwardCommunityPointsAsync(author, 15, "Community Comment Submitted");

            // Notify post author if commenter is not author
            if (post.AuthorId != authorId)
            {
                await _notifications.CreateAsync(
                    post.AuthorId,
                    "কমিউনিটি পোস্টে নতুন মন্তব্য",
                    $"{author.FullName} আপনার পোস্টে একটি মন্তব্য বা সমাধান প্রদান করেছেন।",
                    $"/Community/Post/{post.Id}"
                );
            }

            return new CommunityCommentViewModel
            {
                Id = comment.Id,
                PostId = comment.PostId,
                AuthorId = author.Id,
                AuthorName = author.FullName,
                AuthorRole = author.UserRole,
                AuthorIsVerified = author.IsVerified,
                IsOfficialExpert = author.UserRole == AppRoles.Admin,
                Content = comment.Content,
                AttachmentImageUrl = comment.AttachmentImageUrl,
                AudioRecordingUrl = comment.AudioRecordingUrl,
                AudioDurationSeconds = comment.AudioDurationSeconds,
                ParentCommentId = comment.ParentCommentId,
                IsAcceptedSolution = false,
                UpvoteCount = 0,
                CreatedAt = comment.CreatedAt,
                TimeAgo = "এইমাত্র",
                CanAcceptThisComment = post.AuthorId == authorId && !post.IsSolved
            };
        }

        public async Task<(bool Success, string Action, int LikeCount, string ReactionType)> ToggleReactionAsync(
            string userId,
            int? postId,
            int? commentId,
            string reactionType)
        {
            reactionType = string.IsNullOrWhiteSpace(reactionType) ? CommunityReactionTypes.Helpful : reactionType;

            var existing = await _db.CommunityReactions
                .FirstOrDefaultAsync(r => r.UserId == userId && r.PostId == postId && r.CommentId == commentId);

            if (existing != null)
            {
                if (existing.ReactionType == reactionType)
                {
                    _db.CommunityReactions.Remove(existing);

                    if (postId.HasValue)
                    {
                        var post = await _db.CommunityPosts.FindAsync(postId.Value);
                        if (post != null) post.LikeCount = Math.Max(0, post.LikeCount - 1);
                    }
                    else if (commentId.HasValue)
                    {
                        var comment = await _db.CommunityComments.FindAsync(commentId.Value);
                        if (comment != null) comment.UpvoteCount = Math.Max(0, comment.UpvoteCount - 1);
                    }

                    await _db.SaveChangesAsync();

                    var newCount = postId.HasValue
                        ? (await _db.CommunityPosts.FindAsync(postId.Value))?.LikeCount ?? 0
                        : (await _db.CommunityComments.FindAsync(commentId!.Value))?.UpvoteCount ?? 0;

                    return (true, "Removed", newCount, reactionType);
                }
                else
                {
                    existing.ReactionType = reactionType;
                    await _db.SaveChangesAsync();

                    var currentCount = postId.HasValue
                        ? (await _db.CommunityPosts.FindAsync(postId.Value))?.LikeCount ?? 0
                        : (await _db.CommunityComments.FindAsync(commentId!.Value))?.UpvoteCount ?? 0;

                    return (true, "Updated", currentCount, reactionType);
                }
            }

            var reaction = new CommunityReaction
            {
                UserId = userId,
                PostId = postId,
                CommentId = commentId,
                ReactionType = reactionType,
                CreatedAt = DateTime.UtcNow
            };

            _db.CommunityReactions.Add(reaction);

            if (postId.HasValue)
            {
                var post = await _db.CommunityPosts.FindAsync(postId.Value);
                if (post != null) post.LikeCount++;
            }
            else if (commentId.HasValue)
            {
                var comment = await _db.CommunityComments.FindAsync(commentId.Value);
                if (comment != null) comment.UpvoteCount++;
            }

            await _db.SaveChangesAsync();

            var totalCount = postId.HasValue
                ? (await _db.CommunityPosts.FindAsync(postId.Value))?.LikeCount ?? 0
                : (await _db.CommunityComments.FindAsync(commentId!.Value))?.UpvoteCount ?? 0;

            return (true, "Added", totalCount, reactionType);
        }

        public async Task<(bool Success, string Message)> AcceptSolutionAsync(string currentUserId, int postId, int commentId)
        {
            var post = await _db.CommunityPosts
                .Include(p => p.Author)
                .FirstOrDefaultAsync(p => p.Id == postId);

            if (post == null) return (false, "পোস্টটি পাওয়া যায়নি।");

            var user = await _userManager.FindByIdAsync(currentUserId);
            var isAuthor = post.AuthorId == currentUserId;
            var isAdmin = user != null && await _userManager.IsInRoleAsync(user, AppRoles.Admin);

            if (!isAuthor && !isAdmin)
            {
                return (false, "শুধুমাত্র পোস্টের লেখক বা বিশেষজ্ঞ কৃষি কর্মকর্তা সমাধান গ্রহণ করতে পারেন।");
            }

            var comment = await _db.CommunityComments
                .Include(c => c.Author)
                .FirstOrDefaultAsync(c => c.Id == commentId && c.PostId == postId);

            if (comment == null) return (false, "মন্তব্য বা সমাধানটি পাওয়া যায়নি।");

            // Mark previous accepted comments as false if any
            var prevAccepted = await _db.CommunityComments
                .Where(c => c.PostId == postId && c.IsAcceptedSolution)
                .ToListAsync();

            foreach (var c in prevAccepted) c.IsAcceptedSolution = false;

            comment.IsAcceptedSolution = true;
            post.IsSolved = true;
            post.AcceptedCommentId = commentId;

            await _db.SaveChangesAsync();

            // Award +50 Loyalty Points to the resolving comment author
            if (comment.Author != null)
            {
                await AwardCommunityPointsAsync(comment.Author, 50, "Accepted Solution Reward for Community Help");

                await _notifications.CreateAsync(
                    comment.AuthorId,
                    "অভিনন্দন! আপনার সমাধান গৃহীত হয়েছে",
                    $"আপনার প্রদানকৃত সমাধানটি লেখক কর্তৃক সঠিক সমাধান হিসেবে গৃহীত হয়েছে এবং আপনি +৫০ কৃষি পয়েন্ট অর্জন করেছেন।",
                    $"/Community/Post/{postId}"
                );
            }

            return (true, "সঠিক সমাধান হিসেবে সফলভাবে গৃহীত হয়েছে।");
        }

        public async Task<(bool Success, bool IsBookmarked)> ToggleBookmarkAsync(string userId, int postId)
        {
            var existing = await _db.CommunityBookmarks
                .FirstOrDefaultAsync(b => b.UserId == userId && b.PostId == postId);

            if (existing != null)
            {
                _db.CommunityBookmarks.Remove(existing);
                await _db.SaveChangesAsync();
                return (true, false);
            }

            _db.CommunityBookmarks.Add(new CommunityBookmark
            {
                UserId = userId,
                PostId = postId,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return (true, true);
        }

        public async Task<bool> ReportPostAsync(string reporterId, int postId, string reason)
        {
            var post = await _db.CommunityPosts.FindAsync(postId);
            if (post == null) return false;

            _db.CommunityPostReports.Add(new CommunityPostReport
            {
                ReporterId = reporterId,
                PostId = postId,
                Reason = reason.Trim(),
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            });

            post.IsFlagged = true;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeletePostAsync(string userId, bool isAdmin, int postId)
        {
            var post = await _db.CommunityPosts
                .Include(p => p.MediaList)
                .FirstOrDefaultAsync(p => p.Id == postId);

            if (post == null) return false;

            if (post.AuthorId != userId && !isAdmin) return false;

            // Delete physical media files
            foreach (var m in post.MediaList)
            {
                DeleteLocalFile(m.MediaUrl);
            }
            if (!string.IsNullOrEmpty(post.AudioRecordingUrl))
            {
                DeleteLocalFile(post.AudioRecordingUrl);
            }

            _db.CommunityPosts.Remove(post);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<List<EmergencyAlertItemViewModel>> GetEmergencyAlertsAsync(string? district, int limit = 3)
        {
            var query = _db.CommunityPosts
                .Include(p => p.Author)
                .Where(p => p.IsHelpRequest && !p.IsSolved && p.UrgencyLevel == CommunityUrgencyLevels.High)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(district) && !district.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.District == district || p.Author.District == district);
            }

            var urgent = await query
                .OrderByDescending(p => p.CreatedAt)
                .Take(limit)
                .ToListAsync();

            return urgent.Select(p => new EmergencyAlertItemViewModel
            {
                PostId = p.Id,
                CropName = p.CropCategory ?? "ফসল",
                IssueTitle = p.IssueCategory ?? "জরুরি সমস্যা",
                AuthorName = p.Author.FullName,
                Location = p.Upazila ?? p.District ?? "বাংলাদেশ",
                TimeAgo = FormatTimeAgo(p.CreatedAt),
                UrgencyLevel = p.UrgencyLevel
            }).ToList();
        }

        public async Task<List<TopMentorViewModel>> GetTopMentorsAsync(int limit = 3)
        {
            // Calculate top contributors who have solutions accepted or high points
            var topUsers = await _db.Users
                .OrderByDescending(u => u.LoyaltyPoints)
                .Take(limit)
                .ToListAsync();

            var result = new List<TopMentorViewModel>();
            int rank = 1;
            foreach (var u in topUsers)
            {
                var solutions = await _db.CommunityComments.CountAsync(c => c.AuthorId == u.Id && c.IsAcceptedSolution);
                result.Add(new TopMentorViewModel
                {
                    UserId = u.Id,
                    FullName = u.FullName,
                    Role = u.UserRole,
                    AvatarUrl = null,
                    IsVerified = u.IsVerified,
                    Points = u.LoyaltyPoints,
                    SolutionsCount = solutions,
                    Rank = rank++
                });
            }

            return result;
        }

        public async Task<CommunityWeatherAdvisorViewModel> GetWeatherAdvisorAsync(string? district)
        {
            district = string.IsNullOrWhiteSpace(district) ? "Dhaka" : district;

            try
            {
                var weather = await _weatherService.GetForecastAsync(district);
                var temp = weather?.Temperature ?? 28.0;
                var humidity = (int)(weather?.Humidity ?? 75.0);
                var condition = weather?.BanglaCondition ?? weather?.Condition ?? "রৌদ্রোজ্জ্বল";

                // Spray heuristic: Favorable if humidity < 85% and no heavy precipitation forecasted
                bool favorable = humidity < 85;
                string advice = favorable
                    ? "আজ বিকেলে ছত্রাকনাশক ও বালাইনাশক স্প্রে করার জন্য আবহাওয়া অত্যন্ত অনুকূল।"
                    : "বাতাসে অতিরিক্ত আর্দ্রতা বা বৃষ্টির সম্ভাবনায় আজ বালাইনাশক স্প্রে স্থগিত রাখার পরামর্শ দেওয়া হচ্ছে।";

                return new CommunityWeatherAdvisorViewModel
                {
                    District = district,
                    Temperature = temp,
                    Humidity = humidity,
                    ConditionText = condition,
                    IsSprayFavorable = favorable,
                    SprayAdviceText = advice
                };
            }
            catch
            {
                return new CommunityWeatherAdvisorViewModel
                {
                    District = district,
                    Temperature = 28.5,
                    Humidity = 76,
                    ConditionText = "পরিষ্কার আকাশ",
                    IsSprayFavorable = true,
                    SprayAdviceText = "আজ বিকেলে ছত্রাকনাশক স্প্রে করার উপযুক্ত সময়।"
                };
            }
        }

        #region Helper Methods

        private CommunityPostViewModel MapToPostViewModel(
            CommunityPost p,
            List<CommunityComment> allPostComments,
            ApplicationUser? currentUser,
            bool isLiked,
            string? reactionType,
            bool isBookmarked)
        {
            var isAuthor = currentUser != null && p.AuthorId == currentUser.Id;
            var isAdmin = currentUser != null && currentUser.UserRole == AppRoles.Admin;

            // Structure top-level comments and 1-level replies
            var topComments = allPostComments
                .Where(c => c.ParentCommentId == null)
                .Select(c => MapToCommentViewModel(c, allPostComments, p.AuthorId, currentUser))
                .ToList();

            CommunityCommentViewModel? acceptedModel = null;
            if (p.AcceptedComment != null)
            {
                acceptedModel = MapToCommentViewModel(p.AcceptedComment, allPostComments, p.AuthorId, currentUser);
            }
            else if (p.AcceptedCommentId.HasValue)
            {
                var acc = allPostComments.FirstOrDefault(c => c.Id == p.AcceptedCommentId.Value);
                if (acc != null) acceptedModel = MapToCommentViewModel(acc, allPostComments, p.AuthorId, currentUser);
            }

            return new CommunityPostViewModel
            {
                Id = p.Id,
                AuthorId = p.AuthorId,
                AuthorName = p.Author.FullName,
                AuthorRole = p.Author.UserRole,
                AuthorIsVerified = p.Author.IsVerified,
                AuthorDistrict = p.Author.District,

                Content = p.Content,
                PostType = p.PostType,
                CropCategory = p.CropCategory,
                IssueCategory = p.IssueCategory,
                UrgencyLevel = p.UrgencyLevel,
                CropAge = p.CropAge,
                AffectedArea = p.AffectedArea,

                District = p.District,
                Upazila = p.Upazila,

                IsHelpRequest = p.IsHelpRequest,
                IsSolved = p.IsSolved,

                AudioRecordingUrl = p.AudioRecordingUrl,
                AudioDurationSeconds = p.AudioDurationSeconds,

                ViewCount = p.ViewCount,
                LikeCount = p.LikeCount,
                CommentCount = p.CommentCount,
                ShareCount = p.ShareCount,
                IsPinned = p.IsPinned,

                CreatedAt = p.CreatedAt,
                TimeAgo = FormatTimeAgo(p.CreatedAt),

                MediaList = p.MediaList.OrderBy(m => m.SortOrder).Select(m => new PostMediaViewModel
                {
                    Id = m.Id,
                    MediaUrl = m.MediaUrl,
                    MediaType = m.MediaType,
                    ThumbnailUrl = m.ThumbnailUrl,
                    SortOrder = m.SortOrder
                }).ToList(),

                AcceptedComment = acceptedModel,

                IsLikedByCurrentUser = isLiked,
                CurrentUserReactionType = reactionType,
                IsBookmarkedByCurrentUser = isBookmarked,
                CanEdit = isAuthor && (DateTime.UtcNow - p.CreatedAt).TotalHours < 1,
                CanDelete = isAuthor || isAdmin,
                CanAcceptSolution = (isAuthor || isAdmin) && !p.IsSolved,

                Comments = topComments
            };
        }

        private CommunityCommentViewModel MapToCommentViewModel(
            CommunityComment c,
            List<CommunityComment> allComments,
            string postAuthorId,
            ApplicationUser? currentUser)
        {
            var replies = allComments
                .Where(r => r.ParentCommentId == c.Id)
                .Select(r => MapToCommentViewModel(r, allComments, postAuthorId, currentUser))
                .ToList();

            var isPostAuthor = currentUser != null && currentUser.Id == postAuthorId;
            var isAdmin = currentUser != null && currentUser.UserRole == AppRoles.Admin;

            return new CommunityCommentViewModel
            {
                Id = c.Id,
                PostId = c.PostId,
                AuthorId = c.AuthorId,
                AuthorName = c.Author?.FullName ?? "Unknown",
                AuthorRole = c.Author?.UserRole,
                AuthorIsVerified = c.Author?.IsVerified ?? false,
                IsOfficialExpert = c.Author?.UserRole == AppRoles.Admin,
                Content = c.Content,
                AttachmentImageUrl = c.AttachmentImageUrl,
                AudioRecordingUrl = c.AudioRecordingUrl,
                AudioDurationSeconds = c.AudioDurationSeconds,
                ParentCommentId = c.ParentCommentId,
                IsAcceptedSolution = c.IsAcceptedSolution,
                UpvoteCount = c.UpvoteCount,
                CreatedAt = c.CreatedAt,
                TimeAgo = FormatTimeAgo(c.CreatedAt),
                CanAcceptThisComment = (isPostAuthor || isAdmin) && !c.IsAcceptedSolution,
                Replies = replies
            };
        }

        private async Task<string> SaveMediaFileAsync(IFormFile file, string subFolder)
        {
            var uploadDir = Path.Combine(_env.WebRootPath, "uploads", subFolder);
            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(extension))
            {
                // Fallback extensions for audio/video blob uploads
                if (file.ContentType.Contains("audio")) extension = ".webm";
                else if (file.ContentType.Contains("video")) extension = ".mp4";
                else extension = ".jpg";
            }

            var uniqueName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(uploadDir, uniqueName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/{subFolder}/{uniqueName}";
        }

        private void DeleteLocalFile(string relativeUrl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(relativeUrl)) return;
                var trimmed = relativeUrl.TrimStart('/');
                var fullPath = Path.Combine(_env.WebRootPath, trimmed);
                if (File.Exists(fullPath)) File.Delete(fullPath);
            }
            catch { }
        }

        private async Task AwardCommunityPointsAsync(ApplicationUser user, int points, string description)
        {
            try
            {
                user.LoyaltyPoints += points;
                _db.LoyaltyPointTransactions.Add(new LoyaltyPointTransaction
                {
                    UserId = user.Id,
                    Points = points,
                    Type = LoyaltyTransactionTypes.Earned,
                    Description = description,
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }
            catch { }
        }

        private static string GetUserRoleBadgeTitle(ApplicationUser user)
        {
            return user.UserRole switch
            {
                AppRoles.Admin => "কৃষি কর্মকর্তা (DAE)",
                AppRoles.EquipmentOwner => "যন্ত্রপাতি মালিক",
                AppRoles.GodownOwner => "গুদাম মালিক",
                _ => user.IsVerified ? "ভেরিফাইড কৃষক" : "কৃষক"
            };
        }

        private static string FormatTimeAgo(DateTime dt)
        {
            var span = DateTime.UtcNow - dt;
            if (span.TotalMinutes < 1) return "এইমাত্র";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} মিনিট আগে";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} ঘণ্টা আগে";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays} দিন আগে";
            return dt.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
