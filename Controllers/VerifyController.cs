using System.Security.Claims;
using KrishiLink.BLL.Services;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    public class VerifyController : Controller
    {
        private readonly IBookingService _bookings;
        private readonly IStorageIntakeService _intakeService;

        public VerifyController(IBookingService bookings, IStorageIntakeService intakeService)
        {
            _bookings = bookings;
            _intakeService = intakeService;
        }

        /// <summary>
        /// Public & Owner Booking Verification Certificate endpoint: /Verify/KL-EQ-2026-001
        /// Scannable by any smartphone camera or KrishiLink in-app QR scanner.
        /// </summary>
        [HttpGet]
        [Route("Verify/{code}")]
        public async Task<IActionResult> Index(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return RedirectToAction("Index", "Home");

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var host = $"{Request.Scheme}://{Request.Host}";
            var model = await _bookings.GetVerificationByCodeAsync(code, currentUserId, host);

            return View(model);
        }

        /// <summary>
        /// Allows authenticated equipment or godown owners to perform 1-click status actions (Accept, Confirm Pickup/Handover, Complete) directly from the scanned verification certificate.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Verify/{code}/Action")]
        public async Task<IActionResult> QuickAction(string code, string action)
        {
            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(ownerId))
                return Challenge();

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
                TempData["SuccessMessage"] = actionMsg;
            }
            else
            {
                TempData["ErrorMessage"] = error;
            }

            return RedirectToAction(nameof(Index), new { code });
        }

        /// <summary>
        /// Public & Anonymous Warehouse Receipt Verification endpoint: /Verify/Receipt/KL-WR-2026-00001
        /// Scannable by any smartphone camera. Never exposes farmer personal details.
        /// </summary>
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [Route("Verify/Receipt/{receiptNumber}")]
        public async Task<IActionResult> Receipt(string receiptNumber)
        {
            Response.Headers.CacheControl = "no-store";
            var model = await _intakeService.GetVerificationAsync(receiptNumber);
            return View(model);
        }
    }
}
