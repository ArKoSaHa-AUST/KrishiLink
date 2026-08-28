using System;
using System.Collections.Generic;
using System.Linq;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    public class BookingsController : Controller
    {
        private static readonly List<BookingHistoryItemViewModel> MasterBookings = new()
        {
            new BookingHistoryItemViewModel
            {
                Id = 1,
                BookingCode = "KL-EQ-2026-101",
                ItemName = "Mahindra 575 DI Heavy Tractor",
                BookingType = "Equipment",
                Category = "Tractor",
                ImageUrl = "https://images.unsplash.com/photo-1592878904946-b3cd8ae243d0?auto=format&fit=crop&w=800&q=80",
                Location = "Bogra Sadar, Bogra",
                StartDate = DateTime.Today.AddDays(-4),
                EndDate = DateTime.Today.AddDays(1),
                TotalCost = 7500,
                RateDescription = "৳1,500 / day × 5 days",
                QuantityDisplay = "1 Heavy Tractor",
                Status = "Accepted",
                PaymentStatus = "Pending on Delivery",
                RequestedAt = DateTime.Today.AddDays(-7).AddHours(10),
                OwnerName = "Abdul Karim",
                OwnerPhone = "+880 1712-345678",
                FarmerNotes = "Need standard disc plough attachment for deep tilling before Aman transplantation.",
                OwnerRemarks = "Request accepted. Tractor will be fueled and delivered to your farm field by 8:00 AM.",
                ListingDetailUrl = "/Equipment/Details/1",
                Timeline = new List<BookingTimelineStep>
                {
                    new() { Title = "Rental Requested", Description = "Request submitted by Rahim Uddin", DateDisplay = "22 Aug 2026, 10:30 AM", IsCompleted = true, State = "done" },
                    new() { Title = "Owner Accepted", Description = "Accepted by Abdul Karim", DateDisplay = "22 Aug 2026, 02:15 PM", IsCompleted = true, State = "done" },
                    new() { Title = "Active in Field", Description = "Equipment currently in use", DateDisplay = "25 Aug – 30 Aug 2026", IsCompleted = false, IsCurrent = true, State = "active" },
                    new() { Title = "Completed & Handover", Description = "Return inspection and final payment", DateDisplay = "Expected 30 Aug 2026", IsCompleted = false, State = "pending" }
                }
            },
            new BookingHistoryItemViewModel
            {
                Id = 2,
                BookingCode = "KL-GD-2026-204",
                ItemName = "Green Grain Cold Storage Facility",
                BookingType = "Godown",
                Category = "Cold Storage (Vegetables)",
                ImageUrl = "https://images.unsplash.com/photo-1586528116311-ad8dd3c8310d?auto=format&fit=crop&w=800&q=80",
                Location = "Dinajpur Sadar, Dinajpur",
                StartDate = DateTime.Today.AddDays(3),
                EndDate = DateTime.Today.AddMonths(3),
                TotalCost = 36000,
                RateDescription = "৳1,200 / ton / mo × 10 Tons × 3 Months",
                QuantityDisplay = "10 Tons Capacity",
                Status = "Pending",
                PaymentStatus = "Unpaid (Awaiting Confirmation)",
                RequestedAt = DateTime.Today.AddDays(-1).AddHours(16),
                OwnerName = "Abdul Mannan",
                OwnerPhone = "+880 1711-987654",
                FarmerNotes = "10 Tons of potato bags. Climate-controlled room (2-8°C) needed for 3 months.",
                OwnerRemarks = "Reviewing chamber allocation.",
                ListingDetailUrl = "/Godown/Details/1",
                Timeline = new List<BookingTimelineStep>
                {
                    new() { Title = "Booking Requested", Description = "Request for 10 Tons submitted", DateDisplay = "27 Aug 2026, 04:20 PM", IsCompleted = true, State = "done" },
                    new() { Title = "Owner Review", Description = "Abdul Mannan checking chamber availability", DateDisplay = "In Progress", IsCompleted = false, IsCurrent = true, State = "active" },
                    new() { Title = "Accepted & Reserved", Description = "Deposit & bay allocation confirmation", DateDisplay = "Pending confirmation", IsCompleted = false, State = "pending" },
                    new() { Title = "Storage Stored", Description = "Produce unloaded into facility", DateDisplay = "Scheduled 01 Sep 2026", IsCompleted = false, State = "pending" }
                }
            },
            new BookingHistoryItemViewModel
            {
                Id = 3,
                BookingCode = "KL-EQ-2026-088",
                ItemName = "Sifang 12HP Diesel Power Tiller",
                BookingType = "Equipment",
                Category = "Power Tiller",
                ImageUrl = "https://images.unsplash.com/photo-1530267981375-f0de937f5f13?auto=format&fit=crop&w=800&q=80",
                Location = "Rangpur Sadar, Rangpur",
                StartDate = DateTime.Today.AddDays(-18),
                EndDate = DateTime.Today.AddDays(-13),
                TotalCost = 4500,
                RateDescription = "৳900 / day × 5 days",
                QuantityDisplay = "1 Power Tiller",
                Status = "Completed",
                PaymentStatus = "Paid in Full (Bkash)",
                RequestedAt = DateTime.Today.AddDays(-20),
                OwnerName = "Mokbul Hossain",
                OwnerPhone = "+880 1718-445566",
                FarmerNotes = "Tilling 3 bighas of Aman nursery bed.",
                OwnerRemarks = "Tiller returned clean and on time. Excellent renter.",
                ListingDetailUrl = "/Equipment/Details/3",
                Timeline = new List<BookingTimelineStep>
                {
                    new() { Title = "Rental Requested", Description = "Request submitted", DateDisplay = "08 Aug 2026", IsCompleted = true, State = "done" },
                    new() { Title = "Owner Accepted", Description = "Accepted by Mokbul Hossain", DateDisplay = "08 Aug 2026", IsCompleted = true, State = "done" },
                    new() { Title = "Active in Field", Description = "Tilling operation executed", DateDisplay = "10 Aug – 15 Aug 2026", IsCompleted = true, State = "done" },
                    new() { Title = "Rental Completed", Description = "Payment settled & return signed off", DateDisplay = "15 Aug 2026, 06:00 PM", IsCompleted = true, State = "done" }
                }
            },
            new BookingHistoryItemViewModel
            {
                Id = 4,
                BookingCode = "KL-GD-2026-042",
                ItemName = "Aman Season Warehouse",
                BookingType = "Godown",
                Category = "Grain Warehouse",
                ImageUrl = "https://images.unsplash.com/photo-1595246140625-573b715d11dc?auto=format&fit=crop&w=800&q=80",
                Location = "Mymensingh Sadar, Mymensingh",
                StartDate = DateTime.Today.AddDays(-54),
                EndDate = DateTime.Today.AddDays(-49),
                TotalCost = 6500,
                RateDescription = "৳650 / ton × 10 Tons",
                QuantityDisplay = "10 Tons Grain",
                Status = "Rejected",
                PaymentStatus = "No Charge",
                RequestedAt = DateTime.Today.AddDays(-56),
                OwnerName = "Haji Nurul Islam",
                OwnerPhone = "+880 1715-112233",
                FarmerNotes = "Urgent paddy storage for sudden rain forecast.",
                OwnerRemarks = "Sorry, facility was at 100% capacity due to peak seasonal harvest intake.",
                ListingDetailUrl = "/Godown/Details/2",
                Timeline = new List<BookingTimelineStep>
                {
                    new() { Title = "Booking Requested", Description = "Submitted for 10 Tons", DateDisplay = "03 Jul 2026", IsCompleted = true, State = "done" },
                    new() { Title = "Request Declined", Description = "Warehouse fully booked during peak harvest", DateDisplay = "04 Jul 2026", IsCompleted = true, State = "rejected" }
                }
            },
            new BookingHistoryItemViewModel
            {
                Id = 5,
                BookingCode = "KL-EQ-2026-115",
                ItemName = "Kubota DC-68G Combine Harvester",
                BookingType = "Equipment",
                Category = "Combine Harvester",
                ImageUrl = "https://images.unsplash.com/photo-1589923188900-85dae523342b?auto=format&fit=crop&w=800&q=80",
                Location = "Dinajpur Sadar, Dinajpur",
                StartDate = DateTime.Today.AddDays(7),
                EndDate = DateTime.Today.AddDays(10),
                TotalCost = 11400,
                RateDescription = "৳3,800 / day × 3 days",
                QuantityDisplay = "1 Combine Harvester",
                Status = "Accepted",
                PaymentStatus = "Pending on Arrival",
                RequestedAt = DateTime.Today.AddDays(-2),
                OwnerName = "Hafizur Rahman",
                OwnerPhone = "+880 1714-334455",
                FarmerNotes = "Fast harvesting needed across 12 bighas paddy land.",
                OwnerRemarks = "Accepted. Trained operator included in daily price.",
                ListingDetailUrl = "/Equipment/Details/2",
                Timeline = new List<BookingTimelineStep>
                {
                    new() { Title = "Rental Requested", Description = "Harvester request sent", DateDisplay = "26 Aug 2026", IsCompleted = true, State = "done" },
                    new() { Title = "Owner Accepted", Description = "Accepted with operator", DateDisplay = "26 Aug 2026", IsCompleted = true, State = "done" },
                    new() { Title = "Upcoming Service", Description = "Deployment scheduled", DateDisplay = "05 Sep – 08 Sep 2026", IsCompleted = false, State = "pending" },
                    new() { Title = "Completion", Description = "Harvest completion & signoff", DateDisplay = "Expected 08 Sep 2026", IsCompleted = false, State = "pending" }
                }
            }
        };

        /// <summary>
        /// GET: /Bookings
        /// </summary>
        public IActionResult Index(
            string tab = "all",
            string status = "all",
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string? searchTerm = null)
        {
            var query = MasterBookings.AsEnumerable();

            // 1. Type Tabs Filter
            tab = string.IsNullOrWhiteSpace(tab) ? "all" : tab.ToLowerInvariant();
            if (tab == "equipment")
            {
                query = query.Where(b => b.BookingType.Equals("Equipment", StringComparison.OrdinalIgnoreCase));
            }
            else if (tab == "godown")
            {
                query = query.Where(b => b.BookingType.Equals("Godown", StringComparison.OrdinalIgnoreCase));
            }

            // 2. Status Filter
            status = string.IsNullOrWhiteSpace(status) ? "all" : status.ToLowerInvariant();
            if (status != "all")
            {
                query = query.Where(b => b.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            }

            // 3. Search Query
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLowerInvariant();
                query = query.Where(b =>
                    b.ItemName.ToLowerInvariant().Contains(term) ||
                    b.BookingCode.ToLowerInvariant().Contains(term) ||
                    b.Location.ToLowerInvariant().Contains(term) ||
                    b.OwnerName.ToLowerInvariant().Contains(term));
            }

            // 4. Date Range
            if (dateFrom.HasValue)
            {
                query = query.Where(b => b.StartDate >= dateFrom.Value);
            }
            if (dateTo.HasValue)
            {
                query = query.Where(b => b.EndDate <= dateTo.Value);
            }

            var list = query.OrderByDescending(b => b.RequestedAt).ToList();

            var model = new BookingHistoryViewModel
            {
                ActiveTab = tab,
                StatusFilter = status,
                DateFrom = dateFrom,
                DateTo = dateTo,
                SearchTerm = searchTerm,
                Bookings = list,

                TotalAllCount = MasterBookings.Count,
                EquipmentCount = MasterBookings.Count(b => b.BookingType == "Equipment"),
                GodownCount = MasterBookings.Count(b => b.BookingType == "Godown"),
                PendingCount = MasterBookings.Count(b => b.Status == "Pending"),
                AcceptedCount = MasterBookings.Count(b => b.Status == "Accepted"),
                CompletedCount = MasterBookings.Count(b => b.Status == "Completed"),
                RejectedCount = MasterBookings.Count(b => b.Status == "Rejected")
            };

            return View(model);
        }
    }
}
