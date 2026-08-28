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

        /// <summary>
        /// GET: /EquipmentOwner/Create
        /// Renders blank Add Equipment Listing form.
        /// </summary>
        [HttpGet]
        public IActionResult Create()
        {
            var model = new EquipmentListingViewModel
            {
                DailyRate = 1500,
                IsAvailable = true,
                ExistingImageUrls = new List<string>()
            };
            return View(model);
        }

        /// <summary>
        /// GET: /EquipmentOwner/Edit/1
        /// Renders pre-filled Edit Equipment Listing form.
        /// </summary>
        [HttpGet]
        public IActionResult Edit(int id = 1)
        {
            var model = new EquipmentListingViewModel
            {
                Id = id,
                Name = "Mahindra 575 DI Heavy Tractor",
                Category = "Tractor",
                Description = "High-performance 45 HP diesel tractor with 4-wheel drive capability. Ideal for deep tilling, dry and wet paddy field preparation, rotavator attachments, and heavy haulage.",
                Location = "Bogra Sadar, Bogra",
                DailyRate = 1500,
                HourlyRate = 250,
                IsAvailable = true,
                ExistingImageUrls = new List<string>
                {
                    "https://images.unsplash.com/photo-1592878904946-b3cd8ae243d0?auto=format&fit=crop&w=800&q=80",
                    "https://images.unsplash.com/photo-1589923188900-85dae523342b?auto=format&fit=crop&w=800&q=80"
                }
            };
            return View("Create", model);
        }

        /// <summary>
        /// POST: /EquipmentOwner/Save
        /// Handles creation or updating of equipment listing.
        /// </summary>
        [HttpPost]
        public IActionResult Save(EquipmentListingViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Create", model);
            }

            var actionName = model.IsEditMode ? "updated" : "created";
            TempData["SuccessMessage"] = $"Equipment listing '{model.Name}' successfully {actionName}!";
            return RedirectToAction(nameof(Index));
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
