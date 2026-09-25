using System.Security.Claims;
using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace KrishiLink.BLL.Services;

/// <summary>
/// Adds the email_verified claim behind the <see cref="AppPolicies.VerifiedEmail"/> policy. The cookie principal is
/// rebuilt on every request after <c>EmailConfirmed</c> has been re-synchronized from Supabase's email_confirmed_at,
/// so confirming the address takes effect on the next request without signing in again.
/// </summary>
public sealed class KrishiLinkClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(AppPolicies.EmailVerifiedClaim, user.EmailConfirmed ? "true" : "false"));
        return identity;
    }
}
