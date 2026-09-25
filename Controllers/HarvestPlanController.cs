using System.Security.Claims;
using KrishiLink.BLL.Helpers;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;

namespace KrishiLink.Controllers
{
    [Authorize(Roles = AppRoles.Farmer)]
    public class HarvestPlanController : Controller
    {
        private readonly IHarvestPlanService _harvestPlans;
        private readonly ISeasonEconomicsService _economics;
        private readonly ICropCalendarService _calendar;
        private readonly UserManager<ApplicationUser> _users;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public HarvestPlanController(IHarvestPlanService harvestPlans, ISeasonEconomicsService economics, ICropCalendarService calendar,
            UserManager<ApplicationUser> users, IStringLocalizer<SharedResource> localizer)
        {
            _harvestPlans = harvestPlans;
            _economics = economics;
            _calendar = calendar;
            _users = users;
            _localizer = localizer;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var model = await _harvestPlans.GetPlansAsync(userId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> Create(string name, string? crop = null, string? note = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var (error, planId) = await _harvestPlans.CreatePlanAsync(userId, name, crop, note);
            if (error != null)
            {
                TempData["Error"] = _localizer[error].Value;
                return RedirectToAction("Index");
            }

            TempData["Success"] = _localizer["New harvest plan created. You can now add equipment and storage items."].Value;
            return RedirectToAction("Details", new { id = planId });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var model = await _harvestPlans.GetDetailsAsync(userId, id);
            if (model == null) return NotFound();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> AddItem(HarvestPlanItemInput input)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var (error, planId, itemId) = await _harvestPlans.AddItemAsync(userId, input);

            if (error != null)
            {
                TempData["Error"] = _localizer[error].Value;
                if (Url.IsLocalUrl(input.ReturnUrl))
                {
                    return Redirect(input.ReturnUrl!);
                }
                return input.PlanId.HasValue
                    ? RedirectToAction("Details", new { id = input.PlanId.Value })
                    : RedirectToAction("Index");
            }

            TempData["Success"] = _localizer["Item successfully added to your harvest plan."].Value;
            return RedirectToAction("Details", new { id = planId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> UpdateItem(int itemId, int planId, DateTime startDate, DateTime endDate, int? units, double? tons, string? note)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var error = await _harvestPlans.UpdateItemAsync(userId, itemId, startDate, endDate, units, tons, note);

            if (error != null)
            {
                TempData["Error"] = _localizer[error].Value;
            }
            else
            {
                TempData["Success"] = _localizer["Item updated successfully."].Value;
            }

            return RedirectToAction("Details", new { id = planId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> RemoveItem(int itemId, int planId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var error = await _harvestPlans.RemoveItemAsync(userId, itemId);

            if (error != null)
            {
                TempData["Error"] = _localizer[error].Value;
            }
            else
            {
                TempData["Success"] = _localizer["Item removed from harvest plan."].Value;
            }

            return RedirectToAction("Details", new { id = planId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> UpdatePlan(int planId, string name, string? description)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var error = await _harvestPlans.UpdatePlanAsync(userId, planId, name, description);

            if (error != null)
            {
                TempData["Error"] = _localizer[error].Value;
            }
            else
            {
                TempData["Success"] = _localizer["Harvest plan updated."].Value;
            }

            return RedirectToAction("Details", new { id = planId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> Delete(int planId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var error = await _harvestPlans.DeleteAsync(userId, planId);

            if (error != null)
            {
                TempData["Error"] = _localizer[error].Value;
                return RedirectToAction("Details", new { id = planId });
            }

            TempData["Success"] = _localizer["Harvest plan deleted."].Value;
            return RedirectToAction("Index");
        }

        [HttpPost]
        [Authorize(Policy = AppPolicies.VerifiedEmail)]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        [SlowPath("booking")]
        public async Task<IActionResult> Submit(int planId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _harvestPlans.SubmitAsync(userId, planId);

            if (result.Success)
            {
                TempData["Success"] = _localizer[result.Message ?? string.Empty].Value;
            }
            else
            {
                TempData["Error"] = _localizer[result.Message ?? string.Empty].Value;
            }

            return RedirectToAction("Details", new { id = planId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> Clone(int planId, int shiftDays = 365)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var (error, newPlanId) = await _harvestPlans.CloneAsync(userId, planId, shiftDays);

            if (error != null)
            {
                TempData["Error"] = _localizer[error].Value;
                return RedirectToAction("Details", new { id = planId });
            }

            TempData["Success"] = _localizer["Harvest plan duplicated with dates shifted by {0} days.", shiftDays].Value;
            return RedirectToAction("Details", new { id = newPlanId });
        }

        // ---------------------------------------------------------------- Season costs & returns (ECO-01)

        /// <summary>GET: /HarvestPlan/Season/5 — the season cost sheet and expected return for one plan.</summary>
        [HttpGet]
        public async Task<IActionResult> Season(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var sheet = await _economics.GetSheetAsync(userId, id, HttpContext.RequestAborted);
            if (sheet is null) return NotFound();
            return View(new SeasonSheetViewModel
            {
                Sheet = sheet,
                LandUnit = (await _users.GetUserAsync(User))?.PreferredLandUnit ?? LandUnit.Decimal,
                Crops = (await _calendar.GetAllCropsAsync())
                    .OrderBy(c => c.Name, StringComparer.Ordinal).ToList()
            });
        }

        /// <summary>POST: /HarvestPlan/SeasonInputs — crop, land size (in the farmer's unit) and their own expected price.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> SeasonInputs(SeasonInputsForm form)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            double? land = form.LandSize is { } size ? UnitFormat.ToDecimals(size, form.LandUnit) : null;
            decimal? pricePerKg = form.Price is { } price ? (form.PricePerMaund ? price / (decimal)UnitFormat.KgPerMaund : price) : null;
            var error = !ModelState.IsValid
                ? "Please check the values you entered."
                : await _economics.UpdateInputsAsync(userId, form.PlanId, form.CropEntryId, land, pricePerKg, HttpContext.RequestAborted);
            if (error is null) TempData["SuccessMessage"] = _localizer["Season details saved."].Value;
            else TempData["ErrorMessage"] = _localizer[error].Value;
            return RedirectToAction(nameof(Season), new { id = form.PlanId });
        }

        /// <summary>POST: /HarvestPlan/AddSeasonCost — a cost paid outside KrishiLink (seed, fertilizer, labour...).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> AddSeasonCost(int planId, string category, string? note, decimal amount, DateTime? incurredOn)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var error = await _economics.AddCostAsync(userId, planId, category, note, amount, incurredOn, HttpContext.RequestAborted);
            if (error is null) TempData["SuccessMessage"] = _localizer["Cost added to the season."].Value;
            else TempData["ErrorMessage"] = _localizer[error].Value;
            return RedirectToAction(nameof(Season), new { id = planId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> DeleteSeasonCost(int planId, int costId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            if (!await _economics.DeleteCostAsync(userId, planId, costId, HttpContext.RequestAborted)) return NotFound();
            TempData["SuccessMessage"] = _localizer["Cost removed."].Value;
            return RedirectToAction(nameof(Season), new { id = planId });
        }

        /// <summary>GET: /HarvestPlan/SeasonPdf/5 — the season summary as a printable PDF.</summary>
        [HttpGet]
        [EnableRateLimiting(RateLimitPolicies.ReadJson)]
        public async Task<IActionResult> SeasonPdf(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var sheet = await _economics.GetSheetAsync(userId, id, HttpContext.RequestAborted);
            if (sheet is null) return NotFound();
            var farmer = await _users.GetUserAsync(User);
            var document = new SeasonSummaryDocument(sheet, farmer?.FullName ?? string.Empty, DateTime.UtcNow);
            return File(QuestPDF.Fluent.GenerateExtensions.GeneratePdf(document), "application/pdf", document.FileName);
        }

        /// <summary>POST: /HarvestPlan/StartFromAdvice — turns saved crop advice into a season plan with its crop and land size.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> StartFromAdvice(int savedAdvisoryId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var (error, planId) = await _economics.StartFromAdviceAsync(userId, savedAdvisoryId, HttpContext.RequestAborted);
            if (error is not null || planId is null)
            {
                TempData["ErrorMessage"] = _localizer[error ?? "Could not start a season plan."].Value;
                return RedirectToAction("Dashboard", "Farmer");
            }
            TempData["SuccessMessage"] = _localizer["Season plan started from your saved advice. Add your costs and expected price."].Value;
            return RedirectToAction(nameof(Season), new { id = planId });
        }

        [HttpGet]
        [EnableRateLimiting(RateLimitPolicies.ReadJson)]
        public async Task<IActionResult> GetDraftPlans()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var plans = await _harvestPlans.GetDraftPlansForSelectionAsync(userId);
            return Json(plans);
        }
    }
}
