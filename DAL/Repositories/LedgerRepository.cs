using KrishiLink.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.DAL.Repositories
{
    /// <summary>
    /// Append-only access to the money ledger. <see cref="Add"/> only stages the row — the caller's
    /// SaveChanges commits it together with the status change it records, so the two can never diverge.
    /// </summary>
    public interface ILedgerRepository
    {
        void Add(LedgerEntry entry);

        /// <summary>Credits minus debits for one account.</summary>
        decimal Balance(string account);

        decimal Sum(string type, string? userId = null);
        bool Exists(string type, int? paymentId = null, int? payoutId = null);
        IReadOnlyList<LedgerEntry> ForBooking(string bookingType, int bookingId);
        IReadOnlyList<LedgerEntry> ForOwner(string ownerId);
    }

    public class LedgerRepository : ILedgerRepository
    {
        private readonly ApplicationDbContext _db;

        public LedgerRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public void Add(LedgerEntry entry)
        {
            if (entry.Amount <= 0) throw new InvalidOperationException($"Ledger amounts must be positive ({entry.Type}: {entry.Amount}).");
            _db.LedgerEntries.Add(entry);
        }

        public decimal Balance(string account) =>
            _db.LedgerEntries.Where(l => l.CreditAccount == account).Sum(l => (decimal?)l.Amount).GetValueOrDefault()
            - _db.LedgerEntries.Where(l => l.DebitAccount == account).Sum(l => (decimal?)l.Amount).GetValueOrDefault();

        public decimal Sum(string type, string? userId = null) =>
            _db.LedgerEntries
                .Where(l => l.Type == type && (userId == null || l.UserId == userId))
                .Sum(l => (decimal?)l.Amount).GetValueOrDefault();

        public bool Exists(string type, int? paymentId = null, int? payoutId = null) =>
            _db.LedgerEntries.Any(l => l.Type == type
                && (paymentId == null || l.PaymentId == paymentId)
                && (payoutId == null || l.PayoutId == payoutId));

        public IReadOnlyList<LedgerEntry> ForBooking(string bookingType, int bookingId) =>
            _db.LedgerEntries.AsNoTracking()
                .Where(l => l.BookingType == bookingType && l.BookingId == bookingId)
                .OrderBy(l => l.OccurredOn).ToList();

        public IReadOnlyList<LedgerEntry> ForOwner(string ownerId) =>
            _db.LedgerEntries.AsNoTracking()
                .Where(l => l.UserId == ownerId && l.Type != LedgerEntryType.PaymentIn && l.Type != LedgerEntryType.Refund)
                .OrderBy(l => l.OccurredOn).ToList();
    }
}
