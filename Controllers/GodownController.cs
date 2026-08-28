using System;
using System.Collections.Generic;
using System.Linq;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    public class GodownController : Controller
    {
        // Master list of available godowns and storage facilities
        private static readonly List<GodownItemViewModel> SampleGodowns = new()
        {
            new GodownItemViewModel
            {
                Id = 1,
                Name = "Green Grain Cold Storage Facility",
                StorageType = "Cold Storage (Vegetable & Fruit)",
                Location = "Dinajpur Sadar, Dinajpur",
                DistanceKm = 4.5,
                TotalCapacityTons = 300,
                AvailableCapacityTons = 120,
                PricePerTonPerMonth = 1200,
                ImageUrl = "https://images.unsplash.com/photo-1586528116311-ad8dd3c8310d?auto=format&fit=crop&w=800&q=80",
                OwnerName = "Abdul Mannan",
                Rating = 4.9,
                ReviewCount = 28,
                Facilities = new List<string> { "Climate Control (2-8°C)", "24/7 CCTV", "Backup Generator", "Moisture Proof" },
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new GodownItemViewModel
            {
                Id = 2,
                Name = "Bogra Central Aman Paddy Warehouse",
                StorageType = "Grain Warehouse (Paddy & Wheat)",
                Location = "Bogra Sadar, Bogra",
                DistanceKm = 2.8,
                TotalCapacityTons = 500,
                AvailableCapacityTons = 260,
                PricePerTonPerMonth = 650,
                ImageUrl = "https://images.unsplash.com/photo-1595246140625-573b715d11dc?auto=format&fit=crop&w=800&q=80",
                OwnerName = "Haji Nurul Islam",
                Rating = 4.8,
                ReviewCount = 35,
                Facilities = new List<string> { "Fumigated & Pest-Free", "Elevated Platform", "Easy Truck Loading", "Fire Safety" },
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new GodownItemViewModel
            {
                Id = 3,
                Name = "Sherpur Agro Multi-Chamber Depot",
                StorageType = "Multi-Chamber Cold Storage",
                Location = "Sherpur, Bogra",
                DistanceKm = 9.2,
                TotalCapacityTons = 200,
                AvailableCapacityTons = 45,
                PricePerTonPerMonth = 1450,
                ImageUrl = "https://images.unsplash.com/photo-1616401784845-180882ba9ba8?auto=format&fit=crop&w=800&q=80",
                OwnerName = "Tariqul Alam",
                Rating = 4.7,
                ReviewCount = 14,
                Facilities = new List<string> { "Humidity Control", "Separate Potato & Onion Chambers", "Solar Backup" },
                CreatedAt = DateTime.UtcNow.AddDays(-4)
            },
            new GodownItemViewModel
            {
                Id = 4,
                Name = "Rangpur Farmers Seed & Grain Storage",
                StorageType = "Seed & Fertilizer Godown",
                Location = "Rangpur Sadar, Rangpur",
                DistanceKm = 7.0,
                TotalCapacityTons = 150,
                AvailableCapacityTons = 80,
                PricePerTonPerMonth = 550,
                ImageUrl = "https://images.unsplash.com/photo-1578575437130-527eed3abbec?auto=format&fit=crop&w=800&q=80",
                OwnerName = "Mokbul Hossain",
                Rating = 4.6,
                ReviewCount = 19,
                Facilities = new List<string> { "Insulated Roofing", "Rodent-Proof", "Daily Inspection" },
                CreatedAt = DateTime.UtcNow.AddDays(-6)
            },
            new GodownItemViewModel
            {
                Id = 5,
                Name = "Mymensingh Regional Agricultural Silo",
                StorageType = "Grain Warehouse (Paddy & Wheat)",
                Location = "Mymensingh Sadar, Mymensingh",
                DistanceKm = 16.4,
                TotalCapacityTons = 400,
                AvailableCapacityTons = 190,
                PricePerTonPerMonth = 700,
                ImageUrl = "https://images.unsplash.com/photo-1553413077-190dd305871c?auto=format&fit=crop&w=800&q=80",
                OwnerName = "Kabir Ahmed",
                Rating = 4.9,
                ReviewCount = 42,
                Facilities = new List<string> { "Automated Aeration", "Direct Weighbridge", "Security Guard 24/7" },
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            },
            new GodownItemViewModel
            {
                Id = 6,
                Name = "Jessore Dry Crop & Jute Godown",
                StorageType = "Jute & Crop Storage",
                Location = "Jessore Sadar, Jessore",
                DistanceKm = 24.1,
                TotalCapacityTons = 250,
                AvailableCapacityTons = 0, // Booked
                PricePerTonPerMonth = 480,
                ImageUrl = "https://images.unsplash.com/photo-1587293852726-70cdb56c2866?auto=format&fit=crop&w=800&q=80",
                OwnerName = "Shahidul Islam",
                Rating = 4.5,
                ReviewCount = 11,
                Facilities = new List<string> { "Ventilated", "Spacious Loading Dock" },
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            },
            new GodownItemViewModel
            {
                Id = 7,
                Name = "Rajshahi Mango & Fruit Cold Store",
                StorageType = "Cold Storage (Vegetable & Fruit)",
                Location = "Rajshahi Sadar, Rajshahi",
                DistanceKm = 18.0,
                TotalCapacityTons = 180,
                AvailableCapacityTons = 65,
                PricePerTonPerMonth = 1600,
                ImageUrl = "https://images.unsplash.com/photo-1586528116493-a029325540fa?auto=format&fit=crop&w=800&q=80",
                OwnerName = "Fazle Rabbi",
                Rating = 4.9,
                ReviewCount = 31,
                Facilities = new List<string> { "Ethylene Control", "Nitrogen Purging", "Pre-Cooling Unit" },
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new GodownItemViewModel
            {
                Id = 8,
                Name = "Comilla Agri Logistics & Dry Warehouse",
                StorageType = "Dry Goods Warehouse",
                Location = "Comilla Sadar, Comilla",
                DistanceKm = 20.5,
                TotalCapacityTons = 350,
                AvailableCapacityTons = 140,
                PricePerTonPerMonth = 600,
                ImageUrl = "https://images.unsplash.com/photo-1565793298595-6a879b1d9492?auto=format&fit=crop&w=800&q=80",
                OwnerName = "Anisur Rahman",
                Rating = 4.7,
                ReviewCount = 20,
                Facilities = new List<string> { "Tarpaulin Covered", "Forklift Support", "Fire Hydrant" },
                CreatedAt = DateTime.UtcNow.AddDays(-7)
            }
        };

        private List<GodownItemViewModel> FilterGodownList(
            string? searchTerm,
            List<string>? selectedStorageTypes,
            string? location,
            double? selectedMinCapacity,
            decimal? selectedMaxPrice,
            DateTime? availableStartDate,
            DateTime? availableEndDate,
            string? sortBy)
        {
            IEnumerable<GodownItemViewModel> query = SampleGodowns;

            // 1. Search Query
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(g =>
                    g.Name.ToLower().Contains(term) ||
                    g.StorageType.ToLower().Contains(term) ||
                    g.Location.ToLower().Contains(term) ||
                    g.OwnerName.ToLower().Contains(term));
            }

            // 2. Storage Types Filter
            if (selectedStorageTypes != null && selectedStorageTypes.Any())
            {
                query = query.Where(g => selectedStorageTypes.Any(t => t.Equals(g.StorageType, StringComparison.OrdinalIgnoreCase)));
            }

            // 3. Location Filter
            if (!string.IsNullOrWhiteSpace(location))
            {
                query = query.Where(g => g.Location.Contains(location, StringComparison.OrdinalIgnoreCase));
            }

            // 4. Capacity Needed Filter
            if (selectedMinCapacity.HasValue && selectedMinCapacity.Value > 0)
            {
                query = query.Where(g => g.AvailableCapacityTons >= selectedMinCapacity.Value);
            }

            // 5. Max Monthly Price Filter
            if (selectedMaxPrice.HasValue)
            {
                query = query.Where(g => g.PricePerTonPerMonth <= selectedMaxPrice.Value);
            }

            // 6. Dates Filter
            if (availableStartDate.HasValue || availableEndDate.HasValue)
            {
                query = query.Where(g => g.IsAvailable);
            }

            // 7. Sorting
            var sort = string.IsNullOrWhiteSpace(sortBy) ? "newest" : sortBy.ToLower();
            query = sort switch
            {
                "price_asc" => query.OrderBy(g => g.PricePerTonPerMonth),
                "price_desc" => query.OrderByDescending(g => g.PricePerTonPerMonth),
                "distance" => query.OrderBy(g => g.DistanceKm),
                "capacity_desc" => query.OrderByDescending(g => g.AvailableCapacityTons),
                "newest" => query.OrderByDescending(g => g.CreatedAt),
                _ => query.OrderByDescending(g => g.CreatedAt)
            };

            return query.ToList();
        }

        /// <summary>
        /// GET: /Godown or /Godown/Index
        /// Browse Godowns and Warehouses with live and server-side filtering.
        /// </summary>
        public IActionResult Index(
            string? searchTerm,
            List<string>? selectedStorageTypes,
            string? location,
            double? selectedMinCapacity,
            decimal? selectedMaxPrice,
            DateTime? availableStartDate,
            DateTime? availableEndDate,
            string sortBy = "newest")
        {
            var filtered = FilterGodownList(searchTerm, selectedStorageTypes, location, selectedMinCapacity, selectedMaxPrice, availableStartDate, availableEndDate, sortBy);

            var model = new GodownBrowseViewModel
            {
                SearchTerm = searchTerm,
                SelectedStorageTypes = selectedStorageTypes ?? new List<string>(),
                Location = location,
                SelectedMinCapacity = selectedMinCapacity,
                SelectedMaxPrice = selectedMaxPrice ?? 2500,
                AvailableStartDate = availableStartDate,
                AvailableEndDate = availableEndDate,
                SortBy = string.IsNullOrWhiteSpace(sortBy) ? "newest" : sortBy,
                GodownList = filtered
            };

            return View(model);
        }

        /// <summary>
        /// GET: /Godown/FilterData (AJAX JSON endpoint)
        /// </summary>
        [HttpGet]
        public IActionResult FilterData(
            string? searchTerm,
            [FromQuery] List<string>? selectedStorageTypes,
            string? location,
            double? selectedMinCapacity,
            decimal? selectedMaxPrice,
            DateTime? availableStartDate,
            DateTime? availableEndDate,
            string sortBy = "newest")
        {
            var filtered = FilterGodownList(searchTerm, selectedStorageTypes, location, selectedMinCapacity, selectedMaxPrice, availableStartDate, availableEndDate, sortBy);

            var result = filtered.Select(g => new
            {
                id = g.Id,
                name = g.Name,
                storageType = g.StorageType,
                location = g.Location,
                distanceKm = g.DistanceKm,
                distanceKmFormatted = $"{g.DistanceKm:0.0} km away",
                totalCapacityTons = g.TotalCapacityTons,
                availableCapacityTons = g.AvailableCapacityTons,
                capacityDisplay = $"{g.AvailableCapacityTons:N0} / {g.TotalCapacityTons:N0} Tons available",
                pricePerTonPerMonth = g.PricePerTonPerMonth,
                pricePerTonPerMonthFormatted = $"৳{g.PricePerTonPerMonth:N0}",
                dailyRatePerTonFormatted = g.DailyRatePerTon.HasValue ? $"৳{g.DailyRatePerTon.Value:N0}" : null,
                isAvailable = g.IsAvailable,
                status = g.Status,
                imageUrl = g.ImageUrl,
                ownerName = g.OwnerName,
                rating = g.Rating,
                reviewCount = g.ReviewCount,
                facilities = g.Facilities,
                detailsUrl = Url.Action(nameof(Details), "Godown", new { id = g.Id })
            });

            return Json(new
            {
                totalCount = filtered.Count,
                items = result
            });
        }

        /// <summary>
        /// GET: /Godown/Details/1
        /// </summary>
        public IActionResult Details(int id = 1)
        {
            var godown = SampleGodowns.FirstOrDefault(g => g.Id == id) ?? SampleGodowns.First();

            var model = new GodownDetailViewModel
            {
                Id = godown.Id,
                Name = godown.Name,
                StorageType = godown.StorageType,
                Location = godown.Location,
                TotalCapacityTons = godown.TotalCapacityTons,
                AvailableCapacityTons = godown.AvailableCapacityTons,
                PricePerTonPerMonth = $"৳{godown.PricePerTonPerMonth:N0} / Ton / Month",
                DailyRatePerTon = $"৳{godown.DailyRatePerTon:N0} / Ton / Day",
                Status = godown.Status,
                Description = $"Spacious and well-maintained {godown.StorageType} in {godown.Location}. Equipped with modern agricultural preservation safeguards, pest management, and 24/7 security surveillance for storing grains, seeds, potatoes, and other harvest produce.",

                OwnerName = godown.OwnerName,
                OwnerRating = godown.Rating,
                TotalReviews = godown.ReviewCount,
                OwnerPhone = "+880 1712-889900",
                OwnerMemberSince = "January 2024",

                ImageUrls = new List<string>
                {
                    godown.ImageUrl,
                    "https://images.unsplash.com/photo-1586528116311-ad8dd3c8310d?auto=format&fit=crop&w=800&q=80",
                    "https://images.unsplash.com/photo-1595246140625-573b715d11dc?auto=format&fit=crop&w=800&q=80"
                },

                Facilities = godown.Facilities,
                StartDate = DateTime.Today.AddDays(1),
                EndDate = DateTime.Today.AddMonths(1),
                RequestedCapacityTons = Math.Min(10, godown.AvailableCapacityTons)
            };

            return View(model);
        }
    }
}
