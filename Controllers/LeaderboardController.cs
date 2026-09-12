using System.Threading.Tasks;
using KrishiLink.BLL.Services;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    public class LeaderboardController : Controller
    {
        private readonly ILeaderboardService _leaderboardService;
        private readonly IBadgeService _badgeService;

        public LeaderboardController(
            ILeaderboardService leaderboardService,
            IBadgeService badgeService)
        {
            _leaderboardService = leaderboardService;
            _badgeService = badgeService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            [FromQuery] string category = "All",
            [FromQuery] string sortBy = "trust",
            [FromQuery] string? district = null,
            [FromQuery] bool refresh = false)
        {
            var model = await _leaderboardService.GetLeaderboardAsync(category, sortBy, district, forceRefresh: refresh);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> OwnerBadges(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest("Invalid user ID");

            var badges = await _badgeService.GetOwnerBadgesAsync(userId);
            return Json(new { success = true, badges });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Refresh(string? returnUrl = null)
        {
            _leaderboardService.InvalidateCache();
            TempData["SuccessMessage"] = "Leaderboard rankings refreshed successfully!";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index));
        }
    }
}
