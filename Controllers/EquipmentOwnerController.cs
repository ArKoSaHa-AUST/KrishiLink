using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    public class EquipmentOwnerController : Controller
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

        /// <summary>
        /// GET: /EquipmentOwner/Availability/1
        /// Renders Manage Equipment Availability calendar page for an owner.
        /// </summary>
        public IActionResult Availability(int id = 1)
        {
            var today = DateTime.Today;

            // Farmer booked dates (Locked - non-editable)
            var bookedDates = new List<DateTime>
            {
                today.AddDays(2),
                today.AddDays(3),
                today.AddDays(4),
                today.AddDays(15),
                today.AddDays(16)
            };

            // Owner blocked dates (Maintenance / Personal use)
            var blockedDates = new List<DateTime>
            {
                today.AddDays(8),
                today.AddDays(9),
                today.AddDays(22)
            };

            var model = new ManageAvailabilityViewModel
            {
                EquipmentId = id > 0 ? id : 1,
                EquipmentName = "Mahindra 575 DI Heavy Tractor",
                Category = "Heavy Machinery",
                DailyRate = "৳1,500 / Day",
                Location = "Bogra Sadar, Bogra",
                ThumbnailUrl = "https://images.unsplash.com/photo-1592878904946-b3cd8ae243d0?auto=format&fit=crop&w=400&q=80",
                MonthName = today.ToString("MMMM yyyy"),

                FarmerBookedDates = bookedDates,
                OwnerBlockedDates = blockedDates
            };

            return View(model);
        }

        /// <summary>
        /// POST: /EquipmentOwner/SaveAvailability
        /// Saves updated availability calendar changes.
        /// </summary>
        [HttpPost]
        public IActionResult SaveAvailability(ManageAvailabilityViewModel model)
        {
            TempData["SuccessMessage"] = "Equipment availability calendar updated successfully!";
            return RedirectToAction(nameof(Availability), new { id = model.EquipmentId });
        }
    }
}
