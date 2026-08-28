using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    public class BookingsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
