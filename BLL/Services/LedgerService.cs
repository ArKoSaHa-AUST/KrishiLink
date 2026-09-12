using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;

namespace KrishiLink.BLL.Services
{
    public record ConservationCheck(bool Ok, decimal Lhs, decimal Rhs, string Detail);

    public interface ILedgerService
    {
        /// <summary>
        /// Verifies the escrow conservation invariant:
        /// Σ PaymentIn − Σ Refund = EscrowBalance + Σ CommissionEarned − Σ CommissionReversed + Σ PayoutOut.
        /// </summary>
        ConservationCheck CheckConservation();
    }

    public class LedgerService : ILedgerService
    {
        private readonly ILedgerRepository _ledger;

        public LedgerService(ILedgerRepository ledger)
        {
            _ledger = ledger;
        }

        public ConservationCheck CheckConservation()
        {
            var paymentsIn = _ledger.Sum(LedgerEntryType.PaymentIn);
            var refunds = _ledger.Sum(LedgerEntryType.Refund);
            var escrow = _ledger.Balance(LedgerAccount.PlatformEscrow);
            var commission = _ledger.Sum(LedgerEntryType.CommissionEarned) - _ledger.Sum(LedgerEntryType.CommissionReversed);
            var paidOut = _ledger.Sum(LedgerEntryType.PayoutOut) - _ledger.Sum(LedgerEntryType.PayoutReversed);

            var lhs = paymentsIn - refunds;
            var rhs = escrow + commission + paidOut;
            var detail = $"paymentsIn={paymentsIn:N0} refunds={refunds:N0} | escrow={escrow:N0} commission={commission:N0} paidOut={paidOut:N0}";
            return new ConservationCheck(lhs == rhs && escrow >= 0, lhs, rhs, detail);
        }
    }

    /// <summary>Factories for the only ledger movements the platform makes — one row per event, amounts always positive.</summary>
    public static class LedgerPostings
    {
        public static LedgerEntry PaymentIn(Payment p) => new()
        {
            OccurredOn = p.PaidOn ?? DateTime.UtcNow,
            DebitAccount = LedgerAccount.FarmerExternal,
            CreditAccount = LedgerAccount.PlatformEscrow,
            Amount = p.Amount,
            Type = LedgerEntryType.PaymentIn,
            BookingType = p.BookingType,
            BookingId = p.BookingId,
            PaymentId = p.Id,
            UserId = p.FarmerId,
            Note = $"{p.Method} {p.Reference}"
        };

        public static LedgerEntry Refund(Payment p) => new()
        {
            OccurredOn = p.RefundedOn ?? DateTime.UtcNow,
            DebitAccount = LedgerAccount.PlatformEscrow,
            CreditAccount = LedgerAccount.FarmerExternal,
            Amount = p.Amount,
            Type = LedgerEntryType.Refund,
            BookingType = p.BookingType,
            BookingId = p.BookingId,
            PaymentId = p.Id,
            UserId = p.FarmerId,
            Note = $"Refund of {p.Reference}"
        };

        public static LedgerEntry CommissionEarned(string bookingType, IPayableBooking b, string ownerId) => new()
        {
            OccurredOn = b.CompletedOn ?? DateTime.UtcNow,
            DebitAccount = LedgerAccount.PlatformEscrow,
            CreditAccount = LedgerAccount.PlatformCommission,
            Amount = BookingPricing.Commission(b),
            Type = LedgerEntryType.CommissionEarned,
            BookingType = bookingType,
            BookingId = b.Id,
            PaymentId = b.PaymentId,
            UserId = ownerId,
            Note = $"{(b.CommissionRate ?? 0) * 100:0.#}% of {b.AgreedGross:N0}"
        };

        public static LedgerEntry CommissionReversed(string bookingType, IPayableBooking b, string ownerId) => new()
        {
            DebitAccount = LedgerAccount.PlatformCommission,
            CreditAccount = LedgerAccount.PlatformEscrow,
            Amount = BookingPricing.Commission(b),
            Type = LedgerEntryType.CommissionReversed,
            BookingType = bookingType,
            BookingId = b.Id,
            PaymentId = b.PaymentId,
            UserId = ownerId,
            Note = "Completion undone by owner"
        };

        public static LedgerEntry PayoutOut(Transaction payout) => new()
        {
            OccurredOn = payout.SettledOn ?? DateTime.UtcNow,
            DebitAccount = LedgerAccount.PlatformEscrow,
            CreditAccount = LedgerAccount.OwnerExternal,
            Amount = payout.Amount,
            Type = LedgerEntryType.PayoutOut,
            BookingType = payout.ListingType ?? string.Empty,
            PayoutId = payout.Id,
            UserId = payout.UserId,
            Note = $"{payout.PaymentMethod} {payout.Reference}"
        };
    }
}
