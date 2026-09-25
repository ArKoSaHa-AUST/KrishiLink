using System.Security.Claims;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;

namespace KrishiLink.Controllers
{
    public class EquipmentController : Controller
    {
        private readonly IEquipmentService _equipment;
        private readonly IPriceBenchmarkService _benchmarks;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public EquipmentController(IEquipmentService equipment, IPriceBenchmarkService benchmarks, IStringLocalizer<SharedResource> localizer)
        {
            _equipment = equipment;
            _benchmarks = benchmarks;
            _localizer = localizer;
        }

        /// <summary>GET: /Equipment — browse & search with server-side filtering.</summary>
        [AllowAnonymous]
        [SlowPath("search")]
        public async Task<IActionResult> Index(EquipmentSearchCriteria criteria)
        {
            criteria.CurrentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return View(await _equipment.BrowseAsync(criteria));
        }

        /// <summary>GET: /Equipment/FilterData — JSON endpoint for live filtering.</summary>
        [AllowAnonymous]
        [HttpGet]
        [EnableRateLimiting(RateLimitPolicies.ReadJson)]
        [SlowPath("search")]
        public async Task<IActionResult> FilterData(EquipmentSearchCriteria criteria)
        {
            criteria.CurrentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var model = await _equipment.BrowseAsync(criteria);
            var items = model.EquipmentList.Select(e => new
            {
                id = e.Id,
                name = e.Name,
                category = e.Category,
                dailyRate = e.DailyRate,
                dailyRateFormatted = $"৳{e.DailyRate:N0}",
                hasRateRules = e.HasRateRules,
                hourlyRate = e.HourlyRate,
                hourlyRateFormatted = e.HourlyRate.HasValue ? $"৳{e.HourlyRate.Value:N0}" : null,
                location = e.Location,
                district = e.District,
                distanceKm = e.DistanceKm,
                latitude = e.Latitude,
                longitude = e.Longitude,
                isAvailable = e.IsAvailable,
                isFavorite = e.IsFavorite,
                status = e.Status,
                quantity = e.Quantity,
                imageUrl = e.ImageUrl,
                ownerName = e.OwnerName,
                ownerIsVerified = e.OwnerIsVerified,
                ownerVerificationStatus = e.OwnerVerificationStatus,
                rating = e.Rating,
                reviewCount = e.ReviewCount,
                ownerRating = e.OwnerRating,
                ownerReviewCount = e.OwnerReviewCount,
                lastServicedDaysAgo = e.LastServicedDaysAgo,
                lastServicedText = e.LastServicedText,
                detailsUrl = Url.Action(nameof(Details), "Equipment", new { id = e.Id })
            });

            return Json(new
            {
                totalCount = model.TotalCount,
                fuzzy = model.IsFuzzyMatch,
                near = model.IsNearSearch,
                page = model.Page,
                pageSize = model.PageSize,
                totalPages = model.TotalPages,
                hasPreviousPage = model.HasPreviousPage,
                hasNextPage = model.HasNextPage,
                items
            });
        }

        /// <summary>GET: /Equipment/Quote?id=&start=&end=&units=1 — Live rule-aware rental price quote.</summary>
        [AllowAnonymous]
        [HttpGet]
        [EnableRateLimiting(RateLimitPolicies.ReadJson)]
        public async Task<IActionResult> Quote(int id, DateTime? start, DateTime? end, int units = 1)
        {
            var quote = await _equipment.QuoteAsync(id, start, end, units);
            if (quote is null) return NotFound();

            return Json(new
            {
                ok = quote.Ok,
                error = quote.Error,
                days = quote.Days,
                gross = quote.Gross,
                minDays = quote.MinDays,
                units = quote.Units,
                freeUnits = quote.FreeUnits,
                quantity = quote.Quantity,
                breakdown = quote.Breakdown.Select(b => new
                {
                    rate = b.Rate,
                    days = b.Days,
                    label = b.Label,
                    subtotal = b.Subtotal
                }),
                description = quote.Description
            });
        }

        /// <summary>GET: /Equipment/FreeUnits?id=&start=&end= — returns free units for range.</summary>
        [AllowAnonymous]
        [HttpGet]
        [EnableRateLimiting(RateLimitPolicies.ReadJson)]
        public async Task<IActionResult> FreeUnits(int id, DateTime? start, DateTime? end)
        {
            var eq = await _equipment.GetDetailsAsync(id);
            if (eq == null) return NotFound();

            if (!start.HasValue || !end.HasValue)
            {
                return Json(new { free = eq.Quantity, quantity = eq.Quantity });
            }

            var free = await _equipment.FreeUnitsAsync(id, start.Value, end.Value);
            return Json(new { free, quantity = eq.Quantity });
        }

        /// <summary>GET: /Equipment/Details/5 — details & rental request form.</summary>
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id, bool requestSent = false)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var model = await _equipment.GetDetailsAsync(id, currentUserId);
            if (model is null) return NotFound();

            model.IsRequestSubmitted = requestSent;
            if (!string.IsNullOrWhiteSpace(model.District))
            {
                model.PriceBenchmark = new PriceBenchmarkViewModel
                {
                    Benchmark = await _benchmarks.ForEquipmentAsync(model.Category, model.District, cancellationToken: HttpContext.RequestAborted),
                    Price = model.DailyRateAmount,
                    Category = model.Category,
                    District = model.District
                };
            }
            return View(model);
        }

        /// <summary>POST: /Equipment/SubmitRequest — farmer sends a rental request to the owner.</summary>
        [HttpPost]
        [Authorize(Roles = AppRoles.Farmer)]
        [Authorize(Policy = AppPolicies.VerifiedEmail)]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        [TypeFilter(typeof(AgentProposalGate), Arguments = new object[] { AgentProposal.EquipmentType })]
        [SlowPath("booking")]
        public async Task<IActionResult> SubmitRequest(EquipmentDetailViewModel model)
        {
            var farmerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var (error, bookingId) = await _equipment.RequestRentalWithResultAsync(
                farmerId,
                model.Id,
                model.StartDate,
                model.EndDate,
                model.Note,
                model.Units,
                model.AppliedPromoCode,
                model.PointsUsed
            );

            if (error is null && bookingId.HasValue)
            {
                TempData["SuccessMessage"] = _localizer["Rental request sent successfully! Here is your official booking confirmation pass."].Value;
                return RedirectToAction("Confirmation", "Bookings", new { type = "Equipment", id = bookingId.Value, justCreated = true });
            }

            TempData["ErrorMessage"] = _localizer[error ?? "Failed to submit rental request."].Value;
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }
    }
}
