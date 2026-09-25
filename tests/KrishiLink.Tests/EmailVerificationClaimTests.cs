using KrishiLink.Models.Entities;
using KrishiLink.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace KrishiLink.Tests;

[Collection(PostgresCollection.Name)]
public class EmailVerificationClaimTests
{
    private readonly PostgresDatabase _database;

    public EmailVerificationClaimTests(PostgresDatabase database) => _database = database;

    [PostgresFact]
    public async Task The_principal_reflects_the_confirmation_state_every_time_it_is_built()
    {
        await using var market = new Marketplace(_database);
        var userId = await market.AddUserAsync(AppRoles.Farmer);

        async Task<string?> ClaimValueAsync(bool confirmed) => await market.InScopeAsync(async sp =>
        {
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(userId))!;
            user.EmailConfirmed = confirmed;
            Assert.True((await users.UpdateAsync(user)).Succeeded);
            var principal = await sp.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>().CreateAsync(user);
            return principal.FindFirst(AppPolicies.EmailVerifiedClaim)?.Value;
        });

        Assert.Equal("false", await ClaimValueAsync(false));
        Assert.Equal("true", await ClaimValueAsync(true));
        Assert.Equal("false", await ClaimValueAsync(false));
    }
}
