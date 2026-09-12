using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KrishiLink.BLL.Services
{
    /// <summary>
    /// Background scheduler that periodically evaluates weather forecasts against registered farmers' crops
    /// and dispatches proactive notifications for upcoming weather risks (e.g. rain during harvest).
    /// </summary>
    public class WeatherSuggestionScheduler : BackgroundService
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(12);
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<WeatherSuggestionScheduler> _logger;

        public WeatherSuggestionScheduler(
            IServiceScopeFactory scopes,
            ILogger<WeatherSuggestionScheduler> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Initial delay to allow DB migrations and seed data initialization to complete
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var suggestionService = scope.ServiceProvider.GetRequiredService<IWeatherSuggestionService>();
                    await suggestionService.ProcessDailyFarmerSuggestionsAsync();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Weather suggestion scheduler run encountered an error; will retry next cycle.");
                }

                await Task.Delay(CheckInterval, stoppingToken);
            }
        }
    }
}
