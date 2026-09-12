using System.Security.Claims;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    [Authorize]
    public class BookingsController : Controller
    {
        private readonly IBookingService _bookings;

        public BookingsController(IBookingService bookings)
        {
            _bookings = bookings;
        }

        /// <summary>GET: /Bookings — the farmer's rental and storage booking history.</summary>
        [Authorize(Roles = AppRoles.Farmer)]
        public async Task<IActionResult> Index(
            string tab = "all",
            string status = "all",
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string? searchTerm = null)
        {
            var farmerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return View(await _bookings.GetHistoryAsync(farmerId, tab, status, dateFrom, dateTo, searchTerm));
        }

        /// <summary>GET: /Bookings/Confirmation?type=Equipment&id=1 — Printable Booking Confirmation Pass & QR Voucher.</summary>
        [HttpGet]
        public async Task<IActionResult> Confirmation(string type, int id, bool justCreated = false)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var host = $"{Request.Scheme}://{Request.Host}";
            var model = await _bookings.GetConfirmationAsync(userId, type, id, host, justCreated);

            if (model == null)
            {
                TempData["ErrorMessage"] = "Booking confirmation could not be found.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        /// <summary>GET: /Bookings/Pass/{code} — Lookup confirmation voucher by reference code.</summary>
        [HttpGet]
        [Route("Bookings/Pass/{code}")]
        public async Task<IActionResult> Pass(string code)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var host = $"{Request.Scheme}://{Request.Host}";
            var model = await _bookings.GetConfirmationByCodeAsync(userId, code, host);

            if (model == null)
            {
                TempData["ErrorMessage"] = "Booking pass could not be found.";
                return RedirectToAction(nameof(Index));
            }

            return View("Confirmation", model);
        }

        /// <summary>GET: /Bookings/GetQrData?type=Equipment&id=1 — JSON endpoint for fast modal QR popups.</summary>
        [HttpGet]
        public async Task<IActionResult> GetQrData(string type, int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var host = $"{Request.Scheme}://{Request.Host}";
            var model = await _bookings.GetConfirmationAsync(userId, type, id, host, false);

            if (model == null) return NotFound(new { error = "Booking not found" });

            return Json(new
            {
                bookingCode = model.BookingCode,
                itemName = model.ItemName,
                bookingType = model.BookingType,
                ownerName = model.OwnerName,
                dates = model.DateRangeDisplay,
                amount = model.CostDisplay,
                status = model.Status,
                verificationUrl = model.VerificationUrl,
                qrSvg = model.QrCodeSvg,
                qrBase64 = model.QrCodeBase64
            });
        }

        /// <summary>POST: /Bookings/Cancel — farmer withdraws a pending request or an accepted booking that hasn't started.</summary>
        [HttpPost]
        [Authorize(Roles = AppRoles.Farmer)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(string type, int id, string? returnUrl)
        {
            var farmerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var error = await _bookings.CancelAsync(farmerId, type ?? string.Empty, id);
            if (error is null) TempData["SuccessMessage"] = "Booking cancelled. The owner has been notified and the dates are free again.";
            else TempData["ErrorMessage"] = error;

            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
        }
    }
}
