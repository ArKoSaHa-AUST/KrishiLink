using KrishiLink.DAL;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KrishiLink.BLL.Services
{
    public interface IPayoutSettlementService
    {
        /// <summary>
        /// Settles every "Processing" payout older than the configured delay (or all of them when <paramref name="ignoreDelay"/>):
        /// Completed + ledger PayoutOut, or Failed with its bookings released back to the owed balance. Returns how many changed.
        /// </summary>
        Task<int> SettleDuePayoutsAsync(bool ignoreDelay = false, string? ownerId = null, CancellationToken ct = default);
    }

    /// <summary>
    /// Simulates the bank/bKash transfer leg of a payout. No human is involved: every Processing payout
    /// automatically ends Completed or, when the destination account ends with the configured suffix, Failed.
    /// </summary>
    public class PayoutSettlementService : IPayoutSettlementService
    {
        public const string RejectedAccountReason = "Destination account rejected (simulated)";

        private readonly ApplicationDbContext _db;
        private readonly ILedgerRepository _ledger;
        private readonly INotificationService _notifications;
        private readonly PaymentsOptions _options;
        private readonly ILogger<PayoutSettlementService> _logger;

        public PayoutSettlementService(
            ApplicationDbContext db,
            ILedgerRepository ledger,
            INotificationService notifications,
            IOptions<PaymentsOptions> options,
            ILogger<PayoutSettlementService> logger)
        {
            _db = db;
            _ledger = ledger;
            _notifications = notifications;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<int> SettleDuePayoutsAsync(bool ignoreDelay = false, string? ownerId = null, CancellationToken ct = default)
        {
            var cutoff = DateTime.Now - (_options.SettlementDelay ?? TimeSpan.FromMinutes(30));
            var query = _db.Transactions.Include(t => t.User).Where(t => t.Status == PayoutStatus.Processing);
            if (!ignoreDelay) query = query.Where(t => t.TransactionDate <= cutoff);
            if (!string.IsNullOrWhiteSpace(ownerId)) query = query.Where(t => t.UserId == ownerId);

            var due = await query.ToListAsync(ct);
            if (due.Count == 0) return 0;

            var now = DateTime.Now;
            foreach (var t in due)
            {
                t.SettledOn = now;
                if (IsRejectedAccount(t.PayoutAccount))
                {
                    t.Status = PayoutStatus.Failed;
                    t.FailureReason = RejectedAccountReason;
                    // The bookings go back to "owed" so the owner can request again with a working account.
                    await _db.EquipmentBookings.Where(b => b.PayoutId == t.Id).ForEachAsync(b => b.PayoutId = null, ct);
                    await _db.GodownBookings.Where(b => b.PayoutId == t.Id).ForEachAsync(b => b.PayoutId = null, ct);
                }
                else
                {
                    t.Status = PayoutStatus.Completed;
                    // Idempotent against a concurrent tick or the dev "settle now" button.
                    if (!_ledger.Exists(LedgerEntryType.PayoutOut, payoutId: t.Id)) _ledger.Add(LedgerPostings.PayoutOut(t));
                }
            }
            await _db.SaveChangesAsync(ct);

            foreach (var t in due)
            {
                try
                {
                    var failed = t.Status == PayoutStatus.Failed;
                    await _notifications.NotifyAsync(new NotificationRequest
                    {
                        UserId = t.UserId,
                        Type = NotificationTypes.PayoutProcessed,
                        TitleKey = failed ? "Payout Failed" : "Payout Completed",
                        MessageKey = failed
                            ? "Your payout of ৳{0} via {1} ({2}) failed: {3}. The bookings are back in your pending balance."
                            : "Your payout of ৳{0:N0} via {1} ({2}) has been settled successfully.",
                        Args = failed
                            ? new object[] { $"{t.Amount:N0}", t.PaymentMethod, MaskAccount(t.PayoutAccount), t.FailureReason! }
                            : new object[] { t.Amount, t.PaymentMethod, MaskAccount(t.PayoutAccount) },
                        LinkUrl = AppLinks.OwnerPayouts(t.ListingType),
                        DedupeKey = $"payout:{t.Id}:{t.Status}",
                        SendEmail = true,
                        RecipientEmail = t.User?.Email
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send payout settlement notification for transaction {TransactionId}", t.Id);
                }
            }

            _logger.LogInformation("Settled {Count} payout(s): {Completed} completed, {Failed} failed.",
                due.Count, due.Count(t => t.Status == PayoutStatus.Completed), due.Count(t => t.Status == PayoutStatus.Failed));
            return due.Count;
        }

        private bool IsRejectedAccount(string? account) =>
            !string.IsNullOrEmpty(_options.FailAccountSuffix)
            && (account ?? string.Empty).Trim().EndsWith(_options.FailAccountSuffix, StringComparison.Ordinal);

        public static string MaskAccount(string? account)
        {
            if (string.IsNullOrWhiteSpace(account)) return "••••";
            var clean = account.Trim();
            if (clean.Length <= 4) return clean;
            if (clean.Length <= 8) return $"{clean[..2]}•••{clean[^2..]}";
            return $"{clean[..3]}••••{clean[^4..]}";
        }
    }
}
