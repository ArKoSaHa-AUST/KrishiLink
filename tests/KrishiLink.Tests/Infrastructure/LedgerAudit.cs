using KrishiLink.BLL.Services;
using KrishiLink.DAL;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KrishiLink.Tests.Infrastructure;

/// <summary>
/// The conservation identity only proves the ledger is internally consistent: every row debits one account and
/// credits another, so a row that was never written still "balances". This audit reconciles each ledger total
/// against the domain tables it must mirror, which is what catches a missing or duplicated posting.
/// </summary>
public static class LedgerAudit
{
    public static async Task<IReadOnlyList<string>> FindDiscrepanciesAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var ledger = services.GetRequiredService<ILedgerRepository>();
        var problems = new List<string>();

        void Expect(string what, decimal ledgerValue, decimal domainValue)
        {
            if (ledgerValue != domainValue) problems.Add($"{what}: ledger {ledgerValue:N2} ≠ domain {domainValue:N2}");
        }

        var payments = await db.Payments.AsNoTracking().ToListAsync();
        Expect("PaymentIn", ledger.Sum(LedgerEntryType.PaymentIn),
            payments.Where(p => p.Status is PaymentStatus.Succeeded or PaymentStatus.Refunded).Sum(p => p.Amount));
        Expect("Refund", ledger.Sum(LedgerEntryType.Refund),
            payments.Where(p => p.Status == PaymentStatus.Refunded).Sum(p => p.Amount));

        var payouts = await db.Transactions.AsNoTracking().ToListAsync();
        Expect("PayoutOut", ledger.Sum(LedgerEntryType.PayoutOut) - ledger.Sum(LedgerEntryType.PayoutReversed),
            payouts.Where(t => t.Status == PayoutStatus.Completed).Sum(t => t.Amount));

        var bookings = (await db.EquipmentBookings.AsNoTracking().ToListAsync()).Cast<IPayableBooking>()
            .Concat(await db.GodownBookings.AsNoTracking().ToListAsync())
            .ToList();
        var completed = bookings.Where(b => b.Status == BookingStatus.Completed).ToList();
        Expect("Net commission", ledger.Sum(LedgerEntryType.CommissionEarned) - ledger.Sum(LedgerEntryType.CommissionReversed),
            completed.Sum(BookingPricing.Commission));

        var settledPayoutIds = payouts.Where(t => t.Status == PayoutStatus.Completed).Select(t => t.Id).ToHashSet();
        var heldInEscrow =
            bookings.Where(b => b.Status == BookingStatus.Paid).Sum(b => b.AgreedGross ?? 0)
            + completed.Where(b => b.PayoutId is null || !settledPayoutIds.Contains(b.PayoutId.Value))
                .Sum(b => (b.AgreedGross ?? 0) - BookingPricing.Commission(b));
        Expect("Escrow balance", ledger.Balance(LedgerAccount.PlatformEscrow), heldInEscrow);

        return problems;
    }
}
