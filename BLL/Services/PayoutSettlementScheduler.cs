using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KrishiLink.BLL.Services
{
    /// <summary>
    /// Background service that checks for processing payout transactions exceeding the settlement window
    /// and completes them, sending email and in-app notifications.
    /// </summary>
    public class PayoutSettlementScheduler : BackgroundService
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<PayoutSettlementScheduler> _logger;

        public PayoutSettlementScheduler(IServiceScopeFactory scopes, ILogger<PayoutSettlementScheduler> logger)
        {
            _scopes = scopes;
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

                await Task.Delay(CheckInterval, stoppingToken);
            }
        }
    }
}
