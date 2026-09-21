using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace KrishiLink.BLL.Services;

/// <summary>Admin-backed provisioning must not bypass public authentication request throttling.</summary>
public sealed class SupabaseAuthRateLimiter : IDisposable
{
    public PartitionedRateLimiter<HttpContext> Limiter { get; } =
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                $"{context.Connection.RemoteIpAddress}:{context.Request.RouteValues["action"]?.ToString()?.ToUpperInvariant()}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

    public void Dispose() => Limiter.Dispose();
}

public sealed class SupabaseAuthRateLimitFilter(SupabaseAuthRateLimiter limiter) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        using var lease = await limiter.Limiter.AcquireAsync(context.HttpContext, 1, context.HttpContext.RequestAborted);
        if (!lease.IsAcquired)
        {
            context.HttpContext.Response.Headers.RetryAfter = "60";
            context.Result = new ObjectResult(new { success = false, message = "Too many attempts. Please wait one minute." })
            {
                StatusCode = StatusCodes.Status429TooManyRequests
            };
            return;
        }
        await next();
    }
}
