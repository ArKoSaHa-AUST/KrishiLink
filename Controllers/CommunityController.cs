using System;
using System.Security.Claims;
using System.Threading.Tasks;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    public class CommunityController : Controller
    {
        private readonly ICommunityService _communityService;
        private readonly UserManager<ApplicationUser> _userManager;

        public CommunityController(
            ICommunityService communityService,
            UserManager<ApplicationUser> userManager)
        {
            _communityService = communityService;
            _userManager = userManager;
        }

        // GET: /Community
        [HttpGet]
        public async Task<IActionResult> Index(
            string? category = "All",
            string? district = null,
            string? urgency = null,
            string? sort = "Latest",
            string? q = null,
            int page = 1)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var model = await _communityService.GetFeedAsync(userId, category, district, urgency, sort, q, page, 15);
            return View(model);
        }

        // GET: /Community/Post/5
        [HttpGet]
        public async Task<IActionResult> Post(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var post = await _communityService.GetPostByIdAsync(id, userId);

            if (post == null)
            {
                TempData["ErrorMessage"] = "পোস্টটি পাওয়া যায়নি বা মুছে ফেলা হয়েছে।";
                return RedirectToAction(nameof(Index));
            }

            return View(post);
        }

        // POST: /Community/CreatePost
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePost([FromForm] CommunityPostCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "অনুগ্রহ করে পোস্টের সকল প্রয়োজনীয় তথ্য সঠিকভাবে প্রদান করুন।";
                return RedirectToAction(nameof(Index));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            try
            {
                var postId = await _communityService.CreatePostAsync(userId, model);
                TempData["SuccessMessage"] = "আপনার পোস্টটি সফলভাবে কমিউনিটিতে প্রকাশ করা হয়েছে।";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"পোস্ট তৈরি করতে সমস্যা হয়েছে: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Community/AddComment
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment([FromForm] CommunityCommentCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return BadRequest(new { success = false, message = "মন্তব্যের বিবরণ সঠিকভাবে লিখুন।" });
                }
                TempData["ErrorMessage"] = "মন্তব্য সঠিকভাবে লিখুন।";
                return RedirectToAction(nameof(Post), new { id = model.PostId });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var comment = await _communityService.AddCommentAsync(userId, model);

            if (comment == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return BadRequest(new { success = false, message = "মন্তব্য যোগ করা সম্ভব হয়নি।" });
                }
                return RedirectToAction(nameof(Post), new { id = model.PostId });
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_CommentItemPartial", comment);
            }

            TempData["SuccessMessage"] = "আপনার মন্তব্য বা সমাধান সফলভাবে যোগ করা হয়েছে।";
            return RedirectToAction(nameof(Post), new { id = model.PostId });
        }

        // GET: /Community/GetCommentsModal/{id}
        [HttpGet]
        public async Task<IActionResult> GetCommentsModal(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var post = await _communityService.GetPostByIdAsync(id, currentUserId);
            if (post == null)
            {
                return NotFound();
            }

            return PartialView("_CommentsModalPartial", post);
        }

        // POST: /Community/ToggleReaction
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleReaction(int? postId, int? commentId, string reactionType = CommunityReactionTypes.Helpful)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _communityService.ToggleReactionAsync(userId, postId, commentId, reactionType);

            return Json(new
            {
                success = result.Success,
                action = result.Action,
                likeCount = result.LikeCount,
                reactionType = result.ReactionType
            });
        }

        // POST: /Community/AcceptSolution
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptSolution(int postId, int commentId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _communityService.AcceptSolutionAsync(userId, postId, commentId);

            return Json(new
            {
                success = result.Success,
                message = result.Message
            });
        }

        // POST: /Community/ToggleBookmark
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBookmark(int postId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _communityService.ToggleBookmarkAsync(userId, postId);

            return Json(new
            {
                success = result.Success,
                isBookmarked = result.IsBookmarked
            });
        }

        // POST: /Community/ReportPost
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportPost(int postId, string reason)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _communityService.ReportPostAsync(userId, postId, reason);

            return Json(new
            {
                success = result,
                message = result ? "রিপোর্টটি সফলভাবে জমা নেওয়া হয়েছে।" : "রিপোর্ট জমা নেওয়া যায়নি।"
            });
        }

        // POST: /Community/DeletePost
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePost(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var isAdmin = User.IsInRole(AppRoles.Admin);

            var deleted = await _communityService.DeletePostAsync(userId, isAdmin, id);
            if (deleted)
            {
                TempData["SuccessMessage"] = "পোস্টটি মুছে ফেলা হয়েছে।";
            }
            else
            {
                TempData["ErrorMessage"] = "পোস্টটি মুছে ফেলা সম্ভব হয়নি।";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: /Community/GetFeedPartial (For AJAX dynamic tab filtering)
        [HttpGet]
        public async Task<IActionResult> GetFeedPartial(
            string? category = "All",
            string? district = null,
            string? urgency = null,
            string? sort = "Latest",
            string? q = null,
            int page = 1)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var model = await _communityService.GetFeedAsync(userId, category, district, urgency, sort, q, page, 15);
            return PartialView("_PostListPartial", model);
        }
    }
}
