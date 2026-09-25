using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using KrishiLink.Controllers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KrishiLink.Tests;

/// <summary>SEC-04 / SEC-08: one rate-limiting mechanism, partitioned per user when signed in and per IP otherwise.</summary>
public class RateLimitingTests : IAsyncLifetime
{
    private WebApplication? _app;
    private HttpClient _http = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["AllowedHosts"] = "*";
        builder.Logging.ClearProviders();
        builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
        builder.Services.AddControllers()
            .ConfigureApplicationPartManager(parts =>
            {
                parts.ApplicationParts.Clear();
                parts.FeatureProviders.Add(new LimitedControllerFeature());
            });
        builder.Services.AddAuthentication(HeaderUser.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderUser>(HeaderUser.SchemeName, _ => { });
        builder.Services.AddKrishiLinkRateLimiting();

        _app = builder.Build();
        _app.UseRouting();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.UseRateLimiter();
        _app.MapControllers();
        await _app.StartAsync();
        _http = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        if (_app is not null) await _app.DisposeAsync();
    }

    private Task<HttpResponseMessage> PostAsync(string path, string? user = null, bool json = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (user is not null) request.Headers.Add(HeaderUser.Header, user);
        if (json) request.Headers.Accept.ParseAdd("application/json");
        return _http.SendAsync(request);
    }

    [Fact]
    public async Task The_eleventh_auth_attempt_in_a_minute_is_rejected_with_retry_after()
    {
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.OK, (await PostAsync("/limited/login", json: true)).StatusCode);

        var rejected = await PostAsync("/limited/login", json: true);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.Contains("\"success\":false", await rejected.Content.ReadAsStringAsync());

        // Auth buckets are per action, so exhausting one does not block another.
        Assert.Equal(HttpStatusCode.OK, (await PostAsync("/limited/register", json: true)).StatusCode);
    }

    [Fact]
    public async Task Signed_in_users_do_not_share_a_bucket_with_their_neighbours()
    {
        for (var i = 0; i < 30; i++)
            Assert.Equal(HttpStatusCode.OK, (await PostAsync("/limited/write", "alice")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await PostAsync("/limited/write", "alice")).StatusCode);

        // Same IP (the test client), different account: unaffected.
        Assert.Equal(HttpStatusCode.OK, (await PostAsync("/limited/write", "bob")).StatusCode);
    }

    [Route("limited")]
    public sealed class LimitedController : ControllerBase
    {
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Auth)]
        public IActionResult Login() => Ok();

        [HttpPost("register")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Auth)]
        public IActionResult Register() => Ok();

        [HttpPost("write")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public IActionResult Write() => Ok();
    }

    private sealed class LimitedControllerFeature : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature) =>
            feature.Controllers.Add(typeof(LimitedController).GetTypeInfo());
    }

    private sealed class HeaderUser(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "header-user";
        public const string Header = "X-Test-User";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(Header, out var user)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.ToString()) }, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
