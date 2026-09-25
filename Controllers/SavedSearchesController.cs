using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace KrishiLink.Controllers
{
    [Authorize(Roles = AppRoles.Farmer)]
    public class SavedSearchesController : Controller
    {
        private readonly ISavedSearchService _savedSearches;
        private readonly IWebHostEnvironment _env;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public SavedSearchesController(ISavedSearchService savedSearches, IWebHostEnvironment env, IStringLocalizer<SharedResource> localizer)
        {
            _savedSearches = savedSearches;
            _env = env;
            _localizer = localizer;
        }

        private string FarmerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        /// <summary>GET: /SavedSearches — list farmer's saved searches with criteria summary and alert controls.</summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _savedSearches.GetAsync(FarmerId);
            return View(model);
        }

        /// <summary>POST: /SavedSearches/Create — create a new saved search from the browse page modal.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SavedSearchInput input)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = _localizer["Please provide valid search criteria."].Value;
                if (!string.IsNullOrWhiteSpace(input.ReturnUrl) && Url.IsLocalUrl(input.ReturnUrl))
                {
                    return Redirect(input.ReturnUrl);
                }
                return RedirectToAction(nameof(Index));
            }

            var (success, error, id) = await _savedSearches.CreateAsync(FarmerId, input);
            if (!success)
            {
                TempData["ErrorMessage"] = _localizer[error ?? "Failed to save search."].Value;
            }
            else
            {
                TempData["SuccessMessage"] = _localizer["Search '{0}' has been saved! You will receive alerts when new listings match.", input.Name].Value;
            }

            if (!string.IsNullOrWhiteSpace(input.ReturnUrl) && Url.IsLocalUrl(input.ReturnUrl))
            {
                return Redirect(input.ReturnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>POST: /SavedSearches/ToggleAlerts — toggle availability alert notifications on/off.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAlerts(int id)
        {
            var updated = await _savedSearches.ToggleAlertsAsync(FarmerId, id);
            if (updated)
            {
                TempData["SuccessMessage"] = _localizer["Alert settings updated."].Value;
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>POST: /SavedSearches/Delete — remove a saved search.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _savedSearches.DeleteAsync(FarmerId, id);
            if (deleted)
            {
                TempData["SuccessMessage"] = _localizer["Saved search deleted."].Value;
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>POST: /SavedSearches/RunNow — Development-only trigger to evaluate searches immediately.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunNow(CancellationToken ct)
        {
            if (!_env.IsDevelopment()) return NotFound();

            var alertsSent = await _savedSearches.RunAlertsAsync(ct, onlyUserId: FarmerId);
            TempData["SuccessMessage"] = _localizer["[Dev] Evaluated saved searches. {0} alert(s) sent.", alertsSent].Value;
            return RedirectToAction(nameof(Index));
        }
    }
}
