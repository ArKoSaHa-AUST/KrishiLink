using System.Globalization;
using System.Security.Claims;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KrishiLink.Controllers
{
    /// <summary>
    /// Revenue actions shared by the Godown and Equipment owner controllers. Views resolve from
    /// Views/Shared (Revenue.cshtml, Invoice.cshtml); the derived controller supplies the role-specific service.
    /// </summary>
    public abstract class OwnerRevenueControllerBase : Controller
    {
        private readonly IOwnerRevenueService _revenueService;
        protected readonly UserManager<ApplicationUser> _userManager;

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

        /// <summary>GET: /{Owner}/ProfitAndLoss?from=&to=&range= — downloads standalone P&L PDF.</summary>
        [HttpGet]
        public async Task<IActionResult> ProfitAndLoss(RevenueFilter filter)
        {
            var owner = await _userManager.GetUserAsync(User);
            var pnl = _revenueService.GenerateProfitAndLoss(OwnerId, filter,
                new StatementOwner(owner?.FullName ?? User.Identity?.Name ?? "Owner", owner?.BusinessOrFarmName, owner?.Location));
            return File(pnl.Content, "application/pdf", pnl.FileName);
        }

        /// <summary>POST: /{Owner}/SaveExpense — adds (no expenseId) or edits a cost against a booking or as general operating expense.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveExpense(int? expenseId, int? bookingId, int? listingId, decimal amount, string category, string? note, DateTime? expenseDate, string? returnUrl)
        {
            var error = _revenueService.SaveExpense(OwnerId, expenseId, bookingId, listingId, amount, category ?? ExpenseCategories.Other, note, expenseDate);
            if (error is null)
            {
                var target = bookingId.HasValue ? $"booking #{bookingId.Value}" : "general operating expenses";
                TempData["SuccessMessage"] = $"Expense of ৳{amount:N0} ({category ?? ExpenseCategories.Other}) {(expenseId is null ? "recorded to" : "updated on")} {target}.";
            }
            else
            {
                TempData["ErrorMessage"] = error;
            }

            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Revenue));
        }

        /// <summary>POST: /{Owner}/DeleteExpense</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteExpense(int expenseId, string? returnUrl)
        {
            if (_revenueService.DeleteExpense(OwnerId, expenseId))
                TempData["SuccessMessage"] = "Expense removed.";
            else
                TempData["ErrorMessage"] = "That expense no longer exists.";

            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Revenue));
        }

        /// <summary>GET: /{Owner}/Payouts — pending payout balance, commission breakdown and full settlement history.</summary>
        [HttpGet]
        public IActionResult Payouts([FromServices] IOptions<PaymentsOptions> payments)
        {
            var model = _revenueService.GetPayoutHistory(OwnerId);
            model.SettlementDelay = payments.Value.SettlementDelay ?? TimeSpan.FromMinutes(30);
            return View("Payouts", model);
        }

        /// <summary>POST: /{Owner}/RequestPayout — asks the platform to settle the pending balance to the given wallet/account.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestPayout(string method, string? account, [FromServices] IOptions<PaymentsOptions> payments)
        {
            var error = await _revenueService.RequestPayoutAsync(OwnerId, method ?? string.Empty, account);
            if (error is null)
                TempData["SuccessMessage"] = $"Payout requested. It will settle automatically in about {DelayText(payments.Value.SettlementDelay ?? TimeSpan.FromMinutes(30))}.";
            else
                TempData["ErrorMessage"] = error;

            return RedirectToAction(nameof(Payouts));
        }

        public static string DelayText(TimeSpan delay) =>
            delay.TotalHours >= 1 ? $"{delay.TotalHours:0.#} hours" : $"{Math.Max(1, (int)delay.TotalMinutes)} minutes";

        /// <summary>POST: /{Owner}/SettlePayoutsNow — Dev-only action to immediately settle processing payouts.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SettlePayoutsNow(
            [FromServices] IHostEnvironment env,
            [FromServices] IPayoutSettlementService settlementService)
        {
            if (!env.IsDevelopment())
            {
                return NotFound();
            }

            var count = await settlementService.SettleDuePayoutsAsync(ignoreDelay: true, ownerId: OwnerId);
            TempData["SuccessMessage"] = count > 0
                ? $"[Dev] Successfully settled {count} pending payout(s)."
                : "[Dev] No processing payouts found to settle.";

            return RedirectToAction(nameof(Payouts));
        }
    }
}
