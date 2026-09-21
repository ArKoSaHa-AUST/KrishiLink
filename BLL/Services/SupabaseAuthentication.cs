using System.Collections.Concurrent;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.BLL.Services;

public static class SupabaseAuthentication
{
    public const string SessionClaim = "krishilink:supabase-session";

    public static IServiceCollection AddSupabaseAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SupabaseAuthOptions>(configuration.GetSection(SupabaseAuthOptions.SectionName));
        services.AddHttpClient<SupabaseAuthClient>(client => client.Timeout = TimeSpan.FromSeconds(20))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddSingleton<SupabaseSessionStore>();
        services.AddScoped<SupabaseSessionService>();
        services.AddScoped<SupabaseAdminBootstrap>();
        services.AddMemoryCache();
        services.AddSingleton<SupabaseEmailLinkStore>();
        services.AddScoped<SupabaseCookieEvents>();
        services.AddSingleton<SupabaseAuthRateLimiter>();
        services.AddScoped<SupabaseAuthRateLimitFilter>();
        services.ConfigureApplicationCookie(options =>
        {
            options.EventsType = typeof(SupabaseCookieEvents);
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.SlidingExpiration = false;
        });
        return services;
    }
}

public sealed class SupabaseSession
{
    public required string UserId { get; init; }
    public required string SecurityStamp { get; init; }
    public required SupabaseAuthTokens Tokens { get; set; }
    public required DateTimeOffset TokenExpiresAt { get; set; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public SemaphoreSlim Gate { get; } = new(1, 1);
}

/// <summary>Opaque-cookie sessions. Restarting the process signs everyone out; deploy as one instance.</summary>
public sealed class SupabaseSessionStore : IDisposable
{
    private readonly ConcurrentDictionary<string, SupabaseSession> _sessions = new();
    private readonly Timer _cleanup;

    public SupabaseSessionStore() => _cleanup = new Timer(state =>
    {
        foreach (var item in _sessions)
            if (item.Value.ExpiresAt <= DateTimeOffset.UtcNow) _sessions.TryRemove(item.Key, out _);
    }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

    public string Add(SupabaseSession session)
    {
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _sessions[id] = session;
        return id;
    }

    public SupabaseSession? Get(string? id) =>
        id != null && _sessions.TryGetValue(id, out var session) && session.ExpiresAt > DateTimeOffset.UtcNow
            ? session : null;

    public void Remove(string? id)
    {
        if (id != null) _sessions.TryRemove(id, out _);
    }

    public void RemoveUser(string userId)
    {
        foreach (var item in _sessions)
            if (item.Value.UserId == userId) _sessions.TryRemove(item.Key, out _);
    }

    public void Dispose() => _cleanup.Dispose();
}

public sealed class SupabaseSessionService(
    SupabaseAuthClient auth,
    SupabaseSessionStore store,
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    ApplicationDbContext db)
{
    public SupabaseSession? Current(ClaimsPrincipal principal) =>
        store.Get(principal.FindFirstValue(SupabaseAuthentication.SessionClaim));

    public async Task SignInAsync(HttpContext context, ApplicationUser user, SupabaseAuthTokens tokens, bool persistent)
    {
        var remote = await auth.GetUserAsync(tokens.AccessToken);
        if (remote.Id != user.Id || remote.EmailConfirmedAt == null || string.IsNullOrWhiteSpace(tokens.RefreshToken))
            throw new SupabaseAuthException("Verify your email before signing in.");
        await SynchronizeEmailAsync(user, remote);
        if (await users.IsLockedOutAsync(user))
            throw new SupabaseAuthException("This account is unavailable.");
        var now = DateTimeOffset.UtcNow;
        var expires = now.Add(persistent ? TimeSpan.FromDays(14) : TimeSpan.FromHours(8));
        var session = new SupabaseSession
        {
            UserId = user.Id,
            SecurityStamp = await users.GetSecurityStampAsync(user),
            Tokens = tokens,
            TokenExpiresAt = now.AddSeconds(tokens.ExpiresIn),
            ExpiresAt = expires
        };
        store.Remove(context.User.FindFirstValue(SupabaseAuthentication.SessionClaim));
        var id = store.Add(session);
        var principal = await signIn.CreateUserPrincipalAsync(user);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(SupabaseAuthentication.SessionClaim, id));
        await context.SignInAsync(IdentityConstants.ApplicationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = persistent,
            ExpiresUtc = expires,
            AllowRefresh = false
        });
    }

    public async Task<ApplicationUser?> ValidateAsync(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(SupabaseAuthentication.SessionClaim);
        var session = store.Get(id);
        if (session == null) return null;
        await session.Gate.WaitAsync();
        try
        {
            if (store.Get(id) != session) return null;
            var user = await users.FindByIdAsync(session.UserId);
            if (user == null || await users.IsLockedOutAsync(user) ||
                await users.GetSecurityStampAsync(user) != session.SecurityStamp) return null;
            if (session.TokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
            {
                session.Tokens = await auth.RefreshAsync(session.Tokens.RefreshToken);
                session.TokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(session.Tokens.ExpiresIn);
            }
            var remote = await auth.GetUserAsync(session.Tokens.AccessToken);
            if (remote.Id != user.Id || remote.EmailConfirmedAt == null) return null;
            // getUser validates JWTs but JWTs can outlive sign-out. Check the server's session row too.
            if (!await IsRemoteSessionActiveAsync(session.Tokens.AccessToken, user.Id)) return null;
            await SynchronizeEmailAsync(user, remote);
            return user;
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public async Task<bool> SignOutAsync(HttpContext context, bool allSessions = false)
    {
        var id = context.User.FindFirstValue(SupabaseAuthentication.SessionClaim);
        var session = store.Get(id);
        store.Remove(id);
        await context.SignOutAsync(IdentityConstants.ApplicationScheme);
        if (session == null) return true;
        if (allSessions) store.RemoveUser(session.UserId);
        await session.Gate.WaitAsync();
        try
        {
            await auth.LogoutAsync(session.Tokens.AccessToken, allSessions);
            return true;
        }
        catch (SupabaseAuthException) { return false; }
        finally { session.Gate.Release(); }
    }

    public void RevokeLocalSessions(string userId) => store.RemoveUser(userId);

    private async Task SynchronizeEmailAsync(ApplicationUser user, SupabaseAuthUser remote)
    {
        if (string.IsNullOrWhiteSpace(remote.Email)) throw new SupabaseAuthException("A verified email is required.");
        if (string.Equals(user.Email, remote.Email, StringComparison.OrdinalIgnoreCase) && user.EmailConfirmed) return;
        user.Email = remote.Email;
        user.UserName = remote.Email;
        user.EmailConfirmed = remote.EmailConfirmedAt != null;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded) throw new SupabaseAuthException("Your account profile could not be synchronized. Contact support.");
    }

    private async Task<bool> IsRemoteSessionActiveAsync(string accessToken, string userId)
    {
        // Decode only after Auth's /user endpoint has validated this exact access token.
        var parts = accessToken.Split('.');
        if (parts.Length != 3) return false;
        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
        using var json = JsonDocument.Parse(Convert.FromBase64String(payload));
        if (!json.RootElement.TryGetProperty("session_id", out var value) ||
            !Guid.TryParse(value.GetString(), out var sessionId) || !Guid.TryParse(userId, out var uid)) return false;
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT EXISTS (SELECT 1 FROM auth.sessions WHERE id = @session AND user_id = @user AND (not_after IS NULL OR not_after > now()))";
            var sessionParameter = command.CreateParameter();
            sessionParameter.ParameterName = "session";
            sessionParameter.Value = sessionId;
            command.Parameters.Add(sessionParameter);
            var userParameter = command.CreateParameter();
            userParameter.ParameterName = "user";
            userParameter.Value = uid;
            command.Parameters.Add(userParameter);
            return await command.ExecuteScalarAsync() is true;
        }
        finally
        {
            if (opened) await connection.CloseAsync();
        }
    }
}

public sealed class SupabaseCookieEvents(
    SupabaseSessionService sessions,
    SupabaseSessionStore store,
    SignInManager<ApplicationUser> signIn,
    ILogger<SupabaseCookieEvents> logger) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var id = context.Principal?.FindFirstValue(SupabaseAuthentication.SessionClaim);
        try
        {
            var user = context.Principal == null ? null : await sessions.ValidateAsync(context.Principal);
            if (user != null && id != null)
            {
                // Roles always come from the application database, never Supabase's user metadata.
                var principal = await signIn.CreateUserPrincipalAsync(user);
                ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(SupabaseAuthentication.SessionClaim, id));
                context.ReplacePrincipal(principal);
                return;
            }
        }
        catch (Exception ex)
        {
            // Fail closed on Auth/database outages without logging credentials, tokens or upstream payloads.
            logger.LogWarning("Supabase session validation failed ({ErrorType}).", ex.GetType().Name);
        }
        store.Remove(id);
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
    }
}
