using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    public class FarmerController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
