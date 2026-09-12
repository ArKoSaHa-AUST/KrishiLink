using System.Security.Claims;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    [Authorize(Roles = AppRoles.Farmer)]
    public class HarvestPlanController : Controller
    {
        private readonly IHarvestPlanService _harvestPlans;

        public HarvestPlanController(IHarvestPlanService harvestPlans)
        {
            _harvestPlans = harvestPlans;
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
        public async Task<IActionResult> Create(string name, string? crop = null, string? note = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var (error, planId) = await _harvestPlans.CreatePlanAsync(userId, name, crop, note);
            if (error != null)
            {
                TempData["Error"] = error;
                return RedirectToAction("Index");
            }

            TempData["Success"] = "New harvest plan created. You can now add equipment and storage items.";
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
        public async Task<IActionResult> AddItem(HarvestPlanItemInput input)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var (error, planId, itemId) = await _harvestPlans.AddItemAsync(userId, input);

            if (error != null)
            {
                TempData["Error"] = error;
                if (!string.IsNullOrWhiteSpace(input.ReturnUrl))
                {
                    return Redirect(input.ReturnUrl);
                }
                return input.PlanId.HasValue
                    ? RedirectToAction("Details", new { id = input.PlanId.Value })
                    : RedirectToAction("Index");
            }

            TempData["Success"] = "Item successfully added to your harvest plan.";
            return RedirectToAction("Details", new { id = planId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateItem(int itemId, int planId, DateTime startDate, DateTime endDate, int? units, double? tons, string? note)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var error = await _harvestPlans.UpdateItemAsync(userId, itemId, startDate, endDate, units, tons, note);

            if (error != null)
            {
                TempData["Error"] = error;
            }
            else
            {
                TempData["Success"] = "Item updated successfully.";
            }

            return RedirectToAction("Details", new { id = planId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveItem(int itemId, int planId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var error = await _harvestPlans.RemoveItemAsync(userId, itemId);

            if (error != null)
            {
                TempData["Error"] = error;
            }
            else
            {
                TempData["Success"] = "Item removed from harvest plan.";
            }

            return RedirectToAction("Details", new { id = planId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePlan(int planId, string name, string? description)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var error = await _harvestPlans.UpdatePlanAsync(userId, planId, name, description);

            if (error != null)
            {
                TempData["Error"] = error;
            }
            else
            {
                TempData["Success"] = "Harvest plan updated.";
            }

            return RedirectToAction("Details", new { id = planId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int planId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var error = await _harvestPlans.DeleteAsync(userId, planId);

            if (error != null)
            {
                TempData["Error"] = error;
                return RedirectToAction("Details", new { id = planId });
            }

            TempData["Success"] = "Harvest plan deleted.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int planId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _harvestPlans.SubmitAsync(userId, planId);

            if (result.Success)
            {
                TempData["Success"] = result.Message;
            }
            else
            {
                TempData["Error"] = result.Message;
            }

            return RedirectToAction("Details", new { id = planId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Clone(int planId, int shiftDays = 365)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var (error, newPlanId) = await _harvestPlans.CloneAsync(userId, planId, shiftDays);

            if (error != null)
            {
                TempData["Error"] = error;
                return RedirectToAction("Details", new { id = planId });
            }

            TempData["Success"] = $"Harvest plan duplicated with dates shifted by {shiftDays} days.";
            return RedirectToAction("Details", new { id = newPlanId });
        }

        [HttpGet]
        public async Task<IActionResult> GetDraftPlans()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var plans = await _harvestPlans.GetDraftPlansForSelectionAsync(userId);
            return Json(plans);
        }
    }
}
