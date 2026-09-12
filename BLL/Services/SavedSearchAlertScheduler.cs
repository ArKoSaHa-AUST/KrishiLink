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
    /// Background scheduler that periodically re-evaluates active saved searches and dispatches
    /// notifications for newly available equipment and storage facilities.
    /// </summary>
    public class SavedSearchAlertScheduler : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IOptions<AlertOptions> _options;
        private readonly IHostEnvironment _env;
        private readonly ILogger<SavedSearchAlertScheduler> _logger;

        public SavedSearchAlertScheduler(
            IServiceScopeFactory scopes,
            IOptions<AlertOptions> options,
            IHostEnvironment env,
            ILogger<SavedSearchAlertScheduler> logger)
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
                ? Math.Min(_options.Value.PollMinutes, 5)
                : Math.Max(1, _options.Value.PollMinutes);

            var interval = TimeSpan.FromMinutes(pollMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<ISavedSearchService>();
                    await service.RunAlertsAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Saved search alert scheduler run encountered an unhandled exception.");
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
