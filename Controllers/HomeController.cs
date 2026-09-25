using System.Diagnostics;
using KrishiLink.BLL.Services;
using KrishiLink.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment _env;

        public HomeController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [AllowAnonymous]
        [Route("")]
        [Route("Home")]
        [Route("Home/Index")]
        [Route("landingpage")]
        public IActionResult Index()
        {
            return View();
        }

        [AllowAnonymous]
        [Route("Privacy")]
        [Route("Home/Privacy")]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [Route("Home/Error")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int? code = null)
        {
            return View(new ErrorViewModel
            {
                // The correlation id (QLT-02), so a user can quote the id the logs carry.
                RequestId = HttpContext.TraceIdentifier,
                StatusCode = code is >= 400 and <= 599 ? code : null
            });
        }

        /// <summary>
        /// GET: /Home/Offline?culture=bn — the page the service worker shows when there is no network (REA-01). It is cached
        /// without cookies, so it renders no user data; the culture comes from the query because the cookie is not sent.
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        [Route("Home/Offline")]
        public IActionResult Offline(string? culture = null)
        {
            if (culture is "en" or "bn")
            {
                var chosen = new System.Globalization.CultureInfo(culture);
                System.Globalization.CultureInfo.CurrentCulture = chosen;
                System.Globalization.CultureInfo.CurrentUICulture = chosen;
            }
            return View();
        }

        // Sets the language culture cookie and redirects back — pure server-side, no JavaScript.
        [AllowAnonymous]
        [HttpPost]
        [Route("Home/SetLanguage")]
        [ValidateAntiForgeryToken]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            if (culture == "en" || culture == "bn")
            {
                Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                    new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, Secure = Request.IsHttps });
            }

            return LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
        }

        /// <summary>GET: /Home/LedgerCheck — Development-only escrow conservation check for manual verification.</summary>
        [HttpGet]
        [Route("Home/LedgerCheck")]
        public IActionResult LedgerCheck([FromServices] ILedgerService ledger)
        {
            if (!_env.IsDevelopment()) return NotFound();
            var check = ledger.CheckConservation();
            return Json(new { ok = check.Ok, lhs = check.Lhs, rhs = check.Rhs, detail = check.Detail });
        }
    }
}
