using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using KrishiLink.BLL.Services;
using KrishiLink.Controllers;
using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KrishiLink.Tests;

/// <summary>SEC-01: an address counts as verified only when Supabase says so, and unverified accounts are gated.</summary>
public class EmailVerificationTests
{
    private static ClaimsPrincipal Principal(string? emailVerified, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "user-1"), new(ClaimTypes.Email, "farmer@example.test") };
        if (emailVerified is not null) claims.Add(new Claim(AppPolicies.EmailVerifiedClaim, emailVerified));
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static IAuthorizationService AuthorizationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(MvcSecurity.ConfigurePolicies);
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public async Task VerifiedEmail_policy_requires_the_claim_to_be_true(string? claim, bool allowed)
    {
        var result = await AuthorizationService().AuthorizeAsync(Principal(claim), AppPolicies.VerifiedEmail);
        Assert.Equal(allowed, result.Succeeded);
    }

    [Fact]
    public async Task VerifiedEmail_policy_refuses_anonymous_users()
    {
        var result = await AuthorizationService().AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), AppPolicies.VerifiedEmail);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task An_unconfirmed_address_alone_redirects_to_the_explanatory_page()
    {
        var context = new DefaultHttpContext { User = Principal("false") };
        var policy = new AuthorizationPolicyBuilder().RequireClaim(AppPolicies.EmailVerifiedClaim, "true").Build();
        var failure = AuthorizationFailure.Failed(policy.Requirements);

        await new VerifiedEmailResultHandler().HandleAsync(_ => Task.CompletedTask, context, policy, PolicyAuthorizationResult.Forbid(failure));

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/Account/AccessDenied?reason=email", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task A_role_failure_is_not_blamed_on_the_email_address()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication("cookie").AddCookie("cookie", o => o.AccessDeniedPath = "/Account/AccessDenied");
        var context = new DefaultHttpContext { User = Principal("false", AppRoles.Farmer), RequestServices = services.BuildServiceProvider() };
        var policy = new AuthorizationPolicyBuilder()
            .RequireRole(AppRoles.EquipmentOwner)
            .RequireClaim(AppPolicies.EmailVerifiedClaim, "true")
            .Build();
        var failure = AuthorizationFailure.Failed(policy.Requirements);

        await new VerifiedEmailResultHandler().HandleAsync(_ => Task.CompletedTask, context, policy, PolicyAuthorizationResult.Forbid(failure));

        Assert.DoesNotContain("reason=email", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task Supabase_error_code_is_exposed_without_the_response_body()
    {
        var client = Client(HttpStatusCode.BadRequest, """{"code":400,"error_code":"email_not_confirmed","msg":"Email not confirmed for farmer@example.test"}""");

        var ex = await Assert.ThrowsAsync<SupabaseAuthException>(() => client.SignInAsync("farmer@example.test", "secret-password"));

        Assert.Equal(SupabaseAuthException.EmailNotConfirmed, ex.ErrorCode);
        Assert.DoesNotContain("farmer@example.test", ex.Message);
    }

    [Theory]
    [InlineData("""{"error_code":"<script>alert(1)</script>"}""")]
    [InlineData("""{"error_code":42}""")]
    [InlineData("not json")]
    [InlineData("")]
    public async Task Unexpected_error_payloads_yield_no_error_code(string body)
    {
        var client = Client(HttpStatusCode.BadRequest, body);
        var ex = await Assert.ThrowsAsync<SupabaseAuthException>(() => client.SignInAsync("a@example.test", "secret-password"));
        Assert.Null(ex.ErrorCode);
    }

    [Fact]
    public async Task New_users_are_created_confirmed_by_default()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """{"id":"00000000-0000-0000-0000-000000000001","email":"a@example.test"}""");
        var client = new SupabaseAuthClient(new HttpClient(handler), Options.Create(new SupabaseAuthOptions
        {
            Url = "https://project.supabase.co",
            PublishableKey = "sb_publishable_test",
            SecretKey = "sb_secret_test"
        }));

        await client.CreateUserAsync("a@example.test", "secret-password");

        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.True(body.RootElement.GetProperty("email_confirm").GetBoolean());
    }

    /// <summary>The acceptance criterion as a guard: email confirmation occurs on creation or from verified records.</summary>
    [Fact]
    public void Email_confirmation_is_only_ever_set_from_a_verified_Supabase_record_or_registration()
    {
        var root = RepositoryRoot();
        var sources = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(p => !Regex.IsMatch(p, @"[\\/](bin|obj|tests|\.kilo|\.git)[\\/]"))
            .ToList();

        var confirmations = sources
            .SelectMany(p => File.ReadAllLines(p).Select((line, i) => (Path: Path.GetRelativePath(root, p).Replace('\\', '/'), Line: line, Index: i)))
            .Where(x => Regex.IsMatch(x.Line, @"EmailConfirmed\s*=\s*true"))
            .ToList();

        var allowed = new[]
        {
            "Controllers/AccountController.cs",
            "Controllers/AccountController.Auth.cs",
            "Controllers/AccountController.EmailLinks.cs",
            "BLL/Services/SupabaseAdminBootstrap.cs"
        };
        Assert.All(confirmations, c => Assert.Contains(c.Path, allowed));
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KrishiLink.csproj"))) dir = dir.Parent;
        return dir!.FullName;
    }

    private static SupabaseAuthClient Client(HttpStatusCode status, string body) =>
        new(new HttpClient(new RecordingHandler(status, body)), Options.Create(new SupabaseAuthOptions
        {
            Url = "https://project.supabase.co",
            PublishableKey = "sb_publishable_test",
            SecretKey = "sb_secret_test"
        }));

    private sealed class RecordingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
