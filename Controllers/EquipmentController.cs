using System.Security.Claims;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    public class EquipmentController : Controller
    {
        private readonly IEquipmentService _equipment;

        public EquipmentController(IEquipmentService equipment)
        {
            _equipment = equipment;
        }

        /// <summary>GET: /Equipment — browse & search with server-side filtering.</summary>
        public async Task<IActionResult> Index(EquipmentSearchCriteria criteria)
        {
            return View(await _equipment.BrowseAsync(criteria));
        }

        /// <summary>GET: /Equipment/FilterData — JSON endpoint for live filtering.</summary>
        [HttpGet]
        public async Task<IActionResult> FilterData(EquipmentSearchCriteria criteria)
        {
            var model = await _equipment.BrowseAsync(criteria);
            var items = model.EquipmentList.Select(e => new
            {
                id = e.Id,
                name = e.Name,
                category = e.Category,
                dailyRate = e.DailyRate,
                dailyRateFormatted = $"৳{e.DailyRate:N0}",
                hourlyRate = e.HourlyRate,
                hourlyRateFormatted = e.HourlyRate.HasValue ? $"৳{e.HourlyRate.Value:N0}" : null,
                location = e.Location,
                distanceKm = e.DistanceKm,
                isAvailable = e.IsAvailable,
                status = e.Status,
                imageUrl = e.ImageUrl,
                ownerName = e.OwnerName,
                ownerIsVerified = e.OwnerIsVerified,
                ownerVerificationStatus = e.OwnerVerificationStatus,
                rating = e.Rating,
                reviewCount = e.ReviewCount,
                lastServicedDaysAgo = e.LastServicedDaysAgo,
                lastServicedText = e.LastServicedText,
                detailsUrl = Url.Action(nameof(Details), "Equipment", new { id = e.Id })
            });

            return Json(new { totalCount = model.TotalCount, items });
        }

        /// <summary>GET: /Equipment/Details/5 — details & rental request form.</summary>
        public async Task<IActionResult> Details(int id, bool requestSent = false)
        {
            var model = await _equipment.GetDetailsAsync(id);
            if (model is null) return NotFound();

            model.IsRequestSubmitted = requestSent;
            return View(model);
        }

        /// <summary>POST: /Equipment/SubmitRequest — farmer sends a rental request to the owner.</summary>
        [HttpPost]
        [Authorize(Roles = AppRoles.Farmer)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRequest(EquipmentDetailViewModel model)
        {
            var farmerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var (error, bookingId) = await _equipment.RequestRentalWithResultAsync(farmerId, model.Id, model.StartDate, model.EndDate, model.Note);

            if (error is null && bookingId.HasValue)
            {
                TempData["SuccessMessage"] = "Rental request sent successfully! Here is your official booking confirmation pass.";
                return RedirectToAction("Confirmation", "Bookings", new { type = "Equipment", id = bookingId.Value, justCreated = true });
            }

            TempData["ErrorMessage"] = error ?? "Failed to submit rental request.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }
    }
}
