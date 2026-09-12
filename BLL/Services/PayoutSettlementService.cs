using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KrishiLink.BLL.Services
{
    public interface IPayoutSettlementService
    {
        Task<int> SettleDuePayoutsAsync(int? maxHours = null, string? ownerId = null, CancellationToken ct = default);
    }

    public class PayoutSettlementService : IPayoutSettlementService
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;
        private readonly RevenueOptions _options;
        private readonly ILogger<PayoutSettlementService> _logger;

        public PayoutSettlementService(
            ApplicationDbContext db,
            INotificationService notifications,
            IOptions<RevenueOptions> options,
            ILogger<PayoutSettlementService> logger)
        {
            _db = db;
            _notifications = notifications;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<int> SettleDuePayoutsAsync(int? maxHours = null, string? ownerId = null, CancellationToken ct = default)
        {
            var settlementHours = maxHours ?? _options.PayoutSettlementHours;
            var cutoff = settlementHours > 0 ? DateTime.UtcNow.AddHours(-settlementHours) : DateTime.UtcNow.AddMinutes(1);

            var query = _db.Transactions
                .Include(t => t.User)
                .Where(t => t.Status == "Processing");

            if (settlementHours > 0)
            {
                query = query.Where(t => t.TransactionDate <= cutoff);
            }

            if (!string.IsNullOrWhiteSpace(ownerId))
            {
                query = query.Where(t => t.UserId == ownerId);
            }

            var pending = await query.ToListAsync(ct);
            if (pending.Count == 0) return 0;

            var settledCount = 0;
            foreach (var t in pending)
            {
                t.Status = "Completed";
                t.SettledOn = DateTime.UtcNow;
                settledCount++;
            }

            await _db.SaveChangesAsync(ct);

            foreach (var t in pending)
            {
                try
                {
                    var masked = MaskAccount(t.PayoutAccount);
                    var linkUrl = AppLinks.OwnerPayouts(t.ListingType);

                    await _notifications.NotifyAsync(new NotificationRequest
                    {
                        UserId = t.UserId,
                        Type = NotificationTypes.PayoutProcessed,
                        TitleKey = "Payout Completed",
                        MessageKey = "Your payout of ৳{0:N0} via {1} ({2}) has been settled successfully.",
                        Args = new object[] { t.Amount, t.PaymentMethod, masked },
                        LinkUrl = linkUrl,
                        DedupeKey = $"payout:{t.Id}:Completed",
                        SendEmail = true,
                        RecipientEmail = t.User?.Email
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send payout completed notification for transaction {TransactionId}", t.Id);
                }
            }

            _logger.LogInformation("Settled {Count} payout transaction(s).", settledCount);
            return settledCount;
        }

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
