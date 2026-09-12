using System.Security.Claims;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    [Authorize]
    public class LoyaltyController : Controller
    {
        private readonly ILoyaltyService _loyalty;

        public LoyaltyController(ILoyaltyService loyalty)
        {
            _loyalty = loyalty;
        }

        /// <summary>
        /// GET: /Loyalty or /Farmer/Loyalty — Farmer Loyalty Hub with balance, tier badge, voucher generator, and point ledger.
        /// </summary>
        [HttpGet("Loyalty")]
        [HttpGet("Farmer/Loyalty")]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var hub = await _loyalty.GetFarmerLoyaltyHubAsync(userId);
            return View(hub);
        }

        /// <summary>
        /// POST: /Loyalty/ValidatePromo — Live checkout calculation for dynamic discount preview.
        /// Accepts JSON body with PromoCode, PointsToRedeem, and GrossAmount.
        /// </summary>
        [HttpPost("Loyalty/ValidatePromo")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ValidatePromo([FromBody] ApplyPromoRequestViewModel req)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _loyalty.ValidateAndCalculateDiscountAsync(userId, req.PromoCode, req.PointsToRedeem, req.GrossAmount);
            return Json(result);
        }

        /// <summary>
        /// POST: /Loyalty/GenerateVoucher — 1-click conversion from points balance into a reusable promo voucher code.
        /// </summary>
        [HttpPost("Loyalty/GenerateVoucher")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateVoucher([FromForm] int pointsRequired)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var voucher = await _loyalty.GenerateVoucherAsync(userId, pointsRequired);

            if (voucher == null)
            {
                TempData["ErrorMessage"] = "Unable to generate promo code. You may not have enough KrishiPoints for this tier.";
                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] = $"Promo Voucher generated successfully! Code: {voucher.PromoCode} (Save ৳{voucher.DiscountAmount:N0} on your next booking)";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// GET: /Loyalty/Tiers — JSON endpoint returning standard conversion tiers.
        /// </summary>
        [HttpGet("Loyalty/Tiers")]
        public IActionResult GetTiers()
        {
            var tiers = _loyalty.GetConversionTiers();
            return Json(tiers);
        }
    }
}
