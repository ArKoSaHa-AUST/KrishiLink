using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    public class EquipmentController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// GET: /Equipment/Details/1
        /// Equipment Details & Rental Request page.
        /// </summary>
        public IActionResult Details(int id = 1)
        {
            // Sample booked dates (e.g., current month dates blocked out)
            var today = DateTime.Today;
            var bookedDates = new List<DateTime>
            {
                today.AddDays(2),
                today.AddDays(3),
                today.AddDays(4),
                today.AddDays(10),
                today.AddDays(11),
                today.AddDays(18)
            };

            var model = new EquipmentDetailViewModel
            {
                Id = id > 0 ? id : 1,
                Name = "Mahindra 575 DI Heavy Tractor",
                Category = "Heavy Machinery",
                Description = "High-performance 45 HP 4-stroke diesel tractor ideal for deep tilling, seedbed preparation, harrowing, and heavy agricultural haulage. Well-maintained with fuel-efficient engine and power steering. Comes with standard hitch attachment.",
                DailyRate = "৳1,500 / Day",
                HourlyRate = "৳250 / Hour",
                Location = "Bogra Sadar, Bogra, Rajshahi Division",
                Status = "Available",

                OwnerName = "Abdul Karim",
                OwnerRating = 4.9,
                TotalReviews = 32,
                OwnerPhone = "+880 1712-345678",
                OwnerMemberSince = "March 2024",

                ImageUrls = new List<string>
                {
                    "https://images.unsplash.com/photo-1592878904946-b3cd8ae243d0?auto=format&fit=crop&w=800&q=80",
                    "https://images.unsplash.com/photo-1530267981375-f0de937f5f13?auto=format&fit=crop&w=800&q=80",
                    "https://images.unsplash.com/photo-1589923188900-85dae523342b?auto=format&fit=crop&w=800&q=80"
                },

                BookedDates = bookedDates
            };

            return View(model);
        }

        /// <summary>
        /// POST: /Equipment/SubmitRequest
        /// Handles rental request submission.
        /// </summary>
        [HttpPost]
        public IActionResult SubmitRequest(EquipmentDetailViewModel model)
        {
            // Simulating successful request submission
            TempData["SuccessMessage"] = "Rental request sent to owner! You will be notified once accepted.";
            return RedirectToAction(nameof(Details), new { id = model.Id, requestSent = true });
        }
    }
}
