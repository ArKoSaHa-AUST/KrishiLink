using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;

namespace KrishiLink.Controllers
{
    /// <summary>
    /// The one request-throttling mechanism (ASP.NET Core rate limiting). Controllers opt in with
    /// [EnableRateLimiting(RateLimitPolicies.X)]; anything without a policy is not throttled here.
    /// </summary>
    public static class RateLimitPolicies
    {
        /// <summary>Sign-in, registration, codes and password flows: strict, per client and per action.</summary>
        public const string Auth = "auth";

        /// <summary>State-changing requests (bookings, reviews, plans): moderate, per user.</summary>
        public const string Write = "write";

        /// <summary>Live JSON endpoints behind filters, quotes and widgets: generous, per user or IP.</summary>
        public const string ReadJson = "read-json";

        /// <summary>AI assistant turns: each one spends paid upstream tokens, so strict and per user.</summary>
        public const string Agent = "agent";

        /// <summary>
        /// Signed-in users get their own bucket so neighbours behind one NAT are not punished for each other.
        /// Anonymous callers are keyed by client IP, which is only correct because UseForwardedHeaders runs first.
        /// </summary>
        public static string PartitionKey(HttpContext context) =>
            context.User.Identity?.IsAuthenticated == true && context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId
                ? $"user:{userId}"
                : $"ip:{context.Connection.RemoteIpAddress}";

        public static IServiceCollection AddKrishiLinkRateLimiting(this IServiceCollection services) =>
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.AddPolicy(Auth, context => RateLimitPartition.GetFixedWindowLimiter(
                    $"{PartitionKey(context)}:{context.Request.RouteValues["action"]?.ToString()?.ToUpperInvariant()}",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

                options.AddPolicy(Write, context => RateLimitPartition.GetTokenBucketLimiter(
                    PartitionKey(context),
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 30,
                        TokensPerPeriod = 30,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

                options.AddPolicy(ReadJson, context => RateLimitPartition.GetSlidingWindowLimiter(
                    PartitionKey(context),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6,
                        QueueLimit = 0
                    }));

                options.AddPolicy(Agent, context => RateLimitPartition.GetFixedWindowLimiter(
                    PartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 8, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

                options.OnRejected = async (rejected, ct) =>
                {
                    var http = rejected.HttpContext;
                    var retryAfter = rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait) ? wait : TimeSpan.FromMinutes(1);
                    http.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);

                    var localizer = http.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
                    var message = localizer["Too many attempts. Please wait a minute and try again."].Value;
                    if (ConcurrencyConflictFilter.WantsJson(http.Request))
                    {
                        // "success"/"message" keep the existing fetch callers working alongside the standard fields.
                        var problem = new ProblemDetails { Status = StatusCodes.Status429TooManyRequests, Title = message };
                        problem.Extensions["success"] = false;
                        problem.Extensions["message"] = message;
                        await http.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json", ct);
                    }
                    else
                    {
                        http.Response.ContentType = "text/plain; charset=utf-8";
                        await http.Response.WriteAsync(message, ct);
                    }
                };
            });
    }
}
