using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace KrishiLink.Controllers
{
    /// <summary>
    /// A write based on a stale read (xmin concurrency token) becomes a "review and retry" message instead of a 500.
    /// Nothing was saved: the service's SaveChanges threw, so its transaction rolled back.
    /// </summary>
    public sealed class ConcurrencyConflictFilter(
        IStringLocalizer<SharedResource> localizer,
        ITempDataDictionaryFactory tempData,
        ILogger<ConcurrencyConflictFilter> logger) : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (context.Exception is not DbUpdateConcurrencyException) return;

            var request = context.HttpContext.Request;
            logger.LogWarning("Concurrency conflict on {Method} {Path}.", request.Method, request.Path);
            var message = localizer["This was just updated by someone else. Please review the latest details and try again."].Value;

            if (WantsJson(request))
            {
                context.Result = new ObjectResult(new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = message })
                {
                    StatusCode = StatusCodes.Status409Conflict,
                    ContentTypes = { "application/problem+json" }
                };
            }
            else
            {
                tempData.GetTempData(context.HttpContext)["ErrorMessage"] = message;
                context.Result = new LocalRedirectResult(LocalReferer(request) ?? "/");
            }
            context.ExceptionHandled = true;
        }

        internal static bool WantsJson(HttpRequest request) =>
            request.Headers.XRequestedWith == "XMLHttpRequest"
            || request.Headers.Accept.Any(a => a is not null && a.Contains("json", StringComparison.OrdinalIgnoreCase));

        private static string? LocalReferer(HttpRequest request) =>
            Uri.TryCreate(request.Headers.Referer.ToString(), UriKind.Absolute, out var referer)
            && string.Equals(referer.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase)
                ? referer.PathAndQuery
                : null;
    }
}
