using System.Globalization;
using System.Security.Claims;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    /// <summary>
    /// Revenue actions shared by the Godown and Equipment owner controllers. Views resolve from
    /// Views/Shared (Revenue.cshtml, Invoice.cshtml); the derived controller supplies the role-specific service.
    /// </summary>
    public abstract class OwnerRevenueControllerBase : Controller
    {
        private readonly IOwnerRevenueService _revenueService;
        private readonly UserManager<ApplicationUser> _userManager;

        protected OwnerRevenueControllerBase(IOwnerRevenueService revenueService, UserManager<ApplicationUser> userManager)
        {
            _revenueService = revenueService;
            _userManager = userManager;
        }

        protected string OwnerId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        protected decimal ThisMonthRevenue => _revenueService.GetReport(OwnerId, new RevenueFilter()).ThisMonthRevenue;

        protected async Task<string> OwnerDisplayNameAsync()
        {
            var owner = await _userManager.GetUserAsync(User);
            return string.IsNullOrWhiteSpace(owner?.FullName) ? User.Identity?.Name ?? "Owner" : owner.FullName;
        }

        /// <summary>GET: /{Owner}/Revenue — KPIs, settlement, trend, per-listing breakdown, funnel and transactions.</summary>
        [HttpGet]
        public IActionResult Revenue(RevenueFilter filter)
        {
            return View(_revenueService.GetReport(OwnerId, filter));
        }

        /// <summary>GET: /{Owner}/Statement?month=2026-09 — downloads the monthly statement PDF (defaults to the current month).</summary>
        [HttpGet]
        public async Task<IActionResult> Statement(string? month)
        {
            var period = DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : DateTime.Today;
            if (period > DateTime.Today) return BadRequest("Statements are only available for current or past months.");

            var owner = await _userManager.GetUserAsync(User);
            var statement = _revenueService.GenerateMonthlyStatement(OwnerId, period,
                new StatementOwner(owner?.FullName ?? User.Identity?.Name ?? "Owner", owner?.BusinessOrFarmName, owner?.Location));
            return File(statement.Content, "application/pdf", statement.FileName);
        }

        /// <summary>GET: /{Owner}/Invoice/185 — printable receipt for a completed booking (browser "Save as PDF").</summary>
        [HttpGet]
        public async Task<IActionResult> Invoice(int id)
        {
            var model = _revenueService.GetInvoice(OwnerId, id);
            if (model is null) return NotFound();

            var owner = await _userManager.GetUserAsync(User);
            model.OwnerName = owner?.FullName ?? User.Identity?.Name ?? "Owner";
            model.OwnerBusiness = owner?.BusinessOrFarmName;
            model.OwnerLocation = owner?.Location;
            return View(model);
        }

        /// <summary>POST: /{Owner}/AddExpense — records a cost against a booking so the revenue page can show net profit.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddExpense(int bookingId, decimal amount, string? note, string? returnUrl)
        {
            if (_revenueService.AddExpense(OwnerId, bookingId, amount, note))
                TempData["SuccessMessage"] = $"Expense of ৳{amount:N0} recorded against booking #{bookingId}.";
            else
                TempData["ErrorMessage"] = "Expense must be a positive amount on an accepted or completed booking.";

            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Revenue));
        }
    }
}
