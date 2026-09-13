using System.Security.Claims;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    public class GodownController : Controller
    {
        private readonly IGodownService _godowns;

        public GodownController(IGodownService godowns)
        {
            _godowns = godowns;
        }

        /// <summary>GET: /Godown — browse storage facilities with server-side filtering.</summary>
        public async Task<IActionResult> Index(GodownSearchCriteria criteria)
        {
            criteria.CurrentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return View(await _godowns.BrowseAsync(criteria));
        }

        /// <summary>GET: /Godown/FilterData — JSON endpoint for live filtering.</summary>
        [HttpGet]
        public async Task<IActionResult> FilterData(GodownSearchCriteria criteria)
        {
            criteria.CurrentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var model = await _godowns.BrowseAsync(criteria);
            var items = model.GodownList.Select(g => new
            {
                id = g.Id,
                name = g.Name,
                storageType = g.StorageType,
                location = g.Location,
                district = g.District,
                distanceKm = g.DistanceKm,
                totalCapacityTons = g.TotalCapacityTons,
                availableCapacityTons = g.AvailableCapacityTons,
                capacityDisplay = $"{g.AvailableCapacityTons:N0} / {g.TotalCapacityTons:N0} Tons available",
                pricePerTonPerMonth = g.PricePerTonPerMonth,
                pricePerTonPerMonthFormatted = $"৳{g.PricePerTonPerMonth:N0}",
                dailyRatePerTonFormatted = g.DailyRatePerTon.HasValue ? $"৳{g.DailyRatePerTon.Value:N0}" : null,
                isAvailable = g.IsAvailable,
                isFavorite = g.IsFavorite,
                status = g.Status,
                imageUrl = g.ImageUrl,
                ownerName = g.OwnerName,
                ownerIsVerified = g.OwnerIsVerified,
                ownerVerificationStatus = g.OwnerVerificationStatus,
                rating = g.Rating,
                reviewCount = g.ReviewCount,
                ownerRating = g.OwnerRating,
                ownerReviewCount = g.OwnerReviewCount,
                facilities = g.Facilities,
                detailsUrl = Url.Action(nameof(Details), "Godown", new { id = g.Id })
            });

            return Json(new
            {
                totalCount = model.TotalCount,
                page = model.Page,
                pageSize = model.PageSize,
                totalPages = model.TotalPages,
                hasPreviousPage = model.HasPreviousPage,
                hasNextPage = model.HasNextPage,
                items
            });
        }

        /// <summary>GET: /Godown/Details/3</summary>
        public async Task<IActionResult> Details(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var model = await _godowns.GetDetailsAsync(id, currentUserId);
            return model is null ? NotFound() : View(model);
        }

        /// <summary>POST: /Godown/SubmitBooking — farmer requests storage space.</summary>
        [HttpPost]
        [Authorize(Roles = AppRoles.Farmer)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitBooking(GodownDetailViewModel model)
        {
            var farmerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var (error, bookingId) = await _godowns.RequestStorageWithResultAsync(farmerId, model, model.AppliedPromoCode, model.PointsUsed);

            if (error is null && bookingId.HasValue)
            {
                TempData["SuccessMessage"] = $"Storage space request ({model.RequestedCapacityTons:N0} Tons) sent successfully! Here is your official booking confirmation pass.";
                return RedirectToAction("Confirmation", "Bookings", new { type = "Godown", id = bookingId.Value, justCreated = true });
            }

            TempData["ErrorMessage"] = error ?? "Failed to submit storage booking.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }
    }
}
