using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    public class AdvisoryController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
