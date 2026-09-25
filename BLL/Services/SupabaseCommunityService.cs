using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using KrishiLink.BLL.Helpers;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace KrishiLink.BLL.Services
{
    public class SupabaseCommunityService : ICommunityService
    {
        private readonly string _connectionString;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly INotificationService _notifications;
        private readonly IWeatherService _weatherService;

        public SupabaseCommunityService(
            IConfiguration configuration,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            IWebHostEnvironment env,
            INotificationService notifications,
            IWeatherService weatherService)
        {
            var conn = configuration.GetConnectionString("SupabaseConnection");
            if (string.IsNullOrWhiteSpace(conn) || conn.Contains("YOUR_SUPABASE_PASSWORD"))
            {
                conn = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION")
                    ?? configuration["DIRECT_URL"]
                    ?? configuration["DATABASE_URL"];

                if (string.IsNullOrWhiteSpace(conn))
                {
                    try
                    {
                        var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
                        if (File.Exists(envPath))
                        {
                            var lines = File.ReadAllLines(envPath);
                            string? dbPass = null;
                            foreach (var line in lines)
                            {
                                if (line.StartsWith("SUPABASE_DB_PASSWORD="))
                                    dbPass = line.Substring("SUPABASE_DB_PASSWORD=".Length).Trim();
                            }
                            if (!string.IsNullOrEmpty(dbPass))
                            {
                                conn = $"Host=aws-0-ap-northeast-2.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.estwgpswfpakthwtdioy;Password={dbPass};SSL Mode=Require;Trust Server Certificate=true;";
                            }
                        }
                    }
                    catch { }
                }
            }

            _connectionString = !string.IsNullOrWhiteSpace(conn) && !conn.Contains("YOUR_SUPABASE_PASSWORD")
                ? conn
                : (configuration.GetConnectionString("SupabaseConnection")
                    ?? throw new InvalidOperationException("Supabase connection string 'SupabaseConnection' not found in configuration."));

            _userManager = userManager;
            _db = db;
            _env = env;
            _notifications = notifications;
            _weatherService = weatherService;
        }

        private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

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

            using var conn = CreateConnection();

            // Overall counters
            var totalAll = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM community_posts");
            var totalUrgent = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM community_posts WHERE is_help_request = true AND is_solved = false");
            var totalExp = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM community_posts WHERE post_type = 'Experience'");
            var totalTips = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM community_posts WHERE post_type = 'AgritechTip'");
            var totalSolved = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM community_posts WHERE is_solved = true");
            var myPostsCount = user != null
                ? await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM community_posts WHERE author_id = @UserId", new { UserId = user.Id })
                : 0;
            var mySavedCount = user != null
                ? await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM community_bookmarks WHERE user_id = @UserId", new { UserId = user.Id })
                : 0;

            // Build dynamic query
            var whereClauses = new List<string>();
            var p = new DynamicParameters();

            switch (category.ToLowerInvariant())
            {
                case "helpneeded":
                case "urgent":
                    whereClauses.Add("(is_help_request = true AND is_solved = false)");
                    break;
                case "experience":
                    whereClauses.Add("post_type = 'Experience'");
                    break;
                case "agritechtip":
                case "tips":
                    whereClauses.Add("post_type = 'AgritechTip'");
                    break;
                case "solved":
                    whereClauses.Add("is_solved = true");
                    break;
                case "myposts":
                    if (user != null)
                    {
                        whereClauses.Add("author_id = @CurrentUserId");
                        p.Add("CurrentUserId", user.Id);
                    }
                    break;
                case "saved":
                    if (user != null)
                    {
                        whereClauses.Add("id IN (SELECT post_id FROM community_bookmarks WHERE user_id = @CurrentUserId)");
                        p.Add("CurrentUserId", user.Id);
                    }
                    break;
                default:
                    break;
            }

            if (!string.IsNullOrWhiteSpace(district) && !district.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                whereClauses.Add("(district = @District OR author_district = @District)");
                p.Add("District", district);
            }

            if (!string.IsNullOrWhiteSpace(urgency) && !urgency.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                whereClauses.Add("urgency_level = @Urgency");
                p.Add("Urgency", urgency);
            }

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                whereClauses.Add("(content ILIKE @SearchTerm OR crop_category ILIKE @SearchTerm OR issue_category ILIKE @SearchTerm OR author_name ILIKE @SearchTerm)");
                p.Add("SearchTerm", $"%{searchQuery.Trim()}%");
            }

            var whereSql = whereClauses.Any() ? " WHERE " + string.Join(" AND ", whereClauses) : "";

            var orderSql = sort.ToLowerInvariant() switch
            {
                "popular" => " ORDER BY is_pinned DESC, (like_count + comment_count * 2 + share_count * 3) DESC, created_at DESC",
                "urgent" => " ORDER BY is_pinned DESC, (is_help_request AND NOT is_solved AND urgency_level = 'High') DESC, created_at DESC",
                "proximity" when user != null && !string.IsNullOrEmpty(user.District) =>
                    $" ORDER BY is_pinned DESC, (district = '{user.District}' OR author_district = '{user.District}') DESC, created_at DESC",
                _ => " ORDER BY is_pinned DESC, created_at DESC"
            };

            var countSql = $"SELECT COUNT(*) FROM community_posts {whereSql}";
            var totalFiltered = await conn.ExecuteScalarAsync<int>(countSql, p);

            var offset = (pageNumber - 1) * pageSize;
            p.Add("Limit", pageSize);
            p.Add("Offset", offset);

            var querySql = $"SELECT * FROM community_posts {whereSql} {orderSql} LIMIT @Limit OFFSET @Offset";
            var rawPosts = (await conn.QueryAsync<SupabaseCommunityPostDto>(querySql, p)).ToList();

            var postIds = rawPosts.Select(x => x.id).ToList();

            // Load media
            List<SupabasePostMediaDto> rawMedia = new();
            List<SupabaseCommentDto> rawComments = new();
            HashSet<int> userReactions = new();
            Dictionary<int, string> userReactionTypes = new();
            HashSet<int> userBookmarks = new();

            if (postIds.Any())
            {
                rawMedia = (await conn.QueryAsync<SupabasePostMediaDto>(
                    "SELECT * FROM community_post_media WHERE post_id = ANY(@PostIds) ORDER BY display_order ASC",
                    new { PostIds = postIds.ToArray() })).ToList();

                rawComments = (await conn.QueryAsync<SupabaseCommentDto>(
                    "SELECT * FROM community_comments WHERE post_id = ANY(@PostIds) ORDER BY created_at ASC",
                    new { PostIds = postIds.ToArray() })).ToList();

                if (user != null)
                {
                    var reactions = (await conn.QueryAsync<SupabaseReactionDto>(
                        "SELECT * FROM community_reactions WHERE user_id = @UserId AND post_id = ANY(@PostIds)",
                        new { UserId = user.Id, PostIds = postIds.ToArray() })).ToList();

                    foreach (var r in reactions)
                    {
                        if (r.post_id.HasValue)
                        {
                            userReactions.Add(r.post_id.Value);
                            userReactionTypes[r.post_id.Value] = r.reaction_type;
                        }
                    }

                    var bookmarks = (await conn.QueryAsync<int>(
                        "SELECT post_id FROM community_bookmarks WHERE user_id = @UserId AND post_id = ANY(@PostIds)",
                        new { UserId = user.Id, PostIds = postIds.ToArray() })).ToList();

                    userBookmarks = new HashSet<int>(bookmarks);
                }
            }

            var postViewModels = rawPosts.Select(dto => MapDtoToPostViewModel(
                dto,
                rawMedia.Where(m => m.post_id == dto.id).ToList(),
                rawComments.Where(c => c.post_id == dto.id).ToList(),
                user,
                userReactions.Contains(dto.id),
                userReactionTypes.GetValueOrDefault(dto.id),
                userBookmarks.Contains(dto.id)
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
            using var conn = CreateConnection();

            await conn.ExecuteAsync("UPDATE community_posts SET view_count = view_count + 1 WHERE id = @Id", new { Id = postId });

            var dto = await conn.QueryFirstOrDefaultAsync<SupabaseCommunityPostDto>(
                "SELECT * FROM community_posts WHERE id = @Id", new { Id = postId });

            if (dto == null) return null;

            var user = !string.IsNullOrEmpty(currentUserId)
                ? await _userManager.FindByIdAsync(currentUserId)
                : null;

            var rawMedia = (await conn.QueryAsync<SupabasePostMediaDto>(
                "SELECT * FROM community_post_media WHERE post_id = @Id ORDER BY display_order ASC", new { Id = postId })).ToList();

            var rawComments = (await conn.QueryAsync<SupabaseCommentDto>(
                "SELECT * FROM community_comments WHERE post_id = @Id ORDER BY created_at ASC", new { Id = postId })).ToList();

            bool isLiked = false;
            string? reaction = null;
            bool isBookmarked = false;

            if (user != null)
            {
                var r = await conn.QueryFirstOrDefaultAsync<SupabaseReactionDto>(
                    "SELECT * FROM community_reactions WHERE user_id = @UserId AND post_id = @PostId",
                    new { UserId = user.Id, PostId = postId });
                if (r != null)
                {
                    isLiked = true;
                    reaction = r.reaction_type;
                }

                isBookmarked = await conn.ExecuteScalarAsync<bool>(
                    "SELECT EXISTS(SELECT 1 FROM community_bookmarks WHERE user_id = @UserId AND post_id = @PostId)",
                    new { UserId = user.Id, PostId = postId });
            }

            return MapDtoToPostViewModel(dto, rawMedia, rawComments, user, isLiked, reaction, isBookmarked);
        }

        public async Task<int> CreatePostAsync(string authorId, CommunityPostCreateViewModel model)
        {
            var author = await _userManager.FindByIdAsync(authorId)
                ?? throw new InvalidOperationException("User not found.");

            var isHelp = model.PostType == CommunityPostTypes.HelpNeeded;

            string? audioUrl = null;
            int? audioSeconds = model.AudioDurationSeconds;

            var uploadedAudio = model.AudioFile ?? model.AudioRecordingFile;
            if (uploadedAudio != null && uploadedAudio.Length > 0)
            {
                audioUrl = await SaveMediaFileAsync(uploadedAudio, "audio");
            }

            using var conn = CreateConnection();

            const string insertPostSql = @"
                INSERT INTO community_posts (
                    author_id, author_name, author_role, author_district, author_is_verified,
                    content, post_type, crop_category, issue_category, urgency_level, crop_age,
                    affected_area, upazila, district, audio_recording_url, audio_duration_seconds,
                    is_help_request, is_solved, is_pinned, is_flagged, like_count, comment_count,
                    share_count, view_count, created_at
                ) VALUES (
                    @AuthorId, @AuthorName, @AuthorRole, @AuthorDistrict, @AuthorIsVerified,
                    @Content, @PostType, @CropCategory, @IssueCategory, @UrgencyLevel, @CropAge,
                    @AffectedArea, @Upazila, @District, @AudioUrl, @AudioSeconds,
                    @IsHelpRequest, false, false, false, 0, 0,
                    0, 0, NOW()
                ) RETURNING id;";

            var postId = await conn.ExecuteScalarAsync<int>(insertPostSql, new
            {
                AuthorId = author.Id,
                AuthorName = author.FullName,
                AuthorRole = author.UserRole,
                AuthorDistrict = author.District,
                AuthorIsVerified = author.IsVerified,
                Content = model.Content.Trim(),
                PostType = model.PostType,
                CropCategory = model.CropCategory,
                IssueCategory = model.IssueCategory,
                UrgencyLevel = model.UrgencyLevel ?? CommunityUrgencyLevels.Normal,
                CropAge = model.CropAge,
                AffectedArea = model.AffectedArea,
                Upazila = author.Location ?? "সদর",
                District = author.District ?? "বাংলাদেশ",
                AudioUrl = audioUrl,
                AudioSeconds = audioSeconds,
                IsHelpRequest = isHelp
            });

            // Handle Images
            int order = 0;
            if (model.ImageFiles != null && model.ImageFiles.Any())
            {
                foreach (var file in model.ImageFiles.Take(5))
                {
                    if (file.Length > 0)
                    {
                        var url = await SaveMediaFileAsync(file, "images");
                        await conn.ExecuteAsync(@"
                            INSERT INTO community_post_media (post_id, media_url, media_type, file_name, file_size_bytes, display_order, created_at)
                            VALUES (@PostId, @MediaUrl, 'Image', @FileName, @FileSize, @DisplayOrder, NOW())",
                            new
                            {
                                PostId = postId,
                                MediaUrl = url,
                                FileName = Path.GetFileName(file.FileName),
                                FileSize = file.Length,
                                DisplayOrder = order++
                            });
                    }
                }
            }

            // Handle Video
            if (model.VideoFile != null && model.VideoFile.Length > 0)
            {
                var url = await SaveMediaFileAsync(model.VideoFile, "videos");
                await conn.ExecuteAsync(@"
                    INSERT INTO community_post_media (post_id, media_url, media_type, file_name, file_size_bytes, display_order, created_at)
                    VALUES (@PostId, @MediaUrl, 'Video', @FileName, @FileSize, @DisplayOrder, NOW())",
                    new
                    {
                        PostId = postId,
                        MediaUrl = url,
                        FileName = Path.GetFileName(model.VideoFile.FileName),
                        FileSize = model.VideoFile.Length,
                        DisplayOrder = order++
                    });
            }

            // Gamification: Award 25 loyalty points in main database
            await AwardCommunityPointsAsync(author, 25, "কমিউনিটি পোস্টে অবদান রাখার জন্য অর্জিত পয়েন্ট");

            return postId;
        }

        public async Task<CommunityCommentViewModel?> AddCommentAsync(string authorId, CommunityCommentCreateViewModel model)
        {
            var author = await _userManager.FindByIdAsync(authorId)
                ?? throw new InvalidOperationException("User not found.");

            string? imageUrl = null;
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                imageUrl = await SaveMediaFileAsync(model.ImageFile, "comments");
            }

            string? audioUrl = null;
            if (model.AudioFile != null && model.AudioFile.Length > 0)
            {
                audioUrl = await SaveMediaFileAsync(model.AudioFile, "comments");
            }

            using var conn = CreateConnection();

            const string insertCommentSql = @"
                INSERT INTO community_comments (
                    post_id, parent_comment_id, author_id, author_name, author_role, author_is_verified,
                    content, attachment_image_url, audio_recording_url, audio_duration_seconds,
                    is_accepted_solution, upvote_count, created_at
                ) VALUES (
                    @PostId, @ParentCommentId, @AuthorId, @AuthorName, @AuthorRole, @AuthorIsVerified,
                    @Content, @ImageUrl, @AudioUrl, @AudioSeconds,
                    false, 0, NOW()
                ) RETURNING id;";

            var commentId = await conn.ExecuteScalarAsync<int>(insertCommentSql, new
            {
                PostId = model.PostId,
                ParentCommentId = model.ParentCommentId,
                AuthorId = author.Id,
                AuthorName = author.FullName,
                AuthorRole = author.UserRole,
                AuthorIsVerified = author.IsVerified,
                Content = model.Content.Trim(),
                ImageUrl = imageUrl,
                AudioUrl = audioUrl,
                AudioSeconds = model.AudioDurationSeconds
            });

            await conn.ExecuteAsync("UPDATE community_posts SET comment_count = comment_count + 1 WHERE id = @PostId", new { model.PostId });

            // Award 10 loyalty points to comment author
            await AwardCommunityPointsAsync(author, 10, "কমিউনিটিতে পরামর্শ বা মতামত প্রদানের পয়েন্ট");

            return new CommunityCommentViewModel
            {
                Id = commentId,
                PostId = model.PostId,
                ParentCommentId = model.ParentCommentId,
                AuthorId = author.Id,
                AuthorName = author.FullName,
                AuthorRole = author.UserRole,
                AuthorIsVerified = author.IsVerified,
                Content = model.Content.Trim(),
                AttachmentImageUrl = imageUrl,
                AudioRecordingUrl = audioUrl,
                AudioDurationSeconds = model.AudioDurationSeconds,
                IsAcceptedSolution = false,
                UpvoteCount = 0,
                CreatedAt = DateTime.UtcNow,
                TimeAgo = "এইমাত্র",
                IsOfficialExpert = author.UserRole == AppRoles.Admin,
                CanAcceptThisComment = false,
                Replies = new List<CommunityCommentViewModel>()
            };
        }

        public async Task<(bool Success, string Action, int LikeCount, string ReactionType)> ToggleReactionAsync(
            string userId, int? postId, int? commentId, string reactionType)
        {
            using var conn = CreateConnection();

            if (postId.HasValue)
            {
                var existing = await conn.QueryFirstOrDefaultAsync<int?>(
                    "SELECT id FROM community_reactions WHERE user_id = @UserId AND post_id = @PostId",
                    new { UserId = userId, PostId = postId.Value });

                if (existing.HasValue)
                {
                    await conn.ExecuteAsync("DELETE FROM community_reactions WHERE id = @Id", new { Id = existing.Value });
                    await conn.ExecuteAsync("UPDATE community_posts SET like_count = GREATEST(0, like_count - 1) WHERE id = @PostId", new { PostId = postId.Value });
                    var newCount = await conn.ExecuteScalarAsync<int>("SELECT like_count FROM community_posts WHERE id = @PostId", new { PostId = postId.Value });
                    return (true, "Removed", newCount, reactionType);
                }
                else
                {
                    await conn.ExecuteAsync(@"
                        INSERT INTO community_reactions (post_id, user_id, reaction_type, created_at)
                        VALUES (@PostId, @UserId, @ReactionType, NOW())",
                        new { PostId = postId.Value, UserId = userId, ReactionType = reactionType });
                    await conn.ExecuteAsync("UPDATE community_posts SET like_count = like_count + 1 WHERE id = @PostId", new { PostId = postId.Value });
                    var newCount = await conn.ExecuteScalarAsync<int>("SELECT like_count FROM community_posts WHERE id = @PostId", new { PostId = postId.Value });
                    return (true, "Added", newCount, reactionType);
                }
            }
            else if (commentId.HasValue)
            {
                var existing = await conn.QueryFirstOrDefaultAsync<int?>(
                    "SELECT id FROM community_reactions WHERE user_id = @UserId AND comment_id = @CommentId",
                    new { UserId = userId, CommentId = commentId.Value });

                if (existing.HasValue)
                {
                    await conn.ExecuteAsync("DELETE FROM community_reactions WHERE id = @Id", new { Id = existing.Value });
                    await conn.ExecuteAsync("UPDATE community_comments SET upvote_count = GREATEST(0, upvote_count - 1) WHERE id = @CommentId", new { CommentId = commentId.Value });
                    var newCount = await conn.ExecuteScalarAsync<int>("SELECT upvote_count FROM community_comments WHERE id = @CommentId", new { CommentId = commentId.Value });
                    return (true, "Removed", newCount, reactionType);
                }
                else
                {
                    await conn.ExecuteAsync(@"
                        INSERT INTO community_reactions (comment_id, user_id, reaction_type, created_at)
                        VALUES (@CommentId, @UserId, @ReactionType, NOW())",
                        new { CommentId = commentId.Value, UserId = userId, ReactionType = reactionType });
                    await conn.ExecuteAsync("UPDATE community_comments SET upvote_count = upvote_count + 1 WHERE id = @CommentId", new { CommentId = commentId.Value });
                    var newCount = await conn.ExecuteScalarAsync<int>("SELECT upvote_count FROM community_comments WHERE id = @CommentId", new { CommentId = commentId.Value });
                    return (true, "Added", newCount, reactionType);
                }
            }

            return (false, "None", 0, reactionType);
        }

        public async Task<(bool Success, string Message)> AcceptSolutionAsync(string currentUserId, int postId, int commentId)
        {
            using var conn = CreateConnection();

            var post = await conn.QueryFirstOrDefaultAsync<SupabaseCommunityPostDto>(
                "SELECT * FROM community_posts WHERE id = @PostId", new { PostId = postId });

            if (post == null) return (false, "পোস্টটি পাওয়া যায়নি।");

            var comment = await conn.QueryFirstOrDefaultAsync<SupabaseCommentDto>(
                "SELECT * FROM community_comments WHERE id = @CommentId AND post_id = @PostId",
                new { CommentId = commentId, PostId = postId });

            if (comment == null) return (false, "মন্তব্যটি পাওয়া যায়নি।");

            var user = await _userManager.FindByIdAsync(currentUserId);
            var isAuthor = post.author_id == currentUserId;
            var isAdmin = user != null && user.UserRole == AppRoles.Admin;

            if (!isAuthor && !isAdmin)
            {
                return (false, "শুধুমাত্র পোস্টদাতা বা কৃষি কর্মকর্তা এই সমাধানটি গ্রহণ করতে পারেন।");
            }

            // Reset other accepted comments on this post
            await conn.ExecuteAsync("UPDATE community_comments SET is_accepted_solution = false WHERE post_id = @PostId", new { PostId = postId });
            await conn.ExecuteAsync("UPDATE community_comments SET is_accepted_solution = true WHERE id = @CommentId", new { CommentId = commentId });
            await conn.ExecuteAsync("UPDATE community_posts SET is_solved = true, accepted_comment_id = @CommentId WHERE id = @PostId", new { CommentId = commentId, PostId = postId });

            // Reward 50 loyalty points to solution solver
            var solver = await _userManager.FindByIdAsync(comment.author_id);
            if (solver != null)
            {
                await AwardCommunityPointsAsync(solver, 50, "সঠিক কৃষি সমাধান প্রদানের জন্য অর্জিত বিশেষ পয়েন্ট");
            }

            return (true, "সঠিক সমাধানটি সফলভাবে গৃহীত হয়েছে।");
        }

        public async Task<(bool Success, bool IsBookmarked)> ToggleBookmarkAsync(string userId, int postId)
        {
            using var conn = CreateConnection();

            var existing = await conn.QueryFirstOrDefaultAsync<int?>(
                "SELECT id FROM community_bookmarks WHERE user_id = @UserId AND post_id = @PostId",
                new { UserId = userId, PostId = postId });

            if (existing.HasValue)
            {
                await conn.ExecuteAsync("DELETE FROM community_bookmarks WHERE id = @Id", new { Id = existing.Value });
                return (true, false);
            }
            else
            {
                await conn.ExecuteAsync(
                    "INSERT INTO community_bookmarks (post_id, user_id, created_at) VALUES (@PostId, @UserId, NOW())",
                    new { PostId = postId, UserId = userId });
                return (true, true);
            }
        }

        public async Task<bool> ReportPostAsync(string reporterId, int postId, string reason)
        {
            using var conn = CreateConnection();

            await conn.ExecuteAsync(@"
                INSERT INTO community_moderation_reports (post_id, reporter_id, reason, status, created_at)
                VALUES (@PostId, @ReporterId, @Reason, 'Pending', NOW())",
                new { PostId = postId, ReporterId = reporterId, Reason = reason });

            await conn.ExecuteAsync("UPDATE community_posts SET is_flagged = true WHERE id = @PostId", new { PostId = postId });
            return true;
        }

        public async Task<bool> DeletePostAsync(string userId, bool isAdmin, int postId)
        {
            using var conn = CreateConnection();

            var post = await conn.QueryFirstOrDefaultAsync<SupabaseCommunityPostDto>(
                "SELECT * FROM community_posts WHERE id = @PostId", new { PostId = postId });

            if (post == null) return false;

            if (post.author_id != userId && !isAdmin) return false;

            await conn.ExecuteAsync("DELETE FROM community_posts WHERE id = @PostId", new { PostId = postId });
            return true;
        }

        public async Task<List<EmergencyAlertItemViewModel>> GetEmergencyAlertsAsync(string? district, int limit = 3)
        {
            using var conn = CreateConnection();

            var sql = "SELECT * FROM community_posts WHERE is_help_request = true AND is_solved = false AND urgency_level = 'High'";
            var p = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(district) && !district.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                sql += " AND (district = @District OR author_district = @District)";
                p.Add("District", district);
            }

            sql += " ORDER BY created_at DESC LIMIT @Limit";
            p.Add("Limit", limit);

            var urgent = (await conn.QueryAsync<SupabaseCommunityPostDto>(sql, p)).ToList();

            return urgent.Select(dto => new EmergencyAlertItemViewModel
            {
                PostId = dto.id,
                CropName = dto.crop_category ?? "ফসল",
                IssueTitle = dto.issue_category ?? "জরুরি সমস্যা",
                AuthorName = dto.author_name,
                Location = dto.upazila ?? dto.district ?? "বাংলাদেশ",
                TimeAgo = FormatTimeAgo(dto.created_at),
                UrgencyLevel = dto.urgency_level
            }).ToList();
        }

        public async Task<List<TopMentorViewModel>> GetTopMentorsAsync(int limit = 3)
        {
            var topUsers = await _userManager.Users
                .OrderByDescending(u => u.LoyaltyPoints)
                .Take(limit)
                .ToListAsync();

            using var conn = CreateConnection();

            var result = new List<TopMentorViewModel>();
            int rank = 1;

            foreach (var u in topUsers)
            {
                var solutions = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM community_comments WHERE author_id = @AuthorId AND is_accepted_solution = true",
                    new { AuthorId = u.Id });

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

        #region Helper Mappers & File Saving

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

        private CommunityPostViewModel MapDtoToPostViewModel(
            SupabaseCommunityPostDto dto,
            List<SupabasePostMediaDto> mediaList,
            List<SupabaseCommentDto> allComments,
            ApplicationUser? currentUser,
            bool isLiked,
            string? reactionType,
            bool isBookmarked)
        {
            var isAuthor = currentUser != null && dto.author_id == currentUser.Id;
            var isAdmin = currentUser != null && currentUser.UserRole == AppRoles.Admin;

            var topComments = allComments
                .Where(c => c.parent_comment_id == null)
                .Select(c => MapDtoToCommentViewModel(c, allComments, dto.author_id, currentUser))
                .ToList();

            CommunityCommentViewModel? acceptedModel = null;
            if (dto.accepted_comment_id.HasValue)
            {
                var acc = allComments.FirstOrDefault(c => c.id == dto.accepted_comment_id.Value);
                if (acc != null) acceptedModel = MapDtoToCommentViewModel(acc, allComments, dto.author_id, currentUser);
            }

            return new CommunityPostViewModel
            {
                Id = dto.id,
                AuthorId = dto.author_id,
                AuthorName = dto.author_name,
                AuthorRole = dto.author_role ?? "Farmer",
                AuthorAvatarUrl = null,
                AuthorIsVerified = dto.author_is_verified,
                Content = dto.content,
                PostType = dto.post_type,
                CropCategory = dto.crop_category,
                IssueCategory = dto.issue_category,
                UrgencyLevel = dto.urgency_level ?? "Normal",
                CropAge = dto.crop_age,
                AffectedArea = dto.affected_area,
                Upazila = dto.upazila,
                District = dto.district,
                AudioRecordingUrl = dto.audio_recording_url,
                AudioDurationSeconds = dto.audio_duration_seconds,
                IsHelpRequest = dto.is_help_request,
                IsSolved = dto.is_solved,
                IsPinned = dto.is_pinned,
                LikeCount = dto.like_count,
                CommentCount = dto.comment_count,
                ShareCount = dto.share_count,
                ViewCount = dto.view_count,
                CreatedAt = dto.created_at,
                TimeAgo = FormatTimeAgo(dto.created_at),
                MediaList = mediaList.Select(m => new PostMediaViewModel
                {
                    Id = m.id,
                    MediaUrl = m.media_url,
                    MediaType = m.media_type,
                    SortOrder = m.display_order
                }).ToList(),
                Comments = topComments,
                AcceptedComment = acceptedModel,
                IsLikedByCurrentUser = isLiked,
                CurrentUserReactionType = reactionType,
                IsBookmarkedByCurrentUser = isBookmarked,
                CanDelete = isAuthor || isAdmin,
                CanAcceptSolution = (isAuthor || isAdmin) && dto.is_help_request
            };
        }

        private CommunityCommentViewModel MapDtoToCommentViewModel(
            SupabaseCommentDto c,
            List<SupabaseCommentDto> allComments,
            string postAuthorId,
            ApplicationUser? currentUser)
        {
            var isPostAuthor = currentUser != null && currentUser.Id == postAuthorId;
            var isAdmin = currentUser != null && currentUser.UserRole == AppRoles.Admin;

            var replies = allComments
                .Where(r => r.parent_comment_id == c.id)
                .Select(r => MapDtoToCommentViewModel(r, allComments, postAuthorId, currentUser))
                .ToList();

            return new CommunityCommentViewModel
            {
                Id = c.id,
                PostId = c.post_id,
                ParentCommentId = c.parent_comment_id,
                AuthorId = c.author_id,
                AuthorName = c.author_name,
                AuthorRole = c.author_role ?? "Farmer",
                AuthorAvatarUrl = null,
                AuthorIsVerified = c.author_is_verified,
                Content = c.content,
                AttachmentImageUrl = c.attachment_image_url,
                AudioRecordingUrl = c.audio_recording_url,
                AudioDurationSeconds = c.audio_duration_seconds,
                IsAcceptedSolution = c.is_accepted_solution,
                UpvoteCount = c.upvote_count,
                CreatedAt = c.created_at,
                TimeAgo = FormatTimeAgo(c.created_at),
                IsOfficialExpert = c.author_role == AppRoles.Admin,
                CanAcceptThisComment = (isPostAuthor || isAdmin) && !c.is_accepted_solution,
                Replies = replies
            };
        }

        private static string FormatTimeAgo(DateTime dt)
        {
            var span = DateTime.UtcNow - dt.ToUniversalTime();
            if (span.TotalMinutes < 1) return "এইমাত্র";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} মিনিট আগে";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} ঘণ্টা আগে";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays} দিন আগে";
            return dt.ToString("dd MMM yyyy");
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

        private async Task<string> SaveMediaFileAsync(IFormFile file, string folder)
        {
            var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads", "community", folder);
            if (!Directory.Exists(uploadsRoot))
            {
                Directory.CreateDirectory(uploadsRoot);
            }

            var extension = Path.GetExtension(file.FileName);
            var uniqueName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(uploadsRoot, uniqueName);

            using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/community/{folder}/{uniqueName}";
        }

        #endregion
    }

    #region Supabase DTOs

    internal class SupabaseCommunityPostDto
    {
        public int id { get; set; }
        public string author_id { get; set; } = "";
        public string author_name { get; set; } = "";
        public string? author_role { get; set; }
        public string? author_district { get; set; }
        public bool author_is_verified { get; set; }
        public string content { get; set; } = "";
        public string post_type { get; set; } = "Experience";
        public string? crop_category { get; set; }
        public string? issue_category { get; set; }
        public string? urgency_level { get; set; }
        public string? crop_age { get; set; }
        public string? affected_area { get; set; }
        public string? upazila { get; set; }
        public string? district { get; set; }
        public string? audio_recording_url { get; set; }
        public int? audio_duration_seconds { get; set; }
        public bool is_help_request { get; set; }
        public bool is_solved { get; set; }
        public bool is_pinned { get; set; }
        public bool is_flagged { get; set; }
        public int like_count { get; set; }
        public int comment_count { get; set; }
        public int share_count { get; set; }
        public int view_count { get; set; }
        public int? accepted_comment_id { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
    }

    internal class SupabasePostMediaDto
    {
        public int id { get; set; }
        public int post_id { get; set; }
        public string media_url { get; set; } = "";
        public string media_type { get; set; } = "Image";
        public string? file_name { get; set; }
        public long? file_size_bytes { get; set; }
        public int display_order { get; set; }
        public DateTime created_at { get; set; }
    }

    internal class SupabaseCommentDto
    {
        public int id { get; set; }
        public int post_id { get; set; }
        public int? parent_comment_id { get; set; }
        public string author_id { get; set; } = "";
        public string author_name { get; set; } = "";
        public string? author_role { get; set; }
        public bool author_is_verified { get; set; }
        public string content { get; set; } = "";
        public string? attachment_image_url { get; set; }
        public string? audio_recording_url { get; set; }
        public int? audio_duration_seconds { get; set; }
        public bool is_accepted_solution { get; set; }
        public int upvote_count { get; set; }
        public DateTime created_at { get; set; }
    }

    internal class SupabaseReactionDto
    {
        public int id { get; set; }
        public int? post_id { get; set; }
        public int? comment_id { get; set; }
        public string user_id { get; set; } = "";
        public string reaction_type { get; set; } = "Helpful";
        public DateTime created_at { get; set; }
    }

    #endregion
}
