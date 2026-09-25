using System.Security.Claims;
using KrishiLink.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace KrishiLink.Tests;

/// <summary>REA-01: only a signed-out visitor's advisory page may be kept for offline reading.</summary>
public class OfflineSupportTests
{
    private static HttpContext Run(string method, ClaimsPrincipal user)
    {
        var http = new DefaultHttpContext { User = user };
        http.Request.Method = method;
        var context = new ResultExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(), new ViewResult(), controller: new object());
        new OfflineCacheableAttribute().OnResultExecuting(context);
        return http;
    }

    [Fact]
    public void A_signed_out_visitors_page_is_marked_cacheable() =>
        Assert.Equal("1", Run("GET", new ClaimsPrincipal(new ClaimsIdentity())).Response.Headers[OfflineCacheableAttribute.Header].ToString());

    [Fact]
    public void A_signed_in_page_is_never_marked() =>
        Assert.False(Run("GET", new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "u1") }, "test")))
            .Response.Headers.ContainsKey(OfflineCacheableAttribute.Header));

    [Fact]
    public void A_form_post_is_never_marked() =>
        Assert.False(Run("POST", new ClaimsPrincipal(new ClaimsIdentity())).Response.Headers.ContainsKey(OfflineCacheableAttribute.Header));

    [Fact]
    public void The_manifest_is_installable()
    {
        var root = CropAdvisorTests.RepoRoot();
        using var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "wwwroot", "manifest.json")));
        var m = manifest.RootElement;
        Assert.Equal("standalone", m.GetProperty("display").GetString());
        var icons = m.GetProperty("icons").EnumerateArray().ToList();
        Assert.Contains(icons, i => i.GetProperty("sizes").GetString() == "512x512" && i.GetProperty("purpose").GetString() == "maskable");
        Assert.Contains(icons, i => i.GetProperty("sizes").GetString() == "192x192" && i.GetProperty("purpose").GetString() == "any");
        foreach (var icon in icons)
            Assert.True(File.Exists(Path.Combine(root, "wwwroot", icon.GetProperty("src").GetString()!.TrimStart('/'))));
    }
}
