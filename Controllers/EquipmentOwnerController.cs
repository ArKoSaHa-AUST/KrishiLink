using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    [Authorize(Roles = "EquipmentOwner")]
    public class EquipmentOwnerController : Controller
    {
        public IActionResult Index()
        {
            var model = new EquipmentOwnerDashboardViewModel
            {
                OwnerName = User.Identity?.Name ?? "Owner",
                TotalListings = 5,
                ActiveRentals = 2,
                Listings = new List<OwnerListingItem>
                {
                    new() { Id = 1, Name = "Mahindra 575 DI Heavy Tractor", Category = "Tractor", Status = "Rented", DailyRate = "৳1,500 / Day",
                            ImageUrl = "https://images.unsplash.com/photo-1592878904946-b3cd8ae243d0?auto=format&fit=crop&w=600&q=80" },
                    new() { Id = 2, Name = "Kubota DC-70 Combine Harvester", Category = "Harvester", Status = "Available", DailyRate = "৳4,200 / Day",
                            ImageUrl = "https://images.unsplash.com/photo-1591086429666-871b06bcccb4?auto=format&fit=crop&w=600&q=80" },
                    new() { Id = 3, Name = "ACI Power Tiller 12HP", Category = "Tiller", Status = "Available", DailyRate = "৳800 / Day",
                            ImageUrl = "https://images.unsplash.com/photo-1530267981375-f0de937f5f13?auto=format&fit=crop&w=600&q=80" },
                    new() { Id = 4, Name = "Honda WB30X Irrigation Pump", Category = "Irrigation", Status = "Unavailable", DailyRate = "৳350 / Day",
                            ImageUrl = "https://images.unsplash.com/photo-1625246333195-78d9c38ad449?auto=format&fit=crop&w=600&q=80" },
                    new() { Id = 5, Name = "TAFE 45DI Rotavator", Category = "Tiller", Status = "Available", DailyRate = "৳950 / Day",
                            ImageUrl = "https://images.unsplash.com/photo-1595246140625-573b715d11dc?auto=format&fit=crop&w=600&q=80" }
                },
                PendingRequestItems = new List<PendingRequestItem>
                {
                    new() { Id = 101, FarmerName = "Rahim Uddin", EquipmentName = "Kubota DC-70 Combine Harvester",
                            DateRange = "02 Sep – 06 Sep 2026", Note = "Need it for 5 acres of Aman paddy harvest." },
                    new() { Id = 102, FarmerName = "Karim Mia", EquipmentName = "ACI Power Tiller 12HP",
                            DateRange = "05 Sep – 07 Sep 2026" },
                    new() { Id = 103, FarmerName = "Fatema Begum", EquipmentName = "Mahindra 575 DI Heavy Tractor",
                            DateRange = "10 Sep – 12 Sep 2026", Note = "Land preparation before potato season." }
                }
            };

            return View(model);
        }

        /// <summary>
        /// POST: /EquipmentOwner/RespondRequest
        /// Handles inline Accept/Reject from the dashboard pending requests widget.
        /// An optional reject reason is forwarded to the farmer.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RespondRequest(int id, string decision, string? reason = null)
        {
            var accepted = string.Equals(decision, "accept", StringComparison.OrdinalIgnoreCase);
            return Json(new { success = true, message = $"Request #{id} {(accepted ? "accepted" : "rejected")}." });
        }

        /// <summary>
        /// GET: /EquipmentOwner/PendingCount
        /// Lightweight polling endpoint so the dashboard can animate counts
        /// when new rental requests arrive. Returns sample data until DB wiring.
        /// </summary>
        [HttpGet]
        public IActionResult PendingCount()
        {
            return Json(new { count = 3 });
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
        [ValidateAntiForgeryToken]
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
        [ValidateAntiForgeryToken]
        public IActionResult SaveAvailability(ManageAvailabilityViewModel model)
        {
            TempData["SuccessMessage"] = "Equipment availability calendar updated successfully!";
            return RedirectToAction(nameof(Availability), new { id = model.EquipmentId });
        }
    }
}
