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
                PendingRequestItems = new List<RentalRequestItem>
                {
                    new() { Id = 101, FarmerName = "Rahim Uddin", EquipmentName = "Kubota DC-70 Combine Harvester",
                            DateRange = "02 Sep – 06 Sep 2026", Note = "Need it for 5 acres of Aman paddy harvest.",
                            RequestedOn = DateTime.Now.AddHours(-2) },
                    new() { Id = 102, FarmerName = "Karim Mia", EquipmentName = "ACI Power Tiller 12HP",
                            DateRange = "05 Sep – 07 Sep 2026",
                            RequestedOn = DateTime.Now.AddHours(-7) },
                    new() { Id = 103, FarmerName = "Fatema Begum", EquipmentName = "Mahindra 575 DI Heavy Tractor",
                            DateRange = "10 Sep – 12 Sep 2026", Note = "Land preparation before potato season.",
                            RequestedOn = DateTime.Now.AddHours(-26) }
                }
            };

            return View(model);
        }

        /// <summary>
        /// GET: /EquipmentOwner/Requests
        /// Full list of incoming rental requests with filter tabs and Accept/Reject.
        /// </summary>
        [HttpGet]
        public IActionResult Requests()
        {
            var requests = new List<RentalRequestItem>
            {
                // Pending (mirrors the dashboard widget)
                new() { Id = 101, FarmerName = "Rahim Uddin", EquipmentName = "Kubota DC-70 Combine Harvester", Status = "Pending",
                        DateRange = "02 Sep – 06 Sep 2026", StartDate = new(2026, 9, 2), EndDate = new(2026, 9, 6),
                        RequestedOn = DateTime.Now.AddHours(-2), Note = "Need it for 5 acres of Aman paddy harvest.",
                        EquipmentCategory = "Harvester", DailyRate = "৳4,200 / Day", Location = "Bogra Sadar, Bogra" },
                new() { Id = 102, FarmerName = "Karim Mia", EquipmentName = "ACI Power Tiller 12HP", Status = "Pending",
                        DateRange = "05 Sep – 07 Sep 2026", StartDate = new(2026, 9, 5), EndDate = new(2026, 9, 7),
                        RequestedOn = DateTime.Now.AddHours(-7),
                        EquipmentCategory = "Tiller", DailyRate = "৳800 / Day", Location = "Bogra Sadar, Bogra" },
                new() { Id = 103, FarmerName = "Fatema Begum", EquipmentName = "Mahindra 575 DI Heavy Tractor", Status = "Pending",
                        DateRange = "10 Sep – 12 Sep 2026", StartDate = new(2026, 9, 10), EndDate = new(2026, 9, 12),
                        RequestedOn = DateTime.Now.AddHours(-26), Note = "Land preparation before potato season.",
                        EquipmentCategory = "Tractor", DailyRate = "৳1,500 / Day", Location = "Bogra Sadar, Bogra" },

                // Accepted
                new() { Id = 96, FarmerName = "Abdul Halim", EquipmentName = "Mahindra 575 DI Heavy Tractor", Status = "Accepted",
                        DateRange = "09 Sep – 11 Sep 2026", StartDate = new(2026, 9, 9), EndDate = new(2026, 9, 11),
                        RequestedOn = DateTime.Now.AddDays(-3), Note = "Tilling 3 acres before Rabi sowing.",
                        EquipmentCategory = "Tractor", DailyRate = "৳1,500 / Day", Location = "Bogra Sadar, Bogra" },
                new() { Id = 97, FarmerName = "Shafiq Islam", EquipmentName = "TAFE 45DI Rotavator", Status = "Accepted",
                        DateRange = "28 Aug – 29 Aug 2026", StartDate = new(2026, 8, 28), EndDate = new(2026, 8, 29),
                        RequestedOn = DateTime.Now.AddDays(-2),
                        EquipmentCategory = "Tiller", DailyRate = "৳950 / Day", Location = "Bogra Sadar, Bogra" },

                // Rejected
                new() { Id = 91, FarmerName = "Jahanara Khatun", EquipmentName = "Kubota DC-70 Combine Harvester", Status = "Rejected",
                        DateRange = "20 Aug – 25 Aug 2026", StartDate = new(2026, 8, 20), EndDate = new(2026, 8, 25),
                        RequestedOn = DateTime.Now.AddDays(-9), Note = "Harvest window for early Aman.",
                        RejectReason = "Harvester is under scheduled maintenance that week.",
                        EquipmentCategory = "Harvester", DailyRate = "৳4,200 / Day", Location = "Bogra Sadar, Bogra" },

                // Completed
                new() { Id = 85, FarmerName = "Motaleb Hossain", EquipmentName = "Honda WB30X Irrigation Pump", Status = "Completed",
                        DateRange = "01 Aug – 10 Aug 2026", StartDate = new(2026, 8, 1), EndDate = new(2026, 8, 10),
                        RequestedOn = DateTime.Now.AddDays(-30), Note = "Boro seedbed irrigation.",
                        EquipmentCategory = "Irrigation", DailyRate = "৳350 / Day", Location = "Bogra Sadar, Bogra" },
                new() { Id = 82, FarmerName = "Rahim Uddin", EquipmentName = "ACI Power Tiller 12HP", Status = "Completed",
                        DateRange = "15 Jul – 18 Jul 2026", StartDate = new(2026, 7, 15), EndDate = new(2026, 7, 18),
                        RequestedOn = DateTime.Now.AddDays(-45),
                        EquipmentCategory = "Tiller", DailyRate = "৳800 / Day", Location = "Bogra Sadar, Bogra" }
            };

            // Flag pending requests whose dates overlap an accepted rental of the same equipment
            foreach (var pending in requests.Where(r => r.Status == "Pending"))
            {
                var clash = requests.FirstOrDefault(a => a.Status == "Accepted"
                    && a.EquipmentName == pending.EquipmentName
                    && pending.StartDate <= a.EndDate && a.StartDate <= pending.EndDate);
                if (clash != null)
                {
                    pending.HasConflict = true;
                    pending.ConflictHint = $"Dates overlap with an accepted rental ({clash.DateRange})";
                }
            }

            var model = new RentalRequestsViewModel
            {
                Requests = requests.OrderByDescending(r => r.RequestedOn).ToList()
            };

            return View(model);
        }

        /// <summary>
        /// POST: /EquipmentOwner/RespondRequest
        /// Handles Accept/Reject/Undo for a rental request.
        /// An optional reject reason is forwarded to the farmer.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RespondRequest(int id, string decision, string? reason = null)
        {
            var verb = decision?.ToLowerInvariant() switch
            {
                "accept" => "accepted",
                "reject" => "rejected",
                "complete" => "marked as completed",
                "undo" => "restored",
                _ => "updated"
            };
            return Json(new { success = true, message = $"Request #{id} {verb}." });
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
