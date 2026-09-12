using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    public class ReviewsController : Controller
    {
        private readonly IReviewService _reviewService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewsController(IReviewService reviewService, UserManager<ApplicationUser> userManager)
        {
            _reviewService = reviewService;
            _userManager = userManager;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.Farmer)]
        public async Task<IActionResult> Submit([FromForm] SubmitReviewViewModel model)
        {
            var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                         Request.Headers["Accept"].ToString().Contains("application/json");

            if (!ModelState.IsValid)
            {
                var errors = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                if (isAjax)
                {
                    return BadRequest(new { success = false, message = errors });
                }
                TempData["ErrorMessage"] = errors;
                return RedirectToAction("Index", "Bookings");
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                if (isAjax) return Unauthorized(new { success = false, message = "Please sign in to submit a review." });
                return Challenge();
            }

            var result = await _reviewService.SubmitReviewAsync(userId, model);

            if (isAjax)
            {
                if (!result.Success)
                {
                    return BadRequest(new { success = false, message = result.Message });
                }

                return Json(new
                {
                    success = result.Success,
                    message = result.Message,
                    averageRating = result.NewAverageRating,
                    totalReviews = result.NewTotalReviews,
                    rating = model.Rating,
                    comment = model.Comment,
                    bookingId = model.BookingId,
                    bookingType = model.BookingType
                });
            }

            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction("Index", "Bookings");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.EquipmentOwner},{AppRoles.GodownOwner}")]
        public async Task<IActionResult> Reply([FromForm] ReplyReviewViewModel model)
        {
            var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                         Request.Headers["Accept"].ToString().Contains("application/json");

            if (!ModelState.IsValid)
            {
                var errors = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                if (isAjax)
                {
                    return BadRequest(new { success = false, message = errors });
                }
                TempData["ErrorMessage"] = errors;
                return Redirect(Request.Headers["Referer"].ToString() ?? "/");
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                if (isAjax) return Unauthorized(new { success = false, message = "User authentication required." });
                return Challenge();
            }

            var (success, message) = await _reviewService.ReplyToReviewAsync(userId, model.ReviewId, model.Reply);

            if (isAjax)
            {
                if (!success)
                {
                    return BadRequest(new { success = false, message });
                }

                return Json(new
                {
                    success = true,
                    message,
                    reviewId = model.ReviewId,
                    reply = model.Reply.Trim(),
                    repliedAt = DateTime.UtcNow.ToString("dd MMM yyyy")
                });
            }

            if (success)
            {
                TempData["SuccessMessage"] = message;
            }
            else
            {
                TempData["ErrorMessage"] = message;
            }

            var referer = Request.Headers["Referer"].ToString();
            return !string.IsNullOrWhiteSpace(referer) ? Redirect(referer) : RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> List([FromQuery] string type, [FromQuery] int id, [FromQuery] int page = 1)
        {
            if (string.IsNullOrWhiteSpace(type) || id <= 0)
            {
                return BadRequest();
            }

            var reviews = await _reviewService.GetReviewsPagedAsync(type, id, page, 5);
            return PartialView("_ReviewList", reviews);
        }
    }
}
