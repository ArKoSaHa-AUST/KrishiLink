using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    public class FarmerController : Controller
    {
        /// <summary>
        /// GET: /Farmer or /Farmer/Index — redirects to Dashboard.
        /// </summary>
        public IActionResult Index()
        {
            return RedirectToAction(nameof(Dashboard));
        }

        /// <summary>
        /// GET: /Farmer/Dashboard — Farmer landing page after login.
        /// Populated with realistic dummy data until DB/EF Core is wired up.
        /// </summary>
        public IActionResult Dashboard()
        {
            var viewModel = new FarmerDashboardViewModel
            {
                FarmerName = "Rahim Uddin",

                ActiveBookings = new List<BookingSummaryItem>
                {
                    new BookingSummaryItem
                    {
                        Id = 1,
                        ItemName = "Mahindra 575 DI Tractor",
                        BookingType = "Equipment",
                        Location = "Bogra Sadar, Bogra",
                        DateRange = "25 Aug – 30 Aug 2026",
                        Status = "Accepted",
                        DetailUrl = Url.Action("Index", "Bookings")
                    },
                    new BookingSummaryItem
                    {
                        Id = 2,
                        ItemName = "Green Grain Cold Storage",
                        BookingType = "Godown",
                        Location = "Dinajpur Sadar",
                        DateRange = "01 Sep – 30 Nov 2026",
                        Status = "Pending",
                        DetailUrl = Url.Action("Index", "Bookings")
                    },
                    new BookingSummaryItem
                    {
                        Id = 3,
                        ItemName = "Power Tiller (Diesel)",
                        BookingType = "Equipment",
                        Location = "Rangpur Sadar",
                        DateRange = "10 Aug – 15 Aug 2026",
                        Status = "Completed",
                        DetailUrl = Url.Action("Index", "Bookings")
                    },
                    new BookingSummaryItem
                    {
                        Id = 4,
                        ItemName = "Aman Season Warehouse",
                        BookingType = "Godown",
                        Location = "Mymensingh",
                        DateRange = "05 Jul – 10 Jul 2026",
                        Status = "Rejected",
                        DetailUrl = Url.Action("Index", "Bookings")
                    }
                },

                SavedRecommendation = new CropRecommendation
                {
                    CropName = "Boro Rice (BRRI Dhan-89)",
                    Season = "Rabi Season — Nov to May",
                    Summary = "High-yield variety suitable for irrigated lowland areas in Northern Bangladesh. Recommended for clay-loam soils with consistent water supply. Expected yield: 6–7 tonnes/hectare.",
                    GuideUrl = Url.Action("Index", "Advisory")
                },

                RecentActivity = new List<ActivityFeedItem>
                {
                    new ActivityFeedItem
                    {
                        Description = "Equipment request for Mahindra 575 DI Tractor was accepted by the owner.",
                        IconClass = "bi-check-circle-fill",
                        IconColor = "text-success",
                        TimeAgo = "2 hours ago"
                    },
                    new ActivityFeedItem
                    {
                        Description = "Godown booking request sent for Green Grain Cold Storage, Dinajpur.",
                        IconClass = "bi-building",
                        IconColor = "text-warning",
                        TimeAgo = "5 hours ago"
                    },
                    new ActivityFeedItem
                    {
                        Description = "Crop advisory recommendation saved: Boro Rice (BRRI Dhan-89).",
                        IconClass = "bi-sun-fill",
                        IconColor = "text-success",
                        TimeAgo = "1 day ago"
                    },
                    new ActivityFeedItem
                    {
                        Description = "Power Tiller rental completed. Please leave a review.",
                        IconClass = "bi-tools",
                        IconColor = "text-secondary",
                        TimeAgo = "3 days ago"
                    },
                    new ActivityFeedItem
                    {
                        Description = "Godown booking for Aman Season Warehouse was rejected by the owner.",
                        IconClass = "bi-x-circle-fill",
                        IconColor = "text-danger",
                        TimeAgo = "5 days ago"
                    }
                }
            };

            return View(viewModel);
        }
    }
}
