using System.Globalization;
using System.Security.Claims;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
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
        protected readonly IStringLocalizer<SharedResource> _localizer;

        protected OwnerRevenueControllerBase(IOwnerRevenueService revenueService, UserManager<ApplicationUser> userManager, IStringLocalizer<SharedResource> localizer)
        {
            _revenueService = revenueService;
            _userManager = userManager;
            _localizer = localizer;
        }

        protected string OwnerId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        protected async Task<decimal> ThisMonthRevenueAsync() => (await _revenueService.GetReportAsync(OwnerId, new RevenueFilter())).ThisMonthRevenue;

        protected async Task<string> OwnerDisplayNameAsync()
        {
            var owner = await _userManager.GetUserAsync(User);
            return string.IsNullOrWhiteSpace(owner?.FullName) ? User.Identity?.Name ?? "Owner" : owner.FullName;
        }

        /// <summary>GET: /{Owner}/Revenue — KPIs, settlement, trend, per-listing breakdown, funnel and transactions.</summary>
        [HttpGet]
        public async Task<IActionResult> Revenue(RevenueFilter filter)
        {
            return View(await _revenueService.GetReportAsync(OwnerId, filter));
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
            var statement = await _revenueService.GenerateMonthlyStatementAsync(OwnerId, period,
                new StatementOwner(owner?.FullName ?? User.Identity?.Name ?? "Owner", owner?.BusinessOrFarmName, owner?.Location));
            return File(statement.Content, "application/pdf", statement.FileName);
        }

        /// <summary>GET: /{Owner}/Invoice/185 — printable receipt for a completed booking (browser "Save as PDF").</summary>
        [HttpGet]
        public async Task<IActionResult> Invoice(int id)
        {
            var model = await _revenueService.GetInvoiceAsync(OwnerId, id);
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
            var pnl = await _revenueService.GenerateProfitAndLossAsync(OwnerId, filter,
                new StatementOwner(owner?.FullName ?? User.Identity?.Name ?? "Owner", owner?.BusinessOrFarmName, owner?.Location));
            return File(pnl.Content, "application/pdf", pnl.FileName);
        }

        /// <summary>POST: /{Owner}/SaveExpense — adds (no expenseId) or edits a cost against a booking or as general operating expense.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveExpense(int? expenseId, int? bookingId, int? listingId, decimal amount, string category, string? note, DateTime? expenseDate, string? returnUrl)
        {
            var error = await _revenueService.SaveExpenseAsync(OwnerId, expenseId, bookingId, listingId, amount, category ?? ExpenseCategories.Other, note, expenseDate);
            if (error is null)
            {
                var format = (expenseId is null, bookingId.HasValue) switch
                {
                    (true, true) => "Expense of ৳{0} ({1}) recorded to booking #{2}.",
                    (false, true) => "Expense of ৳{0} ({1}) updated on booking #{2}.",
                    (true, false) => "Expense of ৳{0} ({1}) recorded to general operating expenses.",
                    (false, false) => "Expense of ৳{0} ({1}) updated on general operating expenses."
                };
                TempData["SuccessMessage"] = _localizer[format, $"{amount:N0}", _localizer[category ?? ExpenseCategories.Other].Value, bookingId ?? 0].Value;
            }
            else
            {
                TempData["ErrorMessage"] = _localizer[error].Value;
            }

            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Revenue));
        }

        /// <summary>POST: /{Owner}/DeleteExpense</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteExpense(int expenseId, string? returnUrl)
        {
            if (await _revenueService.DeleteExpenseAsync(OwnerId, expenseId))
                TempData["SuccessMessage"] = _localizer["Expense removed."].Value;
            else
                TempData["ErrorMessage"] = _localizer["That expense no longer exists."].Value;

            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Revenue));
        }

        /// <summary>GET: /{Owner}/Payouts — pending payout balance, commission breakdown and full settlement history.</summary>
        [HttpGet]
        public async Task<IActionResult> Payouts([FromServices] IOptions<PaymentsOptions> payments)
        {
            var model = await _revenueService.GetPayoutHistoryAsync(OwnerId);
            model.SettlementDelay = payments.Value.SettlementDelay ?? TimeSpan.FromMinutes(30);
            return View("Payouts", model);
        }

        /// <summary>POST: /{Owner}/RequestPayout — asks the platform to settle the pending balance to the given wallet/account.</summary>
        [HttpPost]
        [Authorize(Policy = AppPolicies.VerifiedEmail)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestPayout(string method, string? account, [FromServices] IOptions<PaymentsOptions> payments)
        {
            var error = await _revenueService.RequestPayoutAsync(OwnerId, method ?? string.Empty, account);
            if (error is null)
                TempData["SuccessMessage"] = _localizer["Payout requested. It will settle automatically in about {0}.", DelayText(payments.Value.SettlementDelay ?? TimeSpan.FromMinutes(30), _localizer)].Value;
            else
                TempData["ErrorMessage"] = _localizer[error].Value;

            return RedirectToAction(nameof(Payouts));
        }

        public static string DelayText(TimeSpan delay, IStringLocalizer<SharedResource> localizer) =>
            delay.TotalHours >= 1
                ? localizer["{0} hour(s)", $"{delay.TotalHours:0.#}"].Value
                : localizer["{0} minutes", Math.Max(1, (int)delay.TotalMinutes)].Value;

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
            TempData["SuccessMessage"] = _localizer[count > 0
                ? "[Dev] Successfully settled {0} pending payout(s)."
                : "[Dev] No processing payouts found to settle.", count].Value;

            return RedirectToAction(nameof(Payouts));
        }
    }
}
