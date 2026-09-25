using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace KrishiLink.Controllers
{
    /// <summary>
    /// Security response headers for every response, including static files and re-executed error pages.
    /// Inline &lt;script&gt; blocks are allowed only through a per-request nonce (added by <see cref="ScriptNonceTagHelper"/>),
    /// never through 'unsafe-inline'. Enforced by default (Security:EnforceContentSecurityPolicy); report-only when it is false.
    /// </summary>
    public static class SecurityHeaders
    {
        public const string NonceItemKey = "krishilink:csp-nonce";
        public const string ReportPath = "/csp-report";

        public static string Policy(string nonce) => string.Join("; ",
            "default-src 'self'",
            $"script-src 'self' 'nonce-{nonce}' https://cdn.jsdelivr.net https://unpkg.com",
            // Inline style attributes are pervasive in the views and cannot carry a nonce; styles cannot run code.
            "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com https://unpkg.com",
            "font-src 'self' https://cdn.jsdelivr.net https://fonts.gstatic.com",
            "img-src 'self' data: https://*.supabase.co https://unpkg.com https://*.tile.openstreetmap.org",
            "connect-src 'self' https://*.supabase.co",
            "object-src 'none'",
            "frame-ancestors 'none'",
            "base-uri 'self'",
            "form-action 'self'",
            $"report-uri {ReportPath}");

        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, bool enforceCsp)
        {
            var cspHeader = enforceCsp ? "Content-Security-Policy" : "Content-Security-Policy-Report-Only";
            return app.Use(async (context, next) =>
            {
                var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
                context.Items[NonceItemKey] = nonce;

                // OnStarting: the exception handler clears headers before re-executing, so set them as late as possible.
                // An action may already have set a stricter value (e.g. no-referrer on token-bearing e-mail link pages).
                context.Response.OnStarting(() =>
                {
                    var headers = context.Response.Headers;
                    headers[cspHeader] = Policy(nonce);
                    headers.TryAdd("X-Content-Type-Options", "nosniff");
                    headers.TryAdd("X-Frame-Options", "DENY");
                    headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
                    // The owner QR scanner needs the camera and listing forms can use the device location — same origin only.
                    headers.TryAdd("Permissions-Policy", "geolocation=(self), camera=(self), microphone=(), payment=()");
                    return Task.CompletedTask;
                });
                await next();
            });
        }

        /// <summary>Receives browser CSP violation reports so the policy can be tightened from real data.</summary>
        public static IEndpointRouteBuilder MapCspReports(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapPost(ReportPath, async (HttpContext context, ILoggerFactory loggers) =>
            {
                var logger = loggers.CreateLogger(typeof(SecurityHeaders));
                try
                {
                    using var reader = new StreamReader(context.Request.Body);
                    var buffer = new char[8 * 1024];
                    var length = await reader.ReadBlockAsync(buffer, context.RequestAborted);
                    using var json = JsonDocument.Parse(new string(buffer, 0, length));
                    if (json.RootElement.TryGetProperty("csp-report", out var report))
                    {
                        logger.LogWarning("CSP violation: {Directive} blocked {BlockedUri} on {DocumentUri} ({SourceFile}:{Line})",
                            Field(report, "violated-directive"), Field(report, "blocked-uri"), PathOnly(Field(report, "document-uri")),
                            PathOnly(Field(report, "source-file")), Field(report, "line-number"));
                    }
                }
                catch (Exception ex) when (ex is JsonException or IOException or OperationCanceledException)
                {
                    // Malformed or truncated reports are ignored.
                }
                return Results.NoContent();
            }).DisableAntiforgery();
            return endpoints;
        }

        private static string Field(JsonElement report, string name) =>
            report.TryGetProperty(name, out var value) ? Truncate(value.ToString()) : string.Empty;

        // Query strings can carry tokens (QR verification links), so only the path is logged.
        private static string PathOnly(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Path) : Truncate(url);

        private static string Truncate(string value) => value.Length <= 200 ? value : value[..200];
    }

    /// <summary>Stamps the request's CSP nonce on every &lt;script&gt; element rendered by Razor.</summary>
    [HtmlTargetElement("script")]
    public sealed class ScriptNonceTagHelper : TagHelper
    {
        [ViewContext]
        [HtmlAttributeNotBound]
        public ViewContext ViewContext { get; set; } = null!;

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (ViewContext.HttpContext.Items[SecurityHeaders.NonceItemKey] is string nonce)
                output.Attributes.SetAttribute("nonce", nonce);
        }
    }
}
