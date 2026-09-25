using KrishiLink.DAL;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace KrishiLink.BLL.Services;

/// <summary>Readiness: the application database answers a round trip.</summary>
public sealed class DatabaseHealthCheck(ApplicationDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Database unreachable.");
        }
        catch (Exception ex)
        {
            // The exception type is enough to diagnose; messages can carry connection details.
            return HealthCheckResult.Unhealthy($"Database check failed ({ex.GetType().Name}).");
        }
    }
}

/// <summary>Readiness: Supabase Auth's public health endpoint responds, so sign-in and session validation can work.</summary>
public sealed class SupabaseAuthHealthCheck(IHttpClientFactory httpClients, IOptions<SupabaseAuthOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!Uri.TryCreate(settings.Url, UriKind.Absolute, out var root) || string.IsNullOrWhiteSpace(settings.PublishableKey))
            return HealthCheckResult.Unhealthy("Supabase Auth is not configured.");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{root.AbsoluteUri.TrimEnd('/')}/auth/v1/health");
            request.Headers.Add("apikey", settings.PublishableKey);
            using var client = httpClients.CreateClient(nameof(SupabaseAuthHealthCheck));
            client.Timeout = TimeSpan.FromSeconds(5);
            using var response = await client.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Supabase Auth returned {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy($"Supabase Auth unreachable ({ex.GetType().Name}).");
        }
    }
}
