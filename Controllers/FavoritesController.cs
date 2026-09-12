using System.Security.Claims;
using System.Threading.Tasks;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    [Authorize(Roles = AppRoles.Farmer)]
    public class FavoritesController : Controller
    {
        private readonly IFavoriteService _favorites;

        public FavoritesController(IFavoriteService favorites)
        {
            _favorites = favorites;
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
            var (isFavorite, count, error) = await _favorites.ToggleAsync(FarmerId, type, id);
            TempData["SuccessMessage"] = "Item removed from your favorites.";
            return RedirectToAction(nameof(Index));
        }
    }
}
