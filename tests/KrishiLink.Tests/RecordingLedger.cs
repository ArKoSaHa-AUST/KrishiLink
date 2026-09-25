using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;

namespace KrishiLink.Tests;

internal sealed class RecordingLedger : ILedgerRepository
{
    public List<LedgerEntry> Entries { get; } = new();

    public void Add(LedgerEntry entry) => Entries.Add(entry);

    public decimal Balance(string account) =>
        Entries.Where(e => e.CreditAccount == account).Sum(e => e.Amount) - Entries.Where(e => e.DebitAccount == account).Sum(e => e.Amount);

    public decimal Sum(string type, string? userId = null) =>
        Entries.Where(e => e.Type == type && (userId == null || e.UserId == userId)).Sum(e => e.Amount);

    public bool Exists(string type, int? paymentId = null, int? payoutId = null) =>
        Entries.Any(e => e.Type == type && (paymentId == null || e.PaymentId == paymentId) && (payoutId == null || e.PayoutId == payoutId));

    public IReadOnlyList<LedgerEntry> ForBooking(string bookingType, int bookingId) =>
        Entries.Where(e => e.BookingType == bookingType && e.BookingId == bookingId).ToList();

    public IReadOnlyList<LedgerEntry> ForOwner(string ownerId) =>
        Entries.Where(e => e.UserId == ownerId).ToList();
}
