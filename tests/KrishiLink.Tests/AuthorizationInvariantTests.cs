using System.Diagnostics;
using KrishiLink.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace KrishiLink.Tests;

/// <summary>
/// Every action must require an authenticated user unless it appears in <see cref="PublicActions"/>.
/// A new action that forgets its attribute is protected by default; a new [AllowAnonymous] fails here until reviewed.
/// </summary>
public class AuthorizationInvariantTests
{
    /// <summary>The reviewed public surface. Adding to this list is a security review decision.</summary>
    private static readonly HashSet<string> PublicActions = new(StringComparer.Ordinal)
    {
        "Home.Index *", "Home.Privacy *", "Home.Error *", "Home.SetLanguage POST", "Home.Offline GET",

        "Account.Register GET", "Account.Register POST", "Account.Login GET", "Account.Login POST",
        "Account.AccessDenied GET", "Account.VerifyEmail GET", "Account.VerifyEmail POST",
        "Account.ResendConfirmation POST", "Account.ForgotPassword GET", "Account.ForgotPassword POST",
        "Account.ResetPassword GET", "Account.ResetPassword POST", "Account.AuthCallback GET",
        "Account.EmailLink GET", "Account.EmailLink POST",

        "Equipment.Index *", "Equipment.Details *", "Equipment.FilterData GET", "Equipment.Quote GET", "Equipment.FreeUnits GET",
        "Godown.Index *", "Godown.Details *", "Godown.FilterData GET",

        "Advisory.Index *", "Advisory.Index POST", "Advisory.Calendar *", "Advisory.Alerts *", "Advisory.Suggestions *",
        "Advisory.CropDetail GET", "Advisory.WeatherSuggestionsJson GET", "Advisory.WeatherAlertsJson GET",
        "Advisory.Planner *", "Advisory.PlannerIcs GET", "Advisory.CalendarJson GET",

        "Leaderboard.Index GET", "Leaderboard.OwnerBadges GET",
        "Community.Index GET", "Community.Post GET", "Community.GetCommentsModal GET", "Community.GetFeedPartial GET",
        "Reviews.List GET",
        "Verify.Index GET", "Verify.Receipt GET",
        "Realtime.GetRealtimeStream GET", "Realtime.IngestWebhook POST", "Realtime.GetStatus GET",
    };

    private static readonly Lazy<IReadOnlyList<ControllerActionDescriptor>> Actions = new(() =>
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new DiagnosticListener("KrishiLink.Tests"));
        services.AddSingleton<DiagnosticSource>(sp => sp.GetRequiredService<DiagnosticListener>());
        services.AddSingleton<IWebHostEnvironment>(new TestEnvironment());
        services.AddSingleton<IHostEnvironment>(sp => sp.GetRequiredService<IWebHostEnvironment>());
        services.AddControllersWithViews(MvcSecurity.Configure)
            .AddApplicationPart(typeof(HomeController).Assembly);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>().ToList();
    });

    private static string Key(ControllerActionDescriptor action)
    {
        var methods = action.ActionConstraints?.OfType<HttpMethodActionConstraint>().SelectMany(c => c.HttpMethods)
            .Distinct().OrderBy(m => m, StringComparer.Ordinal).ToList();
        return $"{action.ControllerName}.{action.ActionName} {(methods is { Count: > 0 } ? string.Join(",", methods) : "*")}";
    }

    private static bool IsAnonymous(ControllerActionDescriptor action) => action.EndpointMetadata.OfType<IAllowAnonymous>().Any();

    [Fact]
    public void Discovery_finds_the_whole_controller_surface()
    {
        Assert.True(Actions.Value.Count >= 148, $"Only {Actions.Value.Count} actions were discovered.");
        Assert.Contains(Actions.Value, a => a.ControllerName == "Verifications");
    }

    [Fact]
    public void Every_action_carries_the_global_authenticated_user_filter()
    {
        var unguarded = Actions.Value
            .Where(a => !a.FilterDescriptors.Any(f => f.Filter is AuthorizeFilter && f.Scope == Microsoft.AspNetCore.Mvc.Filters.FilterScope.Global))
            .Select(Key)
            .ToList();
        Assert.Empty(unguarded);
    }

    [Fact]
    public void Only_reviewed_actions_allow_anonymous_access()
    {
        var unexpected = Actions.Value.Where(IsAnonymous).Select(Key).Where(k => !PublicActions.Contains(k)).ToList();
        Assert.True(unexpected.Count == 0, "Unreviewed [AllowAnonymous] actions: " + string.Join(", ", unexpected));
    }

    [Fact]
    public void Every_reviewed_public_action_exists_and_is_anonymous()
    {
        var anonymous = Actions.Value.Where(IsAnonymous).Select(Key).ToHashSet(StringComparer.Ordinal);
        var stale = PublicActions.Where(k => !anonymous.Contains(k)).ToList();
        Assert.True(stale.Count == 0, "Allow-list entries without a matching [AllowAnonymous] action: " + string.Join(", ", stale));
    }

    [Theory]
    [InlineData("Account", "Logout")]
    [InlineData("Verify", "QuickAction")]
    [InlineData("Leaderboard", "Refresh")]
    [InlineData("Home", "LedgerCheck")]
    [InlineData("Bookings", "Pay")]
    [InlineData("EquipmentOwner", "RequestPayout")]
    public void Sensitive_actions_are_never_anonymous(string controller, string action)
    {
        var matches = Actions.Value.Where(a => a.ControllerName == controller && a.ActionName == action).ToList();
        Assert.NotEmpty(matches);
        Assert.DoesNotContain(matches, IsAnonymous);
    }

    private static bool RequiresVerifiedEmail(ControllerActionDescriptor action) =>
        action.EndpointMetadata.OfType<IAuthorizeData>().Any(a => a.Policy == Models.Entities.AppPolicies.VerifiedEmail);

    [Theory]
    [InlineData("EquipmentOwner", "Create")]
    [InlineData("EquipmentOwner", "Save")]
    [InlineData("GodownOwner", "Create")]
    [InlineData("GodownOwner", "Save")]
    [InlineData("Equipment", "SubmitRequest")]
    [InlineData("Godown", "SubmitBooking")]
    [InlineData("HarvestPlan", "Submit")]
    [InlineData("Bookings", "Pay")]
    [InlineData("Bookings", "Gateway")]
    [InlineData("Bookings", "PaymentCallback")]
    [InlineData("EquipmentOwner", "RequestPayout")]
    [InlineData("GodownOwner", "RequestPayout")]
    [InlineData("Account", "Verification")]
    public void Actions_where_a_wrong_address_causes_harm_require_a_verified_email(string controller, string action)
    {
        var matches = Actions.Value.Where(a => a.ControllerName == controller && a.ActionName == action).ToList();
        Assert.NotEmpty(matches);
        Assert.All(matches, a => Assert.True(RequiresVerifiedEmail(a), $"{Key(a)} does not require a verified e-mail."));
    }

    [Theory]
    [InlineData("Account", "Profile")]
    [InlineData("Account", "UpdateProfile")]
    [InlineData("Account", "Onboarding")]
    [InlineData("Account", "Logout")]
    [InlineData("Bookings", "Index")]
    [InlineData("Bookings", "Cancel")]
    [InlineData("Equipment", "Index")]
    [InlineData("Advisory", "Index")]
    public void Browsing_profile_editing_and_cancellations_stay_open_to_unverified_accounts(string controller, string action)
    {
        var matches = Actions.Value.Where(a => a.ControllerName == controller && a.ActionName == action).ToList();
        Assert.NotEmpty(matches);
        Assert.DoesNotContain(matches, RequiresVerifiedEmail);
    }

    [Theory]
    [InlineData("Account", "Login", "POST", RateLimitPolicies.Auth)]
    [InlineData("Account", "Register", "POST", RateLimitPolicies.Auth)]
    [InlineData("Account", "VerifyEmail", "POST", RateLimitPolicies.Auth)]
    [InlineData("Account", "ResendConfirmation", "POST", RateLimitPolicies.Auth)]
    [InlineData("Account", "ForgotPassword", "POST", RateLimitPolicies.Auth)]
    [InlineData("Account", "ResetPassword", "POST", RateLimitPolicies.Auth)]
    [InlineData("Account", "ChangePassword", "POST", RateLimitPolicies.Auth)]
    [InlineData("Account", "AuthCallback", "GET", RateLimitPolicies.Auth)]
    [InlineData("Equipment", "Quote", "GET", RateLimitPolicies.ReadJson)]
    [InlineData("Equipment", "FreeUnits", "GET", RateLimitPolicies.ReadJson)]
    [InlineData("Equipment", "FilterData", "GET", RateLimitPolicies.ReadJson)]
    [InlineData("Godown", "FilterData", "GET", RateLimitPolicies.ReadJson)]
    [InlineData("Advisory", "WeatherSuggestionsJson", "GET", RateLimitPolicies.ReadJson)]
    [InlineData("Reviews", "Submit", "POST", RateLimitPolicies.Write)]
    [InlineData("Reviews", "Reply", "POST", RateLimitPolicies.Write)]
    [InlineData("HarvestPlan", "Submit", "POST", RateLimitPolicies.Write)]
    [InlineData("Equipment", "SubmitRequest", "POST", RateLimitPolicies.Write)]
    [InlineData("Godown", "SubmitBooking", "POST", RateLimitPolicies.Write)]
    [InlineData("Bookings", "Pay", "POST", RateLimitPolicies.Write)]
    [InlineData("Bookings", "Cancel", "POST", RateLimitPolicies.Write)]
    [InlineData("Leaderboard", "Refresh", "POST", RateLimitPolicies.Write)]
    public void Throttled_endpoints_carry_their_rate_limit_policy(string controller, string action, string method, string policy)
    {
        var match = Assert.Single(Actions.Value, a => a.ControllerName == controller && a.ActionName == action && Key(a).EndsWith(" " + method));
        var applied = match.EndpointMetadata.OfType<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>().LastOrDefault();
        Assert.Equal(policy, applied?.PolicyName);
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = typeof(HomeController).Assembly.GetName().Name!;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string EnvironmentName { get; set; } = Environments.Production;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
    }
}
