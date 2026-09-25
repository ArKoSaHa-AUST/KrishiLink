using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace KrishiLink.BLL.Services;

/// <summary>Optional first-login provisioning; the configured email is a server-controlled allowlist.</summary>
public sealed class SupabaseAdminBootstrap(
    SupabaseAuthClient auth,
    UserManager<ApplicationUser> users,
    ApplicationDbContext db,
    IOptions<SupabaseAuthOptions> options,
    ILogger<SupabaseAdminBootstrap> logger)
{
    public async Task<ApplicationUser?> ResolveUserAsync(string accessToken)
    {
        var verified = await auth.GetUserAsync(accessToken);
        if (!Guid.TryParse(verified.Id, out _) || string.IsNullOrWhiteSpace(verified.Email))
            throw new SupabaseAuthException("Unable to sign in. Please try again.");

        var user = await users.FindByIdAsync(verified.Id);
        var allowedEmail = options.Value.AdminEmail.Trim();
        if (allowedEmail.Length == 0 ||
            !string.Equals(allowedEmail, verified.Email, StringComparison.OrdinalIgnoreCase))
            return user;
        // Administrator rights are granted by e-mail address, so that address must be proven first.
        if (verified.EmailConfirmedAt == null)
            throw new SupabaseAuthException("A verified email is required before signing in.", errorCode: SupabaseAuthException.EmailNotConfirmed);
        var isAdmin = user != null && await users.IsInRoleAsync(user, AppRoles.Admin);
        if (isAdmin && user!.UserRole == AppRoles.Admin)
            return user;

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            if (user == null)
            {
                user = new ApplicationUser
                {
                    Id = verified.Id,
                    Email = verified.Email,
                    UserName = verified.Email,
                    EmailConfirmed = true,
                    FullName = "Administrator",
                    UserRole = AppRoles.Admin,
                    CreatedAt = DateTime.UtcNow,
                    OnboardingCompletedAt = DateTime.UtcNow
                };
                var created = await users.CreateAsync(user);
                if (!created.Succeeded) throw new InvalidOperationException("Administrator profile creation failed.");
            }
            else
            {
                if (await users.IsLockedOutAsync(user)) throw new SupabaseAuthException("This account is unavailable.");
                user.UserRole = AppRoles.Admin;
                user.OnboardingCompletedAt ??= DateTime.UtcNow;
                var updated = await users.UpdateAsync(user);
                if (!updated.Succeeded) throw new InvalidOperationException("Administrator profile update failed.");
            }
            if (!isAdmin)
            {
                var assigned = await users.AddToRoleAsync(user, AppRoles.Admin);
                if (!assigned.Succeeded) throw new InvalidOperationException("Administrator role assignment failed.");
            }
            await transaction.CommitAsync();
            logger.LogInformation("Configured administrator bootstrap completed for Supabase user {UserId}.", user.Id);
            return user;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Configured administrator bootstrap failed ({ErrorType}).", ex.GetType().Name);
            throw new SupabaseAuthException("Administrator setup could not be completed. Please contact the administrator.");
        }
    }
}
