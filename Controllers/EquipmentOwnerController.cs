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

        public EquipmentOwnerController(IEquipmentService equipment, IEquipmentRevenueService revenueService, UserManager<ApplicationUser> userManager)
            : base(revenueService, userManager)
        {
            _equipment = equipment;
        }

        /// <summary>GET: /EquipmentOwner — dashboard with listings, pending requests and revenue KPI.</summary>
        public async Task<IActionResult> Index()
        {
            var model = await _equipment.GetOwnerDashboardAsync(OwnerId);
            model.OwnerName = await OwnerDisplayNameAsync();
            model.ThisMonthRevenue = ThisMonthRevenue;
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

        /// <summary>POST: /EquipmentOwner/Save — create or update a listing (with optional image uploads).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(EquipmentListingViewModel model, List<IFormFile>? imageFiles)
        {
            if (!ModelState.IsValid)
                return View("Create", model);

            model.ImageFiles = imageFiles;
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
    }
}
