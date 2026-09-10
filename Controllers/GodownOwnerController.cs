using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    [Authorize(Roles = AppRoles.GodownOwner)]
    public class GodownOwnerController : OwnerRevenueControllerBase
    {
        private readonly IGodownService _godowns;

        public GodownOwnerController(IGodownService godowns, IGodownRevenueService revenueService, UserManager<ApplicationUser> userManager)
            : base(revenueService, userManager)
        {
            _godowns = godowns;
        }

        /// <summary>GET: /GodownOwner — dashboard with godowns, capacity utilisation and pending requests.</summary>
        public async Task<IActionResult> Index()
        {
            var model = await _godowns.GetOwnerDashboardAsync(OwnerId);
            model.OwnerName = await OwnerDisplayNameAsync();
            model.ThisMonthRevenue = ThisMonthRevenue;
            return View(model);
        }

        /// <summary>GET: /GodownOwner/Requests — all storage booking requests with Accept/Reject.</summary>
        [HttpGet]
        public async Task<IActionResult> Requests()
        {
            return View(await _godowns.GetOwnerRequestsAsync(OwnerId));
        }

        /// <summary>POST: /GodownOwner/RespondRequest — accept / reject / complete / undo a storage request.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RespondRequest(int id, string decision, string? reason = null)
        {
            if (!await _godowns.RespondAsync(OwnerId, id, decision ?? string.Empty, reason))
                return BadRequest(new { success = false, message = "This booking request could not be updated." });

            var verb = decision!.ToLowerInvariant() switch
            {
                "accept" => "accepted",
                "reject" => "rejected",
                "complete" => "marked as completed",
                "undo" => "restored",
                _ => "updated"
            };
            return Json(new { success = true, message = $"Booking request #{id} {verb}." });
        }

        /// <summary>GET: /GodownOwner/PendingCount — polled by the dashboard for new-request notifications.</summary>
        [HttpGet]
        public async Task<IActionResult> PendingCount()
        {
            return Json(new { count = await _godowns.CountPendingAsync(OwnerId) });
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new GodownListingViewModel());
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _godowns.GetListingAsync(OwnerId, id);
            return model is null ? NotFound() : View("Create", model);
        }

        /// <summary>POST: /GodownOwner/Save — create or update a storage listing (with optional image uploads).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(GodownListingViewModel model, List<IFormFile>? imageFiles)
        {
            if (!ModelState.IsValid)
                return View("Create", model);

            model.ImageFiles = imageFiles;
            var isEdit = model.IsEditMode;
            if (!await _godowns.SaveListingAsync(OwnerId, model))
                return NotFound();

            TempData["SuccessMessage"] = $"Storage facility '{model.Name}' successfully {(isEdit ? "updated" : "listed")}!";
            return RedirectToAction(nameof(Index));
        }
    }
}
