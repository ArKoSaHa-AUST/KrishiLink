using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    [Authorize(Roles = "GodownOwner")]
    public class GodownOwnerController : Controller
    {
        // Sample data shared by the dashboard and the booking requests page until DB wiring
        private static List<OwnerGodownItem> GetSampleGodowns() => new()
        {
            new() { Id = 1, Name = "Green Grain Cold Storage Facility", StorageType = "Cold Storage",
                    TotalCapacityTons = 300, AvailableCapacityTons = 180, Status = "Active" },
            new() { Id = 2, Name = "Dinajpur AgriHub Warehouse", StorageType = "Dry Warehouse",
                    TotalCapacityTons = 500, AvailableCapacityTons = 90, Status = "Active" },
            new() { Id = 3, Name = "Riverside Seed Vault", StorageType = "Seed Storage",
                    TotalCapacityTons = 120, AvailableCapacityTons = 0, Status = "Full" }
        };

        private static List<GodownBookingRequestItem> GetSampleRequests() => new()
        {
            // Pending (mirrors the dashboard widget)
            new() { Id = 201, FarmerName = "Rahim Uddin", GodownId = 1, GodownName = "Green Grain Cold Storage Facility", Status = "Pending",
                    RequestedCapacityTons = 25, DateRange = "02 Sep – 30 Nov 2026",
                    RequestedOn = DateTime.Now.AddHours(-3),
                    Note = "BRRI-28 paddy bags, moisture tested." },
            new() { Id = 202, FarmerName = "Salma Akter", GodownId = 2, GodownName = "Dinajpur AgriHub Warehouse", Status = "Pending",
                    RequestedCapacityTons = 40, DateRange = "05 Sep – 05 Dec 2026",
                    RequestedOn = DateTime.Now.AddHours(-9) },
            new() { Id = 203, FarmerName = "Motaleb Hossain", GodownId = 1, GodownName = "Green Grain Cold Storage Facility", Status = "Pending",
                    RequestedCapacityTons = 200, DateRange = "10 Sep – 10 Oct 2026",
                    RequestedOn = DateTime.Now.AddHours(-30),
                    Note = "Potato harvest, needs 2-8°C climate control." },

            // Accepted
            new() { Id = 196, FarmerName = "Abdul Halim", GodownId = 2, GodownName = "Dinajpur AgriHub Warehouse", Status = "Accepted",
                    RequestedCapacityTons = 120, DateRange = "20 Aug – 20 Nov 2026",
                    RequestedOn = DateTime.Now.AddDays(-4), Note = "Wheat storage before milling." },
            new() { Id = 197, FarmerName = "Shafiq Islam", GodownId = 1, GodownName = "Green Grain Cold Storage Facility", Status = "Accepted",
                    RequestedCapacityTons = 60, DateRange = "25 Aug – 25 Oct 2026",
                    RequestedOn = DateTime.Now.AddDays(-3) },

            // Rejected
            new() { Id = 191, FarmerName = "Jahanara Khatun", GodownId = 3, GodownName = "Riverside Seed Vault", Status = "Rejected",
                    RequestedCapacityTons = 30, DateRange = "18 Aug – 18 Sep 2026",
                    RequestedOn = DateTime.Now.AddDays(-10), Note = "Certified seed paddy for next season.",
                    RejectReason = "Seed vault is fully booked until December." },

            // Completed
            new() { Id = 185, FarmerName = "Motaleb Hossain", GodownId = 2, GodownName = "Dinajpur AgriHub Warehouse", Status = "Completed",
                    RequestedCapacityTons = 80, DateRange = "01 May – 01 Aug 2026",
                    RequestedOn = DateTime.Now.AddDays(-120), Note = "Boro season paddy." },
            new() { Id = 182, FarmerName = "Rahim Uddin", GodownId = 1, GodownName = "Green Grain Cold Storage Facility", Status = "Completed",
                    RequestedCapacityTons = 45, DateRange = "10 Apr – 10 Jul 2026",
                    RequestedOn = DateTime.Now.AddDays(-140) }
        };

        public IActionResult Index()
        {
            var godowns = GetSampleGodowns();

            var model = new GodownOwnerDashboardViewModel
            {
                OwnerName = User.Identity?.Name ?? "Owner",
                TotalGodowns = godowns.Count,
                TotalCapacityTons = godowns.Sum(g => g.TotalCapacityTons),
                OccupiedCapacityTons = godowns.Sum(g => g.OccupiedTons),
                Godowns = godowns,
                PendingRequestItems = GetSampleRequests()
                    .Where(r => r.Status == "Pending")
                    .OrderByDescending(r => r.RequestedOn)
                    .ToList()
            };

            return View(model);
        }

        /// <summary>
        /// GET: /GodownOwner/Requests
        /// Full list of storage booking requests with filter tabs and Accept/Reject.
        /// </summary>
        [HttpGet]
        public IActionResult Requests()
        {
            var model = new GodownBookingRequestsViewModel
            {
                Requests = GetSampleRequests().OrderByDescending(r => r.RequestedOn).ToList(),
                Godowns = GetSampleGodowns()
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
                "complete" => "marked as completed",
                "undo" => "restored",
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
    }
}
