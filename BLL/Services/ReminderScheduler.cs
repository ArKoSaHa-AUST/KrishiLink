using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KrishiLink.BLL.Services
{
    /// <summary>
    /// Background scheduler that periodically dispatches time-based reminders for upcoming, ending,
    /// unpaid, overdue completion, and stale pending bookings.
    /// Idempotent via unique (UserId, DedupeKey) constraint in the database.
    /// </summary>
    public class ReminderScheduler : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IOptions<ReminderOptions> _options;
        private readonly IHostEnvironment _env;
        private readonly ILogger<ReminderScheduler> _logger;

        public ReminderScheduler(
            IServiceScopeFactory scopes,
            IOptions<ReminderOptions> options,
            IHostEnvironment env,
            ILogger<ReminderScheduler> logger)
        {
            _scopes = scopes;
            _options = options;
            _env = env;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Initial delay (25s) after startup so migrations and initialization can finish
            await Task.Delay(TimeSpan.FromSeconds(25), stoppingToken);

            var pollMinutes = _env.IsDevelopment()
                ? Math.Min(_options.Value.PollMinutes, 2)
                : Math.Max(1, _options.Value.PollMinutes);

            var interval = TimeSpan.FromMinutes(pollMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var reminderService = scope.ServiceProvider.GetRequiredService<IReminderService>();
                    await reminderService.SendDueRemindersAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Reminder scheduler background run failed; will retry in next cycle.");
                }

                try
                {
                    using var scope = _scopes.CreateScope();
                    var swept = await scope.ServiceProvider.GetRequiredService<ISupabaseSessionStore>().SweepExpiredAsync(stoppingToken);
                    if (swept > 0) _logger.LogInformation("Removed {Count} expired sign-in session(s).", swept);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Expired session sweep failed; will retry in next cycle.");
                }

                try
                {
                    // Assistant conversations: archived after 90 idle days, deleted after 180 (documented on the privacy page).
                    using var scope = _scopes.CreateScope();
                    var (archived, deleted) = await scope.ServiceProvider.GetRequiredService<Ai.IAgentService>().SweepRetentionAsync(stoppingToken);
                    if (archived + deleted > 0)
                        _logger.LogInformation("Assistant retention: archived {Archived}, deleted {Deleted} conversation(s).", archived, deleted);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Assistant retention sweep failed; will retry in next cycle.");
                }

                try
                {
                    // E-mail delivery records (QLT-03) are kept for support questions, then removed.
                    using var scope = _scopes.CreateScope();
                    var removed = await scope.ServiceProvider.GetRequiredService<IEmailDeliveryRecorder>().SweepAsync(stoppingToken);
                    if (removed > 0) _logger.LogInformation("Removed {Count} e-mail delivery record(s) past retention.", removed);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "E-mail delivery log sweep failed; will retry in next cycle.");
                }

                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
