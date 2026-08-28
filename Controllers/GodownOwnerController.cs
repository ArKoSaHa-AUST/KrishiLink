using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    [Authorize(Roles = "GodownOwner")]
    public class GodownOwnerController : Controller
    {
        public IActionResult Index()
        {
            var godowns = new List<OwnerGodownItem>
            {
                new() { Id = 1, Name = "Green Grain Cold Storage Facility", StorageType = "Cold Storage",
                        TotalCapacityTons = 300, AvailableCapacityTons = 180, Status = "Active" },
                new() { Id = 2, Name = "Dinajpur AgriHub Warehouse", StorageType = "Dry Warehouse",
                        TotalCapacityTons = 500, AvailableCapacityTons = 90, Status = "Active" },
                new() { Id = 3, Name = "Riverside Seed Vault", StorageType = "Seed Storage",
                        TotalCapacityTons = 120, AvailableCapacityTons = 0, Status = "Full" }
            };

            var model = new GodownOwnerDashboardViewModel
            {
                OwnerName = User.Identity?.Name ?? "Owner",
                TotalGodowns = godowns.Count,
                TotalCapacityTons = godowns.Sum(g => g.TotalCapacityTons),
                OccupiedCapacityTons = godowns.Sum(g => g.OccupiedTons),
                Godowns = godowns,
                PendingRequestItems = new List<GodownBookingRequestItem>
                {
                    new() { Id = 201, FarmerName = "Rahim Uddin", GodownId = 1, GodownName = "Green Grain Cold Storage Facility",
                            RequestedCapacityTons = 25, DateRange = "02 Sep – 30 Nov 2026",
                            RequestedOn = DateTime.Now.AddHours(-3),
                            Note = "BRRI-28 paddy bags, moisture tested." },
                    new() { Id = 202, FarmerName = "Salma Akter", GodownId = 2, GodownName = "Dinajpur AgriHub Warehouse",
                            RequestedCapacityTons = 40, DateRange = "05 Sep – 05 Dec 2026",
                            RequestedOn = DateTime.Now.AddHours(-9) },
                    new() { Id = 203, FarmerName = "Motaleb Hossain", GodownId = 1, GodownName = "Green Grain Cold Storage Facility",
                            RequestedCapacityTons = 200, DateRange = "10 Sep – 10 Oct 2026",
                            RequestedOn = DateTime.Now.AddHours(-30),
                            Note = "Potato harvest, needs 2-8°C climate control." }
                }.OrderByDescending(r => r.RequestedOn).ToList()
            };

            return View(model);
        }

        /// <summary>
        /// POST: /GodownOwner/RespondRequest
        /// Handles Accept/Reject/Undo for a storage booking request.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RespondRequest(int id, string decision, string? reason = null)
        {
            var verb = decision?.ToLowerInvariant() switch
            {
                "accept" => "accepted",
                "reject" => "rejected",
                "undo" => "restored to pending",
                _ => "updated"
            };
            return Json(new { success = true, message = $"Booking request #{id} {verb}." });
        }

        /// <summary>
        /// GET: /GodownOwner/PendingCount
        /// Lightweight polling endpoint for new-booking-request notifications.
        /// Returns sample data until DB wiring.
        /// </summary>
        [HttpGet]
        public IActionResult PendingCount()
        {
            return Json(new { count = 3 });
        }

        public IActionResult Create()
        {
            return View();
        }

        /// <summary>
        /// GET: /GodownOwner/Edit/1 — placeholder until the listing form (part 17) is built.
        /// </summary>
        public IActionResult Edit(int id = 1)
        {
            return View("Create");
        }

        public IActionResult Requests()
        {
            return View();
        }
    }
}
