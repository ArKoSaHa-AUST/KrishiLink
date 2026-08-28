using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    public class AdvisoryController : Controller
    {
        /// <summary>
        /// GET: /Advisory
        /// Renders the Crop Advisory form and recommendations.
        /// </summary>
        public IActionResult Index(bool analyze = false)
        {
            var model = new CropAdvisoryViewModel();

            if (analyze)
            {
                PopulateSampleRecommendations(model);
                model.HasSubmitted = true;
            }

            return View(model);
        }

        /// <summary>
        /// POST: /Advisory
        /// Processes farmer's land inputs and generates rule-based recommendations.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(CropAdvisoryViewModel model)
        {
            PopulateSampleRecommendations(model);
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
    }
}
