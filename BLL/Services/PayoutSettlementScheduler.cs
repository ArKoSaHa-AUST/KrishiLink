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
    /// Background loop that settles "Processing" payouts once their delay has elapsed — the automatic exit
    /// for every payout, so no manual step is ever needed. Poll interval comes from Payments:SettlementPollSeconds.
    /// </summary>
    public class PayoutSettlementScheduler : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly TimeSpan _interval;
        private readonly ILogger<PayoutSettlementScheduler> _logger;

        public PayoutSettlementScheduler(IServiceScopeFactory scopes, IOptions<PaymentsOptions> options, ILogger<PayoutSettlementScheduler> logger)
        {
            _scopes = scopes;
            _interval = TimeSpan.FromSeconds(Math.Max(5, options.Value.SettlementPollSeconds));
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Initial delay after startup so migrations and initialization can finish
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var settlementService = scope.ServiceProvider.GetRequiredService<IPayoutSettlementService>();
                    await settlementService.SettleDuePayoutsAsync(ct: stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Payout settlement background run failed; will retry in next cycle.");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
    }
}
