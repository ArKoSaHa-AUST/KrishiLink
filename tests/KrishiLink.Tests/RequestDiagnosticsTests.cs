using System.Collections.Concurrent;
using KrishiLink.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KrishiLink.Tests;

/// <summary>QLT-02: every request carries a correlation id through its logs, and slow requests are reported.</summary>
public class RequestDiagnosticsTests : IAsyncLifetime
{
    private WebApplication? _app;
    private HttpClient _http = null!;
    private readonly CapturingLoggerProvider _logs = new();

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["AllowedHosts"] = "*";
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(_logs);
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        builder.Services.Configure<DiagnosticsOptions>(o => o.SlowRequestMs = 100);

        _app = builder.Build();
        _app.UseRequestDiagnostics();
        _app.MapGet("/ok", (HttpContext context, ILogger<RequestDiagnosticsTests> logger) =>
        {
            logger.LogInformation("inside the request");
            return context.TraceIdentifier;
        });
        _app.MapGet("/slow", async () => { await Task.Delay(250); return "done"; });
        await _app.StartAsync();
        _http = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        if (_app is not null) await _app.DisposeAsync();
    }

    [Fact]
    public async Task A_safe_inbound_id_is_kept_echoed_and_attached_to_every_log_line()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/ok");
        request.Headers.Add(RequestDiagnostics.Header, "farmer-report-2026");
        var response = await _http.SendAsync(request);

        Assert.Equal("farmer-report-2026", response.Headers.GetValues(RequestDiagnostics.Header).Single());
        Assert.Equal("farmer-report-2026", await response.Content.ReadAsStringAsync());
        Assert.Contains(_logs.Entries, e => e.Message == "inside the request" && e.Scopes.Contains("CorrelationId=farmer-report-2026"));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("has spaces in it")]
    [InlineData("<script>alert(1)</script>")]
    public async Task An_unsafe_inbound_id_is_replaced(string inbound)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/ok");
        request.Headers.TryAddWithoutValidation(RequestDiagnostics.Header, inbound);
        var response = await _http.SendAsync(request);
        var id = response.Headers.GetValues(RequestDiagnostics.Header).Single();
        Assert.NotEqual(inbound, id);
        Assert.Matches("^[0-9a-f]{32}$", id);
    }

    [Fact]
    public async Task A_slow_request_is_logged_with_its_duration_and_path()
    {
        await _http.GetAsync("/slow?token=secret");
        var entry = Assert.Single(_logs.Entries, e => e.Level == LogLevel.Warning && e.Message.StartsWith("Slow request", StringComparison.Ordinal));
        Assert.Contains("/slow", entry.Message);
        Assert.DoesNotContain("secret", entry.Message);
    }

    private sealed record Entry(LogLevel Level, string Message, IReadOnlyList<string> Scopes);

    private sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();
        public ConcurrentBag<Entry> EntriesBag { get; } = new();
        public IReadOnlyList<Entry> Entries => EntriesBag.ToList();

        public ILogger CreateLogger(string categoryName) => new Logger(this);
        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;
        public void Dispose() { }

        private sealed class Logger : ILogger
        {
            private readonly CapturingLoggerProvider _owner;
            public Logger(CapturingLoggerProvider owner) => _owner = owner;
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _owner._scopes.Push(state);
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var scopes = new List<string>();
                _owner._scopes.ForEachScope((scope, list) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object>> pairs) list.AddRange(pairs.Select(p => $"{p.Key}={p.Value}"));
                }, scopes);
                _owner.EntriesBag.Add(new Entry(logLevel, formatter(state, exception), scopes));
            }
        }
    }
}
