using KrishiLink.BLL.Services;
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

        [Route("")]
        [Route("Home")]
        [Route("Home/Index")]
        [Route("landingpage")]
        public IActionResult Index()
        {
            return View();
        }

        [Route("Privacy")]
        [Route("Home/Privacy")]
        public IActionResult Privacy()
        {
            return View();
        }

        // Sets the language culture cookie and redirects back — pure server-side, no JavaScript.
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
