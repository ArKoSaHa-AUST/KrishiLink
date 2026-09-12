using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    [Authorize(Roles = AppRoles.EquipmentOwner)]
    public class EquipmentOwnerController : OwnerRevenueControllerBase
    {
        private readonly IEquipmentService _equipment;
        private readonly IFileStorageService _files;

        public EquipmentOwnerController(
            IEquipmentService equipment,
            IFileStorageService files,
            IEquipmentRevenueService revenueService,
            UserManager<ApplicationUser> userManager)
            : base(revenueService, userManager)
        {
            _equipment = equipment;
            _files = files;
        }

        /// <summary>GET: /EquipmentOwner — dashboard with listings, pending requests and revenue KPI.</summary>
        public async Task<IActionResult> Index()
        {
            var model = await _equipment.GetOwnerDashboardAsync(OwnerId);
            model.OwnerName = await OwnerDisplayNameAsync();
            model.ThisMonthRevenue = ThisMonthRevenue;

            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                model.IsVerified = user.IsVerified;
                model.VerificationStatus = user.VerificationStatus ?? "Unverified";
            }

            return View(model);
        }

        /// <summary>GET: /EquipmentOwner/Requests — all incoming rental requests with Accept/Reject.</summary>
        [HttpGet]
        public async Task<IActionResult> Requests()
        {
            return View(await _equipment.GetOwnerRequestsAsync(OwnerId));
        }

        /// <summary>POST: /EquipmentOwner/RespondRequest — accept / reject / complete / undo a rental request.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RespondRequest(int id, string decision, string? reason = null)
        {
            var result = await _equipment.RespondAsync(OwnerId, id, decision ?? string.Empty, reason);
            if (!result.Success)
                return BadRequest(new { success = false, message = result.Error });

            var verb = decision!.ToLowerInvariant() switch
            {
                "accept" => "accepted",
                "reject" => "rejected",
                "complete" => "marked as completed",
                "undo" => "restored",
                _ => "updated"
            };
            return Json(new { success = true, message = $"Request #{id} {verb}.", autoRejected = result.AutoRejectedIds });
        }

        /// <summary>GET: /EquipmentOwner/PendingCount — polled by the dashboard for new-request notifications.</summary>
        [HttpGet]
        public async Task<IActionResult> PendingCount()
        {
            return Json(new { count = await _equipment.CountPendingAsync(OwnerId) });
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new EquipmentListingViewModel());
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _equipment.GetListingAsync(OwnerId, id);
            return model is null ? NotFound() : View("Create", model);
        }

        /// <summary>POST: /EquipmentOwner/Save — create or update a listing (with robust server-side image validation).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(MultipartBodyLengthLimit = 32 * 1024 * 1024)]
        [RequestSizeLimit(32 * 1024 * 1024)]
        public async Task<IActionResult> Save(EquipmentListingViewModel model, List<IFormFile>? imageFiles)
        {
            model.ImageFiles = imageFiles;

            if (imageFiles is not null && imageFiles.Count > 0)
            {
                var fileErrors = _files.ValidateFiles(imageFiles);
                foreach (var err in fileErrors)
                {
                    ModelState.AddModelError("ImageFiles", err);
                }
            }

            var totalImagesCount = (model.ExistingImageUrls?.Count ?? 0) + (imageFiles?.Count(f => f.Length > 0) ?? 0);
            if (totalImagesCount == 0)
            {
                ModelState.AddModelError("ImageFiles", "At least one photograph of the machinery is required.");
            }
            else if (totalImagesCount > 8)
            {
                ModelState.AddModelError("ImageFiles", "A listing cannot have more than 8 images.");
            }

            if (!ModelState.IsValid)
                return View("Create", model);

            var isEdit = model.IsEditMode;
            if (!await _equipment.SaveListingAsync(OwnerId, model))
                return NotFound();

            TempData["SuccessMessage"] = $"Equipment listing '{model.Name}' successfully {(isEdit ? "updated" : "created")}!";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>GET: /EquipmentOwner/Availability/5?month=2026-09-01 — colour-coded availability calendar.</summary>
        public async Task<IActionResult> Availability(int id, DateTime? month = null, bool saved = false)
        {
            var model = await _equipment.GetAvailabilityAsync(OwnerId, id, month);
            if (model is null) return NotFound();

            model.IsSaved = saved;
            return View("Availability", model);
        }

        /// <summary>POST: /EquipmentOwner/SaveAvailability — replaces the owner-blocked dates for the posted month.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAvailability(int listingId, DateTime month, List<DateTime>? blockedDates)
        {
            if (!await _equipment.SaveAvailabilityAsync(OwnerId, listingId, month, blockedDates ?? new List<DateTime>()))
                return NotFound();

            return RedirectToAction(nameof(Availability), new { id = listingId, month = month.ToString("yyyy-MM-dd"), saved = true });
        }

        /// <summary>GET: /EquipmentOwner/Maintenance/5 — Equipment Health Tracker dashboard.</summary>
        [HttpGet]
        public async Task<IActionResult> Maintenance(int id)
        {
            var model = await _equipment.GetMaintenanceDashboardAsync(OwnerId, id);
            if (model is null) return NotFound();

            return View(model);
        }

        /// <summary>POST: /EquipmentOwner/AddMaintenance — Log a new maintenance or servicing event.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMaintenance(int equipmentId, EquipmentMaintenanceRecordInputModel model)
        {
            if (!ModelState.IsValid)
            {
                var dashboard = await _equipment.GetMaintenanceDashboardAsync(OwnerId, equipmentId);
                if (dashboard is null) return NotFound();
                dashboard.NewRecord = model;
                return View("Maintenance", dashboard);
            }

            var (success, error) = await _equipment.AddMaintenanceRecordAsync(OwnerId, equipmentId, model);
            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to save maintenance record.";
                return RedirectToAction(nameof(Maintenance), new { id = equipmentId });
            }

            TempData["SuccessMessage"] = "Maintenance event successfully logged!";
            return RedirectToAction(nameof(Maintenance), new { id = equipmentId });
        }

        /// <summary>POST: /EquipmentOwner/DeleteMaintenance — Delete a logged maintenance record.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMaintenance(int recordId, int equipmentId)
        {
            var success = await _equipment.DeleteMaintenanceRecordAsync(OwnerId, recordId);
            if (!success)
            {
                TempData["ErrorMessage"] = "Could not delete the maintenance record.";
            }
            else
            {
                TempData["SuccessMessage"] = "Maintenance record removed.";
            }

            return RedirectToAction(nameof(Maintenance), new { id = equipmentId });
        }

        /// <summary>GET: /EquipmentOwner/Pricing/5 — Manage seasonal and weekend rate rules.</summary>
        [HttpGet]
        public async Task<IActionResult> Pricing(int id)
        {
            var model = await _equipment.GetPricingAsync(OwnerId, id);
            if (model is null) return NotFound();

            return View("Pricing", model);
        }

        /// <summary>POST: /EquipmentOwner/SaveRateRule — Create or update a dynamic rate rule.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRateRule(int equipmentId, RateRuleInputModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["ErrorMessage"] = errors;
                return RedirectToAction(nameof(Pricing), new { id = equipmentId });
            }

            var error = await _equipment.SaveRateRuleAsync(OwnerId, equipmentId, model);
            if (error != null)
            {
                TempData["ErrorMessage"] = error;
            }
            else
            {
                TempData["SuccessMessage"] = "Rate rule saved successfully!";
            }

            return RedirectToAction(nameof(Pricing), new { id = equipmentId });
        }

        /// <summary>POST: /EquipmentOwner/ToggleRateRule — Activate or deactivate a rate rule.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleRateRule(int ruleId, int equipmentId)
        {
            var success = await _equipment.ToggleRateRuleAsync(OwnerId, ruleId);
            if (!success)
            {
                TempData["ErrorMessage"] = "Could not activate rule (ensure no overlapping active season rules or duplicate active weekend rules).";
            }
            else
            {
                TempData["SuccessMessage"] = "Rate rule status updated.";
            }

            return RedirectToAction(nameof(Pricing), new { id = equipmentId });
        }

        /// <summary>POST: /EquipmentOwner/DeleteRateRule — Delete an equipment rate rule.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRateRule(int ruleId, int equipmentId)
        {
            var success = await _equipment.DeleteRateRuleAsync(OwnerId, ruleId);
            if (!success)
            {
                TempData["ErrorMessage"] = "Could not delete the rate rule.";
            }
            else
            {
                TempData["SuccessMessage"] = "Rate rule removed successfully.";
            }

            return RedirectToAction(nameof(Pricing), new { id = equipmentId });
        }
    }
}
