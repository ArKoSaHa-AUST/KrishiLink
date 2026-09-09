using System.Security.Claims;
using System.Text;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    [Authorize(Roles = "GodownOwner")]
    public class GodownOwnerController : Controller
    {
        private readonly IGodownRevenueService _revenueService;
        private readonly UserManager<ApplicationUser> _userManager;

        public GodownOwnerController(IGodownRevenueService revenueService, UserManager<ApplicationUser> userManager)
        {
            _revenueService = revenueService;
            _userManager = userManager;
        }

        private string OwnerId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

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
                    .ToList(),
                ThisMonthRevenue = _revenueService.GetReport(OwnerId, new RevenueFilter()).ThisMonthRevenue
            };

            return View(model);
        }

        /// <summary>
        /// GET: /GodownOwner/Revenue
        /// Revenue dashboard: KPIs, settlement, trend, per-godown breakdown, funnel and transactions.
        /// </summary>
        [HttpGet]
        public IActionResult Revenue(RevenueFilter filter)
        {
            return View(_revenueService.GetReport(OwnerId, filter));
        }

        /// <summary>
        /// GET: /GodownOwner/RevenueCsv
        /// Downloads the filtered transaction list as UTF-8 (BOM) CSV so Excel renders ৳ correctly.
        /// </summary>
        [HttpGet]
        public IActionResult RevenueCsv(RevenueFilter filter)
        {
            var csv = _revenueService.ExportCsv(OwnerId, filter);
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
            return File(bytes, "text/csv; charset=utf-8", $"godown-revenue-{DateTime.Today:yyyy-MM-dd}.csv");
        }

        /// <summary>
        /// GET: /GodownOwner/Invoice/185
        /// Printable receipt for a completed booking (use the browser's "Save as PDF").
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Invoice(int id)
        {
            var model = _revenueService.GetInvoice(OwnerId, id);
            if (model is null) return NotFound();

            var owner = await _userManager.GetUserAsync(User);
            model.OwnerName = owner?.FullName ?? User.Identity?.Name ?? "Owner";
            model.OwnerBusiness = owner?.BusinessOrFarmName;
            model.OwnerLocation = owner?.Location;
            return View(model);
        }

        /// <summary>
        /// POST: /GodownOwner/AddExpense
        /// Records a cost against a booking so the revenue page can show net profit.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddExpense(int bookingId, decimal amount, string? note, string? returnUrl)
        {
            if (_revenueService.AddExpense(OwnerId, bookingId, amount, note))
                TempData["SuccessMessage"] = $"Expense of ৳{amount:N0} recorded against booking #{bookingId}.";
            else
                TempData["ErrorMessage"] = "Expense must be a positive amount on an accepted or completed booking.";

            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Revenue));
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

        [HttpGet]
        public IActionResult Create()
        {
            var model = new GodownListingViewModel
            {
                Name = string.Empty,
                Category = "Cold Storage",
                Location = "Dinajpur Sadar, Dinajpur",
                TotalCapacity = 200,
                CapacityUnit = "Tons",
                AvailableCapacity = 200,
                PriceAmount = 450,
                PricePeriod = "Month",
                Description = string.Empty,
                IsAvailable = true,
                SelectedFacilities = new List<string> { "Climate Control (2-8°C)", "24/7 Security & CCTV", "Power Backup Generator" },
                ExistingImageUrls = new List<string>()
            };

            return View(model);
        }

        /// <summary>
        /// GET: /GodownOwner/Edit/1
        /// </summary>
        [HttpGet]
        public IActionResult Edit(int id = 1)
        {
            var sampleGodowns = GetSampleGodowns();
            var godown = sampleGodowns.FirstOrDefault(g => g.Id == id) ?? sampleGodowns.First();

            var model = new GodownListingViewModel
            {
                Id = godown.Id,
                Name = godown.Name,
                Category = godown.StorageType,
                Location = "Dinajpur Sadar, Dinajpur",
                TotalCapacity = godown.TotalCapacityTons,
                CapacityUnit = "Tons",
                AvailableCapacity = godown.AvailableCapacityTons,
                PriceAmount = 450,
                PricePeriod = "Month",
                Description = "Modern temperature-controlled warehouse equipped with automated moisture monitors, pallet racking, 24/7 security guard patrol, and power backup for agricultural produce preservation.",
                IsAvailable = godown.Status == "Active",
                SelectedFacilities = new List<string>
                {
                    "Climate Control (2-8°C)",
                    "24/7 Security & CCTV",
                    "Power Backup Generator",
                    "Pest & Rodent Control",
                    "Loading & Unloading Ramp"
                },
                ExistingImageUrls = new List<string>
                {
                    "/images/godown.jpg"
                }
            };

            return View("Create", model);
        }

        /// <summary>
        /// POST: /GodownOwner/Save
        /// Handles creation or updating of godown storage listing.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Save(GodownListingViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Create", model);
            }

            var actionName = model.IsEditMode ? "updated" : "listed";
            TempData["SuccessMessage"] = $"Storage facility '{model.Name}' successfully {actionName}!";
            return RedirectToAction(nameof(Index));
        }
    }
}
