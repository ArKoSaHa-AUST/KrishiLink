using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace KrishiLink.Controllers
{
    /// <summary>
    /// fetch/XHR callers get application/problem+json for an unhandled exception instead of the HTML error page they
    /// cannot parse. Browser navigations keep the normal exception handler. Stale writes are left to
    /// <see cref="ConcurrencyConflictFilter"/>, which answers them with a 409.
    /// </summary>
    public sealed class JsonExceptionFilter(IStringLocalizer<SharedResource> localizer, ILogger<JsonExceptionFilter> logger) : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (context.ExceptionHandled || context.Exception is DbUpdateConcurrencyException) return;
            if (!ConcurrencyConflictFilter.WantsJson(context.HttpContext.Request)) return;

            logger.LogError(context.Exception, "Unhandled exception on {Method} {Path}.", context.HttpContext.Request.Method, context.HttpContext.Request.Path);
            var message = localizer["Something went wrong. Please try again."].Value;
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = message,
                Extensions = { ["success"] = false, ["message"] = message, ["traceId"] = context.HttpContext.TraceIdentifier }
            };
            context.Result = new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status500InternalServerError,
                ContentTypes = { "application/problem+json" }
            };
            context.ExceptionHandled = true;
        }
    }
}
