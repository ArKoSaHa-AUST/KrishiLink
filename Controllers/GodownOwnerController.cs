using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    public class GodownOwnerController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Create()
        {
            return View();
        }

        public IActionResult Requests()
        {
            return View();
        }
    }
}
