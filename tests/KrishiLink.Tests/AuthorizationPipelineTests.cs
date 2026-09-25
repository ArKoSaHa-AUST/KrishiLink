using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using KrishiLink.Controllers;
using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KrishiLink.Tests;

/// <summary>
/// Runs the real ASP.NET Core pipeline (Kestrel on a random loopback port) with the production security configuration,
/// proving the attribute policies are enforced by the authorization middleware and the result handler is reached.
/// </summary>
public class AuthorizationPipelineTests : IAsyncLifetime
{
    private WebApplication? _app;
    private HttpClient _http = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["AllowedHosts"] = "*";
        builder.Logging.ClearProviders();
        builder.Services.AddControllers(MvcSecurity.Configure)
            .ConfigureApplicationPartManager(parts =>
            {
                parts.ApplicationParts.Clear();
                parts.FeatureProviders.Add(new ProbeControllerFeature());
            });
        builder.Services.AddAuthentication(HeaderAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthHandler>(HeaderAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization(MvcSecurity.ConfigurePolicies);
        builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, VerifiedEmailResultHandler>();

        _app = builder.Build();
        _app.UseRouting();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        await _app.StartAsync();

        _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(_app.Urls.First()) };
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        if (_app is not null) await _app.DisposeAsync();
    }

    private Task<HttpResponseMessage> GetAsync(string path, string? emailVerified)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (emailVerified is not null) request.Headers.Add(HeaderAuthHandler.Header, emailVerified);
        return _http.SendAsync(request);
    }

    [Fact]
    public async Task Unconfirmed_account_is_sent_to_the_explanation_page()
    {
        var response = await GetAsync("/probe/gated", "false");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/AccessDenied?reason=email", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Confirmed_account_passes_the_gate()
    {
        Assert.Equal(HttpStatusCode.OK, (await GetAsync("/probe/gated", "true")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_request_is_challenged_on_gated_and_on_unattributed_actions()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("/probe/gated", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("/probe/unattributed", null)).StatusCode);
    }

    [Fact]
    public async Task Unattributed_action_is_open_to_any_signed_in_user_and_public_action_to_everyone()
    {
        Assert.Equal(HttpStatusCode.OK, (await GetAsync("/probe/unattributed", "false")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetAsync("/probe/public", null)).StatusCode);
    }

    [Route("probe")]
    public sealed class ProbeController : ControllerBase
    {
        [HttpGet("gated")]
        [Authorize(Policy = AppPolicies.VerifiedEmail)]
        public IActionResult Gated() => Ok();

        [HttpGet("unattributed")]
        public IActionResult Unattributed() => Ok();

        [HttpGet("public")]
        [AllowAnonymous]
        public IActionResult Public() => Ok();
    }

    private sealed class ProbeControllerFeature : Microsoft.AspNetCore.Mvc.ApplicationParts.IApplicationFeatureProvider<Microsoft.AspNetCore.Mvc.Controllers.ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPart> parts, Microsoft.AspNetCore.Mvc.Controllers.ControllerFeature feature) =>
            feature.Controllers.Add(typeof(ProbeController).GetTypeInfo());
    }

    /// <summary>Signs the request in when the test header is present; its value becomes the email_verified claim.</summary>
    private sealed class HeaderAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "probe";
        public const string Header = "X-Probe-Email-Verified";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(Header, out var verified)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "probe-user"),
                new Claim(AppPolicies.EmailVerifiedClaim, verified.ToString())
            }, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
