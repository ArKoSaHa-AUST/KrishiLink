using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace KrishiLink.Controllers
{
    /// <summary>Thresholds for slow-request and slow-path warnings (appsettings "Diagnostics").</summary>
    public sealed class DiagnosticsOptions
    {
        public const string SectionName = "Diagnostics";

        /// <summary>A whole request slower than this is logged as a warning.</summary>
        public int SlowRequestMs { get; set; } = 2000;

        /// <summary>A named hot path (search, advisory evaluation, booking workflow) slower than this is logged.</summary>
        public int SlowPathMs { get; set; } = 750;
    }

    /// <summary>
    /// Correlation ids and timings (QLT-02). Every request gets an id — the caller's X-Correlation-Id when it is a safe
    /// token, otherwise a new one — that is echoed in the response, used as the trace id in error pages and problem+json,
    /// and attached to every log line written while the request runs, so one user's "it failed at 3pm" can be followed
    /// through the logs. Requests slower than the threshold are logged with their duration.
    /// </summary>
    public static partial class RequestDiagnostics
    {
        public const string Header = "X-Correlation-Id";

        [GeneratedRegex(@"\A[A-Za-z0-9\-]{8,64}\z", RegexOptions.CultureInvariant)]
        private static partial Regex SafeId();

        public static string CorrelationIdFor(string? inbound) =>
            inbound is not null && SafeId().IsMatch(inbound) ? inbound : Guid.NewGuid().ToString("N");

        public static IApplicationBuilder UseRequestDiagnostics(this IApplicationBuilder app) =>
            app.Use(async (context, next) =>
            {
                var id = CorrelationIdFor(context.Request.Headers[Header].FirstOrDefault());
                context.TraceIdentifier = id;
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers[Header] = id;
                    return Task.CompletedTask;
                });

                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RequestDiagnostics));
                var threshold = context.RequestServices.GetRequiredService<IOptions<DiagnosticsOptions>>().Value.SlowRequestMs;
                using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id });
                var clock = Stopwatch.StartNew();
                try
                {
                    await next();
                }
                finally
                {
                    clock.Stop();
                    // The path only: query strings can carry tokens (QR verification links).
                    if (clock.ElapsedMilliseconds >= threshold)
                        logger.LogWarning("Slow request {Method} {Path} took {ElapsedMs} ms (status {Status})",
                            context.Request.Method, context.Request.Path.Value, clock.ElapsedMilliseconds, context.Response.StatusCode);
                    else
                        logger.LogDebug("{Method} {Path} took {ElapsedMs} ms", context.Request.Method, context.Request.Path.Value, clock.ElapsedMilliseconds);
                }
            });
    }

    /// <summary>
    /// Times a hot path — "search", "advisory" or "booking" — around the action (its service calls), and logs it when it
    /// exceeds <see cref="DiagnosticsOptions.SlowPathMs"/>. The numbers show whether caching (ADV-09) and the search
    /// indexes (DIS-01) are doing their job.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class SlowPathAttribute : Attribute, IAsyncActionFilter
    {
        public SlowPathAttribute(string path) => Path = path;

        public string Path { get; }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var clock = Stopwatch.StartNew();
            await next();
            clock.Stop();

            var services = context.HttpContext.RequestServices;
            if (clock.ElapsedMilliseconds < services.GetRequiredService<IOptions<DiagnosticsOptions>>().Value.SlowPathMs) return;
            services.GetRequiredService<ILoggerFactory>().CreateLogger<SlowPathAttribute>()
                .LogWarning("Slow {SlowPath} path: {Action} took {ElapsedMs} ms", Path, context.ActionDescriptor.DisplayName, clock.ElapsedMilliseconds);
        }
    }
}
