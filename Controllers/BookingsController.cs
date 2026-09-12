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
        private readonly IPaymentService _payments;
        private readonly IStorageIntakeService _intakeService;

        public BookingsController(IBookingService bookings, IPaymentService payments, IStorageIntakeService intakeService)
        {
            _bookings = bookings;
            _payments = payments;
            _intakeService = intakeService;
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

        /// <summary>POST: /Bookings/Cancel — farmer withdraws a pending request or a not-yet-started booking; paid bookings are refunded.</summary>
        [HttpPost]
        [Authorize(Roles = AppRoles.Farmer)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(string type, int id, string? returnUrl)
        {
            var farmerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var (error, refunded) = await _bookings.CancelAsync(farmerId, type ?? string.Empty, id);
            if (error is not null) TempData["ErrorMessage"] = error;
            else TempData["SuccessMessage"] = refunded is null
                ? "Booking cancelled. The owner has been notified and the dates are free again."
                : $"Booking cancelled and ৳{refunded:N0} refunded to your payment method. The owner has been notified.";

            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
        }

        /// <summary>POST: /Bookings/Modify — farmer changes dates, units, or tons on a pending or accepted-unpaid booking.</summary>
        [HttpPost]
        [Authorize(Roles = AppRoles.Farmer)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Modify(string type, int id, DateTime? startDate, DateTime? endDate, int? units, double? tons, string? returnUrl)
        {
            if (startDate is null || endDate is null)
            {
                TempData["ErrorMessage"] = "Please provide both start and end dates.";
                return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
            }

            var farmerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var (error, needsReapproval) = await _bookings.ModifyAsync(farmerId, type ?? string.Empty, id, startDate.Value, endDate.Value, units, tons);

            if (error is not null)
            {
                TempData["ErrorMessage"] = error;
            }
            else
            {
                TempData["SuccessMessage"] = needsReapproval
                    ? "Booking updated. Because this request was previously accepted, it has returned to pending for owner re-approval."
                    : "Booking details updated successfully.";
            }

            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Index));
        }

        // ---------------------------------------------------------------- Escrow payment (simulated gateway)

        /// <summary>GET: /Bookings/Pay?type=Equipment&id=1 — checkout for an accepted, unpaid booking.</summary>
        [HttpGet]
        [Authorize(Roles = AppRoles.Farmer)]
        public async Task<IActionResult> Pay(string type, int id)
        {
            var farmerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var model = await _payments.GetCheckoutAsync(farmerId, type ?? string.Empty, id);
            if (model is null)
            {
                TempData["ErrorMessage"] = "This booking is not awaiting payment.";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        /// <summary>POST: /Bookings/Pay — creates the pending payment and hands the farmer to the gateway.</summary>
        [HttpPost]
        [Authorize(Roles = AppRoles.Farmer)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(string type, int id, string method, string? account)
        {
            var farmerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var (error, redirectUrl) = await _payments.InitiateAsync(farmerId, type ?? string.Empty, id, method ?? string.Empty, account);
            if (error is not null)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToAction(nameof(Pay), new { type, id });
            }
            return LocalRedirect(redirectUrl!);
        }

        /// <summary>GET: /Bookings/Gateway?ref=SIM-… — the sandbox "provider page" for the simulated gateway.</summary>
        [HttpGet]
        [Authorize(Roles = AppRoles.Farmer)]
        public async Task<IActionResult> Gateway(string @ref)
        {
            var payment = await _payments.GetByGatewayReferenceAsync(@ref ?? string.Empty);
            if (payment is null) return NotFound();
            if (payment.Status != PaymentStatus.Pending)
            {
                TempData[payment.Status == PaymentStatus.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
                    $"Payment {payment.Reference} is already {payment.Status.ToLowerInvariant()}.";
                return RedirectToAction(nameof(Index));
            }
            return View(payment);
        }

        /// <summary>POST: /Bookings/PaymentCallback — the gateway reports the outcome; idempotent.</summary>
        [HttpPost]
        [Authorize(Roles = AppRoles.Farmer)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PaymentCallback(string @ref, string outcome)
        {
            var (error, payment) = await _payments.CompleteAsync(@ref ?? string.Empty, outcome ?? string.Empty);
            if (payment is null) return NotFound();

            if (error is null)
                TempData["SuccessMessage"] = $"Payment of ৳{payment.Amount:N0} confirmed. Reference {payment.Reference}. Your booking is now confirmed.";
            else
                TempData["ErrorMessage"] = $"Payment failed: {error}. You can try again from your bookings.";

            return LocalRedirect(AppLinks.FarmerBookings(payment.BookingType, payment.BookingId));
        }

        /// <summary>GET: /Bookings/WarehouseReceipt/5 — download official Warehouse Receipt PDF as farmer.</summary>
        [HttpGet]
        [Authorize(Roles = AppRoles.Farmer)]
        [Route("Bookings/WarehouseReceipt/{id}")]
        public async Task<IActionResult> WarehouseReceipt(int id)
        {
            var farmerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _intakeService.GetReceiptPdfAsync(id, farmerId, isOwner: false);
            if (result is null)
                return NotFound();

            return File(result.Value.Content, "application/pdf", result.Value.FileName);
        }

        /// <summary>GET: /Bookings/Receipt?type=Equipment&id=1 — download official payment receipt PDF.</summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Receipt(string type, int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var isOwner = User.IsInRole(AppRoles.EquipmentOwner) || User.IsInRole(AppRoles.GodownOwner);
            var host = $"{Request.Scheme}://{Request.Host}";
            var result = await _bookings.GetReceiptPdfAsync(userId, isOwner, type, id, host);
            if (result is null)
                return NotFound();

            return File(result.Value.Content, "application/pdf", result.Value.FileName);
        }
    }
}
