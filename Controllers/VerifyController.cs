using System.Security.Claims;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace KrishiLink.Controllers
{
    public class VerifyController : Controller
    {
        private readonly IBookingService _bookings;
        private readonly IStorageIntakeService _intakeService;
        private readonly IOptions<AppOptions> _appOptions;
        private readonly IWebHostEnvironment _env;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public VerifyController(IBookingService bookings, IStorageIntakeService intakeService, IOptions<AppOptions> appOptions, IWebHostEnvironment env,
            IStringLocalizer<SharedResource> localizer)
        {
            _localizer = localizer;
            _bookings = bookings;
            _intakeService = intakeService;
            _appOptions = appOptions;
            _env = env;
        }

        private string PublicOrigin => AppLinks.PublicOrigin(_appOptions, _env, Request);

        /// <summary>
        /// Booking Verification Certificate endpoint: /Verify/KL-EQ-2026-001?t={qr secret}
        /// The two parties and administrators see the full certificate when signed in; anyone else must present
        /// the secret from the QR link, and then sees no phone numbers and no money.
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        [Route("Verify/{code}")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Index(string code, [FromQuery(Name = "t")] string? t)
        {
            if (string.IsNullOrWhiteSpace(code))
                return RedirectToAction("Index", "Home");

            Response.Headers.CacheControl = "no-store";
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var model = await _bookings.GetVerificationByCodeAsync(code, t, currentUserId, User.IsInRole(AppRoles.Admin), PublicOrigin);

            return View(model);
        }

        /// <summary>
        /// Allows authenticated equipment or godown owners to perform 1-click status actions (Accept, Confirm Pickup/Handover, Complete) directly from the scanned verification certificate.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Verify/{code}/Action")]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> QuickAction(string code, string action)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var error = await _bookings.QuickVerifyActionAsync(ownerId, code, action);
            if (error is null)
            {
                var actionMsg = action.ToLowerInvariant() switch
                {
                    "accept" => "Booking request accepted successfully!",
                    "reject" => "Booking request rejected.",
                    "confirm-pickup" => "Farmer identity verified & handover confirmed! Booking is now marked as Completed.",
                    "complete" => "Booking marked as Completed & Handover Settled.",
                    _ => "Action completed successfully."
                };
                TempData["SuccessMessage"] = _localizer[actionMsg].Value;
            }
            else
            {
                TempData["ErrorMessage"] = _localizer[error].Value;
            }

            return RedirectToAction(nameof(Index), new { code });
        }

        /// <summary>
        /// Public & Anonymous Warehouse Receipt Verification endpoint: /Verify/Receipt/KL-WR-2026-00001
        /// Scannable by any smartphone camera. Never exposes farmer personal details.
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [Route("Verify/Receipt/{receiptNumber}")]
        public async Task<IActionResult> Receipt(string receiptNumber)
        {
            Response.Headers.CacheControl = "no-store";
            var model = await _intakeService.GetVerificationAsync(receiptNumber, PublicOrigin);
            return View(model);
        }
    }
}
