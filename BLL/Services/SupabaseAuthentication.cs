using System.Data;
using System.Security.Claims;
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

    /// <param name="databaseSessions">
    /// True stores sessions in PostgreSQL (restarts and multiple instances keep everyone signed in);
    /// false keeps them in process memory, which suits Development only.
    /// </param>
    public static IServiceCollection AddSupabaseAuthentication(this IServiceCollection services, IConfiguration configuration, bool databaseSessions)
    {
        services.Configure<SupabaseAuthOptions>(configuration.GetSection(SupabaseAuthOptions.SectionName));
        services.AddHttpClient<SupabaseAuthClient>(client => client.Timeout = TimeSpan.FromSeconds(20))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        if (databaseSessions)
            services.AddScoped<ISupabaseSessionStore, DatabaseSupabaseSessionStore>();
        else
            services.AddSingleton<ISupabaseSessionStore, InMemorySupabaseSessionStore>();
        services.AddScoped<SupabaseSessionService>();
        services.AddScoped<SupabaseAdminBootstrap>();
        services.AddMemoryCache();
        services.AddSingleton<SupabaseEmailLinkStore>();
        services.AddScoped<SupabaseCookieEvents>();
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

/// <summary>Opaque-cookie sessions backed by <see cref="ISupabaseSessionStore"/>.</summary>
public sealed class SupabaseSessionService(
    SupabaseAuthClient auth,
    ISupabaseSessionStore store,
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    ApplicationDbContext db)
{
    public Task<SupabaseSession?> CurrentAsync(ClaimsPrincipal principal) =>
        store.GetAsync(principal.FindFirstValue(SupabaseAuthentication.SessionClaim));

    public async Task SignInAsync(HttpContext context, ApplicationUser user, SupabaseAuthTokens tokens, bool persistent)
    {
        // An unconfirmed address may sign in and browse; the VerifiedEmail policy gates the actions where it matters.
        var remote = await auth.GetUserAsync(tokens.AccessToken);
        if (remote.Id != user.Id || string.IsNullOrWhiteSpace(tokens.RefreshToken))
            throw new SupabaseAuthException("Unable to sign in. Please try again.");
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
        await store.RemoveAsync(context.User.FindFirstValue(SupabaseAuthentication.SessionClaim));
        var id = await store.AddAsync(session);
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
        if (id == null) return null;
        var stored = await store.GetAsync(id);
        if (stored == null) return null;
        var user = await users.FindByIdAsync(stored.UserId);
        if (user == null || await users.IsLockedOutAsync(user) ||
            await users.GetSecurityStampAsync(user) != stored.SecurityStamp) return null;

        // Refresh tokens are single-use: the store serializes refreshes across requests and instances.
        var session = await store.RefreshIfDueAsync(id,
            s => s.TokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1),
            s => auth.RefreshAsync(s.Tokens.RefreshToken));
        if (session == null) return null;

        var remote = await auth.GetUserAsync(session.Tokens.AccessToken);
        if (remote.Id != user.Id) return null;
        // getUser validates JWTs but JWTs can outlive sign-out. Check the server's session row too.
        if (!await IsRemoteSessionActiveAsync(session.Tokens.AccessToken, user.Id)) return null;
        await SynchronizeEmailAsync(user, remote);
        return user;
    }

    public async Task<bool> SignOutAsync(HttpContext context, bool allSessions = false)
    {
        var id = context.User.FindFirstValue(SupabaseAuthentication.SessionClaim);
        var session = await store.GetAsync(id);
        await store.RemoveAsync(id);
        await context.SignOutAsync(IdentityConstants.ApplicationScheme);
        if (session == null) return true;
        if (allSessions) await store.RemoveUserAsync(session.UserId);
        try
        {
            // Revokes the Supabase session itself, so a refresh racing this sign-out cannot keep it alive.
            await auth.LogoutAsync(session.Tokens.AccessToken, allSessions);
            return true;
        }
        catch (SupabaseAuthException) { return false; }
    }

    public Task RevokeLocalSessionsAsync(string userId) => store.RemoveUserAsync(userId);

    private async Task SynchronizeEmailAsync(ApplicationUser user, SupabaseAuthUser remote)
    {
        if (string.IsNullOrWhiteSpace(remote.Email)) throw new SupabaseAuthException("An email address is required.");
        // EmailConfirmed only ever mirrors Supabase's email_confirmed_at; it is never asserted locally.
        var confirmed = remote.EmailConfirmedAt != null;
        if (string.Equals(user.Email, remote.Email, StringComparison.OrdinalIgnoreCase) && user.EmailConfirmed == confirmed) return;
        user.Email = remote.Email;
        user.UserName = remote.Email;
        user.EmailConfirmed = confirmed;
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
    ISupabaseSessionStore store,
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
        try { await store.RemoveAsync(id); }
        catch (Exception ex)
        {
            // The cookie is rejected either way; an unreachable store must not turn this into a 500.
            logger.LogWarning("Supabase session could not be removed ({ErrorType}).", ex.GetType().Name);
        }
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
    }
}
