using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    public class GodownController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Details(int id)
        {
            return View();
        }
    }
}
