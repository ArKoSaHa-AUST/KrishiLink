using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    [Authorize]
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
        public async Task<IActionResult> Submit([FromForm] SubmitReviewViewModel model)
        {
            var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                         Request.Headers["Accept"].ToString().Contains("application/json");

            if (!ModelState.IsValid)
            {
                var errors = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                if (isAjax)
                {
                    return Json(new { success = false, message = errors });
                }
                TempData["ErrorMessage"] = errors;
                return RedirectToAction("Index", "Bookings");
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                if (isAjax) return Json(new { success = false, message = "Please sign in to submit a review." });
                return Challenge();
            }

            var result = await _reviewService.SubmitReviewAsync(userId, model);

            if (isAjax)
            {
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
    }
}
