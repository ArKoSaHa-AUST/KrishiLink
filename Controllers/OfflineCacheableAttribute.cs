using Microsoft.AspNetCore.Mvc.Filters;

namespace KrishiLink.Controllers
{
    /// <summary>
    /// Tells the service worker (wwwroot/sw.js) it may keep this page for offline reading (REA-01) — but only for a
    /// signed-out visitor. A signed-in page carries the user's name, district and anti-forgery token, so it is never marked
    /// and never stored.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class OfflineCacheableAttribute : ActionFilterAttribute
    {
        public const string Header = "X-Offline-Cacheable";

        public override void OnResultExecuting(ResultExecutingContext context)
        {
            var http = context.HttpContext;
            if (HttpMethods.IsGet(http.Request.Method) && http.User.Identity?.IsAuthenticated != true)
                http.Response.Headers[Header] = "1";
        }
    }
}
