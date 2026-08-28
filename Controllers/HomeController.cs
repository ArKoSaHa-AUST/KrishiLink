using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    public class HomeController : Controller
    {
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
    }
}
