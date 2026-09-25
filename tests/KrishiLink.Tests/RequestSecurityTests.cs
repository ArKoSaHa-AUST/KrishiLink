using System.Net;
using System.Security.Claims;
using KrishiLink.BLL.Services;
using KrishiLink.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KrishiLink.Tests;

public class RequestSecurityTests
{
    private static HttpRequest Request(string host)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString(host);
        return context.Request;
    }

    private static IOptions<AppOptions> Options(string url) => Microsoft.Extensions.Options.Options.Create(new AppOptions { PublicBaseUrl = url });

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Outside_development_links_ignore_a_forged_host_header(string environment)
    {
        var origin = AppLinks.PublicOrigin(Options("https://krishilink.example.org/"), new Env(environment), Request("attacker.example"));
        Assert.Equal("https://krishilink.example.org", origin);
    }

    [Fact]
    public void Development_uses_the_request_origin_so_lan_devices_can_scan_qr_codes()
    {
        var origin = AppLinks.PublicOrigin(Options("https://krishilink.example.org"), new Env(Environments.Development), Request("192.168.1.20:5141"));
        Assert.Equal("http://192.168.1.20:5141", origin);
    }

    [Fact]
    public void Signed_in_users_are_rate_limited_per_user_and_anonymous_callers_per_ip()
    {
        var anonymous = new DefaultHttpContext();
        anonymous.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        Assert.Equal("ip:203.0.113.7", RateLimitPolicies.PartitionKey(anonymous));

        var signedIn = new DefaultHttpContext();
        signedIn.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        signedIn.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "user-42") }, "cookie"));
        Assert.Equal("user:user-42", RateLimitPolicies.PartitionKey(signedIn));
    }

    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "KrishiLink";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
