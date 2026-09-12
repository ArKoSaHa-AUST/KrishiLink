using KrishiLink.BLL.Services;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    public class AdvisoryController : Controller
    {
        private readonly ICropCalendarService _cropCalendarService;
        private readonly IPestAlertService _pestAlertService;

        public AdvisoryController(
            ICropCalendarService cropCalendarService,
            IPestAlertService pestAlertService)
        {
            _cropCalendarService = cropCalendarService;
            _pestAlertService = pestAlertService;
        }

        /// <summary>
        /// GET: /Advisory
        /// Renders the Crop Advisory form and recommendations.
        /// </summary>
        public async Task<IActionResult> Index(bool analyze = false)
        {
            var model = new CropAdvisoryViewModel();

            if (analyze)
            {
                PopulateSampleRecommendations(model);
                model.WeatherAlert = await _pestAlertService.GetWeatherAlertNoteAsync(model.Location);
                model.HasSubmitted = true;
            }

            return View(model);
        }

        /// <summary>
        /// GET: /Advisory/Calendar
        /// Interactive Crop Planting & Harvesting Calendar reference across Bangladesh's agricultural zones.
        /// </summary>
        public async Task<IActionResult> Calendar(
            string? search = null,
            string? category = null,
            string? season = null,
            string? division = null,
            int? month = null,
            string? stage = null)
        {
            var model = await _cropCalendarService.GetCalendarModelAsync(search, category, season, division, month, stage);
            return View(model);
        }

        /// <summary>
        /// GET: /Advisory/Alerts
        /// Rule-based Agrometeorological Pest and Disease Warnings based on regional weather data.
        /// </summary>
        public async Task<IActionResult> Alerts(
            string? district = null,
            string? crop = null,
            double? temp = null,
            double? humidity = null,
            string? condition = null)
        {
            var safeDistrict = SanitizeDistrict(district);
            var safeCrop = SanitizeCrop(crop);
            var safeCondition = SanitizeCondition(condition);
            double? safeTemp = temp.HasValue ? Math.Clamp(temp.Value, -10.0, 60.0) : null;
            double? safeHumidity = humidity.HasValue ? Math.Clamp(humidity.Value, 0.0, 100.0) : null;

            var model = await _pestAlertService.GetPestAlertsDashboardAsync(safeDistrict, safeCrop, safeTemp, safeHumidity, safeCondition);
            return View(model);
        }

        /// <summary>
        /// GET: /Advisory/WeatherAlertsJson
        /// JSON endpoint returning live weather and triggered disease alerts for a district.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> WeatherAlertsJson(string? district)
        {
            var safeDistrict = SanitizeDistrict(district);
            var weather = await _pestAlertService.GetRegionalWeatherAsync(safeDistrict);
            var alerts = await _pestAlertService.EvaluateAlertsAsync(weather);
            return Json(new { success = true, weather, alerts });
        }

        /// <summary>
        /// GET: /Advisory/CropDetail/{id}
        /// Returns detailed crop profile JSON for quick modal views or dynamic inspection.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CropDetail(int id)
        {
            var crop = await _cropCalendarService.GetCropByIdAsync(id);
            if (crop == null)
            {
                return NotFound(new { success = false, message = "Crop entry not found." });
            }

            return Json(new { success = true, data = crop });
        }

        /// <summary>
        /// POST: /Advisory
        /// Processes farmer's land inputs and generates rule-based recommendations.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(CropAdvisoryViewModel model)
        {
            PopulateSampleRecommendations(model);
            model.WeatherAlert = await _pestAlertService.GetWeatherAlertNoteAsync(model.Location);
            model.HasSubmitted = true;
            return View(model);
        }

        private static void PopulateSampleRecommendations(CropAdvisoryViewModel model)
        {
            model.WeatherAlert = new WeatherNoteItem
            {
                Title = "Moderate Rainfall Expected Next Week",
                Message = "Heavy rain showers expected in Northern Bangladesh from Sep 3 - Sep 6. Ensure field drainage channels are clear before sowing sensitive seeds.",
                IconClass = "bi-cloud-drizzle-fill",
                BadgeText = "Local Weather Advisory"
            };

            model.RecommendedCrops = new List<CropRecommendationItem>
            {
                new CropRecommendationItem
                {
                    Id = 1,
                    CropName = "Boro Rice (BRRI Dhan-89)",
                    ScientificName = "Oryza sativa",
                    IconClass = "bi-flower2",
                    MatchReason = "Optimal match for your clay-loam soil, Rabi season, and assured irrigation access in Rajshahi division.",
                    MatchPercentage = 98,
                    YieldEstimate = "6.5 - 7.5 tonnes / hectare",
                    CultivationGuide = new CultivationGuideDetails
                    {
                        GrowingSeason = "Rabi Season (November to May)",
                        WaterAndSoilRequirements = "Lowland clay-loam soil with medium organic matter. Requires continuous shallow standing water (3-5 cm) during tillering.",
                        GrowingDuration = "150 – 155 Days",
                        FertilizerNeeds = "Urea: 110 kg/acre (apply in 3 equal splits), TSP: 35 kg/acre, MoP: 45 kg/acre, Gypsum: 25 kg/acre at final land prep.",
                        CommonPestsAndDiseases = "Stem Borer, Rice Blast, Brown Plant Hopper. Monitor fields weekly; spray neem-based organic pesticide at first sight of yellowing leaves.",
                        Precautions = "Drain water 10-12 days before expected harvest. Use certified BRRI seed stock for high germination rate."
                    }
                },
                new CropRecommendationItem
                {
                    Id = 2,
                    CropName = "High-Yield Wheat (BARI Gom-33)",
                    ScientificName = "Triticum aestivum",
                    IconClass = "bi-tsunami",
                    MatchReason = "Excellent alternative crop requiring 40% less irrigation while yielding high profit in cool winter soils.",
                    MatchPercentage = 91,
                    YieldEstimate = "4.0 - 4.8 tonnes / hectare",
                    CultivationGuide = new CultivationGuideDetails
                    {
                        GrowingSeason = "Rabi Season (Mid-November to March)",
                        WaterAndSoilRequirements = "Well-drained loam or clay-loam soil. Needs 3-4 light irrigations at crown root initiation, flowering, and grain filling stages.",
                        GrowingDuration = "105 – 115 Days",
                        FertilizerNeeds = "Urea: 90 kg/acre, TSP: 40 kg/acre, MoP: 30 kg/acre. Apply full P & K with 50% N during sowing.",
                        CommonPestsAndDiseases = "Wheat Blast, Aphids. BARI Gom-33 is blast-resistant; watch out for aphids during warm dry spells.",
                        Precautions = "Sow seeds before Dec 10 for maximum grain fill weight. Avoid waterlogging during initial germination."
                    }
                },
                new CropRecommendationItem
                {
                    Id = 3,
                    CropName = "Hybrid Maize (Sunshine-55)",
                    ScientificName = "Zea mays",
                    IconClass = "bi-sun-fill",
                    MatchReason = "High market demand for poultry feed raw material; excellent match following previous Aman harvest.",
                    MatchPercentage = 86,
                    YieldEstimate = "9.0 - 10.5 tonnes / hectare",
                    CultivationGuide = new CultivationGuideDetails
                    {
                        GrowingSeason = "Rabi / Early Summer",
                        WaterAndSoilRequirements = "Deep, fertile, well-drained loamy soil. Sensitive to standing water; requires furrow irrigation.",
                        GrowingDuration = "135 – 145 Days",
                        FertilizerNeeds = "Urea: 140 kg/acre, TSP: 55 kg/acre, MoP: 60 kg/acre, Zinc Sulphate: 5 kg/acre.",
                        CommonPestsAndDiseases = "Fall Armyworm. Inspect whorls regularly; apply bio-pesticide Pheromone traps early.",
                        Precautions = "Maintain optimum plant spacing (60 cm x 20 cm) for maximum cob size and sun exposure."
                    }
                }
            };
        }

        private static readonly Dictionary<string, string> AllowedDistricts = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Bogra", "Bogra" },
            { "Bogura", "Bogra" },
            { "Dinajpur", "Dinajpur" },
            { "Rangpur", "Rangpur" },
            { "Rajshahi", "Rajshahi" },
            { "Jessore", "Jessore" },
            { "Barisal", "Barisal" },
            { "Mymensingh", "Mymensingh" },
            { "Comilla", "Comilla" },
            { "Sylhet", "Sylhet" },
            { "Dhaka", "Dhaka" }
        };

        private static readonly Dictionary<string, string> AllowedCrops = new(StringComparer.OrdinalIgnoreCase)
        {
            { "All", "All" },
            { "Rice", "Rice" },
            { "Potato", "Potato" },
            { "Wheat", "Wheat" },
            { "Maize", "Maize" },
            { "Mustard", "Mustard" },
            { "Tomato", "Tomato" },
            { "Brinjal", "Brinjal" },
            { "Chili", "Chili" }
        };

        private static readonly Dictionary<string, string> AllowedConditions = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Cloudy with Showers", "Cloudy with Showers" },
            { "Dense Fog & High Moisture", "Dense Fog & High Moisture" },
            { "Monsoon Rain & High Humidity", "Monsoon Rain & High Humidity" },
            { "Overcast with Light Drizzle", "Overcast with Light Drizzle" },
            { "Warm & Humid with Stagnant Air", "Warm & Humid with Stagnant Air" },
            { "Clear & Dry Skies", "Clear & Dry Skies" }
        };

        private static string SanitizeDistrict(string? input)
        {
            if (!string.IsNullOrWhiteSpace(input))
            {
                var trimmed = input.Trim();
                foreach (var entry in AllowedDistricts)
                {
                    if (trimmed.Contains(entry.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.Value;
                    }
                }
            }
            return "Bogra";
        }

        private static string SanitizeCrop(string? input)
        {
            if (!string.IsNullOrWhiteSpace(input))
            {
                var trimmed = input.Trim();
                if (AllowedCrops.TryGetValue(trimmed, out var matchedCrop))
                {
                    return matchedCrop;
                }
            }
            return "All";
        }

        private static string? SanitizeCondition(string? input)
        {
            if (!string.IsNullOrWhiteSpace(input))
            {
                var trimmed = input.Trim();
                if (AllowedConditions.TryGetValue(trimmed, out var matchedCondition))
                {
                    return matchedCondition;
                }
            }
            return null;
        }
    }
}
