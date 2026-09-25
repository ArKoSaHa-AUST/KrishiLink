using System.Threading.Tasks;
using KrishiLink.BLL.Services;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;

namespace KrishiLink.Controllers
{
    public class LeaderboardController : Controller
    {
        private readonly ILeaderboardService _leaderboardService;
        private readonly IBadgeService _badgeService;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public LeaderboardController(
            ILeaderboardService leaderboardService,
            IBadgeService badgeService,
            IStringLocalizer<SharedResource> localizer)
        {
            _leaderboardService = leaderboardService;
            _badgeService = badgeService;
            _localizer = localizer;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Index(
            [FromQuery] string category = "All",
            [FromQuery] string sortBy = "trust",
            [FromQuery] string? district = null,
            [FromQuery] string? division = null,
            [FromQuery] bool refresh = false)
        {
            // A division on its own means "anywhere in this division"; a district always wins.
            var model = await _leaderboardService.GetLeaderboardAsync(category, sortBy, district, division, forceRefresh: refresh);
            return View(model);
        }

        [AllowAnonymous]
        [HttpGet]
        [EnableRateLimiting(RateLimitPolicies.ReadJson)]
        public async Task<IActionResult> OwnerBadges(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest("Invalid user ID");

            var badges = await _badgeService.GetOwnerBadgesAsync(userId);
            return Json(new { success = true, badges });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public IActionResult Refresh(string? returnUrl = null)
        {
            if (_leaderboardService.InvalidateCache())
                TempData["SuccessMessage"] = _localizer["Leaderboard rankings refreshed successfully!"].Value;
            else
                TempData["ErrorMessage"] = _localizer["Rankings were refreshed moments ago. Please try again in a little while."].Value;

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index));
        }
    }
}
