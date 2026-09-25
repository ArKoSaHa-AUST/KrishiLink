using System.Security.Claims;
using System.Threading.Tasks;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace KrishiLink.Controllers
{
    [Authorize(Roles = AppRoles.Farmer)]
    public class FavoritesController : Controller
    {
        private readonly IFavoriteService _favorites;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public FavoritesController(IFavoriteService favorites, IStringLocalizer<SharedResource> localizer)
        {
            _favorites = favorites;
            _localizer = localizer;
        }

        private string FarmerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        /// <summary>GET: /Favorites — farmer's favorited equipment and godowns with live availability hints.</summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _favorites.GetAsync(FarmerId);
            return View(model);
        }

        /// <summary>POST: /Favorites/Toggle — AJAX endpoint to toggle favorite status.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(string type, int id)
        {
            var (isFavorite, count, error) = await _favorites.ToggleAsync(FarmerId, type, id);
            if (error != null)
            {
                return Json(new { success = false, message = error });
            }

            return Json(new { success = true, isFavorite, count });
        }

        /// <summary>POST: /Favorites/Remove — form action to remove a favorite and redirect back to the page.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(string type, int id)
        {
            var error = await _favorites.RemoveAsync(FarmerId, type, id);
            if (error is null) TempData["SuccessMessage"] = _localizer["Item removed from your favorites."].Value;
            else TempData["ErrorMessage"] = _localizer[error].Value;

            return RedirectToAction(nameof(Index));
        }
    }
}
