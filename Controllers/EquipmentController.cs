using System;
using System.Collections.Generic;
using System.Linq;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    public class EquipmentController : Controller
    {
        // Master list of available equipment items
        private static readonly List<EquipmentItemViewModel> SampleEquipment = new()
        {
            new EquipmentItemViewModel
            {
                Id = 1,
                Name = "Mahindra 575 DI Heavy Tractor",
                Category = "Tractor",
                DailyRate = 1500,
                HourlyRate = 250,
                Location = "Bogra Sadar, Bogra",
                DistanceKm = 3.2,
                IsAvailable = true,
                ImageUrl = "https://images.unsplash.com/photo-1592878904946-b3cd8ae243d0?auto=format&fit=crop&w=800&q=80",
                OwnerName = "Abdul Karim",
                Rating = 4.9,
                ReviewCount = 32,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new EquipmentItemViewModel
            {
                Id = 2,
                Name = "Kubota DC-68G Combine Harvester",
                Category = "Combine Harvester",
                DailyRate = 3800,
                HourlyRate = 650,
                Location = "Dinajpur Sadar, Dinajpur",
                DistanceKm = 12.5,
                IsAvailable = true,
                ImageUrl = "https://images.unsplash.com/photo-1589923188900-85dae523342b?auto=format&fit=crop&w=800&q=80",
                OwnerName = "Hafizur Rahman",
                Rating = 4.8,
                ReviewCount = 24,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new EquipmentItemViewModel
            {
                Id = 3,
                Name = "Sifang 12HP Diesel Power Tiller",
                Category = "Power Tiller",
                DailyRate = 900,
                HourlyRate = 150,
                Location = "Rangpur Sadar, Rangpur",
                DistanceKm = 5.8,
                IsAvailable = true,
                ImageUrl = "https://images.unsplash.com/photo-1530267981375-f0de937f5f13?auto=format&fit=crop&w=800&q=80",
                OwnerName = "Mokbul Hossain",
                Rating = 4.7,
                ReviewCount = 19,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new EquipmentItemViewModel
            {
                Id = 4,
                Name = "Automated Paddy Seed Drill / Seeder",
                Category = "Seed Drill / Seeder",
                DailyRate = 750,
                HourlyRate = 120,
                Location = "Sherpur, Bogra",
                DistanceKm = 8.4,
                IsAvailable = true,
                ImageUrl = "https://images.unsplash.com/photo-1563514227147-6d2ff665a6a0?auto=format&fit=crop&w=800&q=80",
                OwnerName = "Tariqul Islam",
                Rating = 4.6,
                ReviewCount = 15,
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            },
            new EquipmentItemViewModel
            {
                Id = 5,
                Name = "High-Pressure Knapsack Power Sprayer",
                Category = "Power Sprayer",
                DailyRate = 350,
                HourlyRate = 60,
                Location = "Mymensingh Sadar, Mymensingh",
                DistanceKm = 15.0,
                IsAvailable = true,
                ImageUrl = "https://images.unsplash.com/photo-1595878715977-2e8f8df18ea8?auto=format&fit=crop&w=800&q=80",
                OwnerName = "Jasim Uddin",
                Rating = 4.9,
                ReviewCount = 28,
                CreatedAt = DateTime.UtcNow.AddDays(-7)
            },
            new EquipmentItemViewModel
            {
                Id = 6,
                Name = "Tafe 45 DI 4WD Agricultural Tractor",
                Category = "Tractor",
                DailyRate = 1800,
                HourlyRate = 300,
                Location = "Comilla Sadar, Comilla",
                DistanceKm = 18.2,
                IsAvailable = true,
                ImageUrl = "https://images.unsplash.com/photo-1500937386664-56d1dfef3854?auto=format&fit=crop&w=800&q=80",
                OwnerName = "Anisur Rahman",
                Rating = 4.9,
                ReviewCount = 40,
                CreatedAt = DateTime.UtcNow.AddDays(-4)
            },
            new EquipmentItemViewModel
            {
                Id = 7,
                Name = "Centrifugal Shallow Irrigation Pump (4-Inch)",
                Category = "Irrigation Pump",
                DailyRate = 600,
                HourlyRate = 90,
                Location = "Jessore Sadar, Jessore",
                DistanceKm = 22.0,
                IsAvailable = false,
                ImageUrl = "https://images.unsplash.com/photo-1628352081506-83c43123ed6d?auto=format&fit=crop&w=800&q=80",
                OwnerName = "Shahidul Alam",
                Rating = 4.5,
                ReviewCount = 11,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            },
            new EquipmentItemViewModel
            {
                Id = 8,
                Name = "Multi-Crop High Speed Thresher",
                Category = "Thresher",
                DailyRate = 1200,
                HourlyRate = 200,
                Location = "Natore Sadar, Natore",
                DistanceKm = 14.1,
                IsAvailable = true,
                ImageUrl = "https://images.unsplash.com/photo-1574943320219-553eb213f72d?auto=format&fit=crop&w=800&q=80",
                OwnerName = "Faruk Ahmed",
                Rating = 4.7,
                ReviewCount = 16,
                CreatedAt = DateTime.UtcNow.AddDays(-6)
            }
        };

        private List<EquipmentItemViewModel> FilterEquipmentList(
            string? searchTerm,
            List<string>? selectedCategories,
            string? location,
            decimal? selectedMaxPrice,
            DateTime? availabilityDate,
            string? sortBy)
        {
            IEnumerable<EquipmentItemViewModel> query = SampleEquipment;

            // 1. Search Query Filter (Name, Category, Location, Owner)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(e =>
                    e.Name.ToLower().Contains(term) ||
                    e.Category.ToLower().Contains(term) ||
                    e.Location.ToLower().Contains(term) ||
                    e.OwnerName.ToLower().Contains(term));
            }

            // 2. Category / Equipment Type Filter
            if (selectedCategories != null && selectedCategories.Any())
            {
                query = query.Where(e => selectedCategories.Any(c => c.Equals(e.Category, StringComparison.OrdinalIgnoreCase)));
            }

            // 3. Location Filter
            if (!string.IsNullOrWhiteSpace(location))
            {
                query = query.Where(e => e.Location.Contains(location, StringComparison.OrdinalIgnoreCase));
            }

            // 4. Max Price Filter
            if (selectedMaxPrice.HasValue)
            {
                query = query.Where(e => e.DailyRate <= selectedMaxPrice.Value);
            }

            // 5. Availability Date Filter
            if (availabilityDate.HasValue)
            {
                query = query.Where(e => e.IsAvailable);
            }

            // 6. Sorting
            var sort = string.IsNullOrWhiteSpace(sortBy) ? "newest" : sortBy.ToLower();
            query = sort switch
            {
                "price_asc" => query.OrderBy(e => e.DailyRate),
                "price_desc" => query.OrderByDescending(e => e.DailyRate),
                "distance" => query.OrderBy(e => e.DistanceKm),
                "newest" => query.OrderByDescending(e => e.CreatedAt),
                _ => query.OrderByDescending(e => e.CreatedAt)
            };

            return query.ToList();
        }

        /// <summary>
        /// GET: /Equipment/Index
        /// Equipment Browse & Search with server-side and live AJAX filtering support.
        /// </summary>
        public IActionResult Index(
            string? searchTerm,
            List<string>? selectedCategories,
            string? location,
            decimal? selectedMaxPrice,
            DateTime? availabilityDate,
            string sortBy = "newest")
        {
            var filtered = FilterEquipmentList(searchTerm, selectedCategories, location, selectedMaxPrice, availabilityDate, sortBy);

            var model = new EquipmentBrowseViewModel
            {
                SearchTerm = searchTerm,
                SelectedCategories = selectedCategories ?? new List<string>(),
                Location = location,
                SelectedMaxPrice = selectedMaxPrice ?? 5000,
                AvailabilityDate = availabilityDate,
                SortBy = string.IsNullOrWhiteSpace(sortBy) ? "newest" : sortBy,
                EquipmentList = filtered
            };

            return View(model);
        }

        /// <summary>
        /// GET: /Equipment/FilterData (AJAX JSON endpoint for instant live updates)
        /// </summary>
        [HttpGet]
        public IActionResult FilterData(
            string? searchTerm,
            [FromQuery] List<string>? selectedCategories,
            string? location,
            decimal? selectedMaxPrice,
            DateTime? availabilityDate,
            string sortBy = "newest")
        {
            var filtered = FilterEquipmentList(searchTerm, selectedCategories, location, selectedMaxPrice, availabilityDate, sortBy);

            var result = filtered.Select(e => new
            {
                id = e.Id,
                name = e.Name,
                category = e.Category,
                dailyRate = e.DailyRate,
                dailyRateFormatted = $"৳{e.DailyRate:N0}",
                hourlyRate = e.HourlyRate,
                hourlyRateFormatted = e.HourlyRate.HasValue ? $"৳{e.HourlyRate.Value:N0}" : null,
                location = e.Location,
                distanceKm = e.DistanceKm,
                distanceKmFormatted = $"{e.DistanceKm:0.0} km away",
                isAvailable = e.IsAvailable,
                status = e.Status,
                imageUrl = e.ImageUrl,
                ownerName = e.OwnerName,
                rating = e.Rating,
                reviewCount = e.ReviewCount,
                detailsUrl = Url.Action(nameof(Details), "Equipment", new { id = e.Id })
            });

            return Json(new
            {
                totalCount = filtered.Count,
                items = result
            });
        }

        /// <summary>
        /// GET: /Equipment/Details/1
        /// Equipment Details & Rental Request page.
        /// </summary>
        public IActionResult Details(int id = 1)
        {
            var item = SampleEquipment.FirstOrDefault(e => e.Id == id) ?? SampleEquipment.First();

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
                Id = item.Id,
                Name = item.Name,
                Category = item.Category,
                Description = $"High-performance {item.Name} well-maintained and ready for field operations in {item.Location}. Suitable for heavy tillage, harvesting, or crop protection with optimal fuel efficiency and professional handling.",
                DailyRate = $"৳{item.DailyRate:N0} / Day",
                HourlyRate = item.HourlyRate.HasValue ? $"৳{item.HourlyRate.Value:N0} / Hour" : "৳200 / Hour",
                Location = item.Location,
                Status = item.Status,

                OwnerName = item.OwnerName,
                OwnerRating = item.Rating,
                TotalReviews = item.ReviewCount,
                OwnerPhone = "+880 1712-345678",
                OwnerMemberSince = "March 2024",

                ImageUrls = new List<string>
                {
                    item.ImageUrl,
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
            TempData["SuccessMessage"] = "Rental request sent to owner! You will be notified once accepted.";
            return RedirectToAction(nameof(Details), new { id = model.Id, requestSent = true });
        }
    }
}
