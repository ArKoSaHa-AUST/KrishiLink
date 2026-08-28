namespace KrishiLink.Models.ViewModels
{
    /// <summary>
    /// ViewModel for Crop Advisory Form & Recommendations page.
    /// Supports low-literacy inputs and detailed cultivation guides.
    /// </summary>
    public class CropAdvisoryViewModel
    {
        // --- STEP 1: FORM INPUTS ---
        public string Location { get; set; } = "Bogra, Rajshahi Division";
        public string Season { get; set; } = "Rabi (Winter)";
        public string SoilType { get; set; } = "Clay Loam";
        public double? SoilPh { get; set; } = 6.5;
        public double? LandSizeDecimal { get; set; } = 50; // 50 decimals ~ 0.5 acre
        public string? PreviousCrop { get; set; } = "Aman Rice";
        public bool HasIrrigation { get; set; } = true;

        // --- STATE FLAGS ---
        public bool HasSubmitted { get; set; } = false;
        public bool IsSaved { get; set; } = false;

        // --- STEP 2: RESULTS & RECOMMENDATIONS ---
        public WeatherNoteItem? WeatherAlert { get; set; }
        public List<CropRecommendationItem> RecommendedCrops { get; set; } = new();
    }

    public class WeatherNoteItem
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string IconClass { get; set; } = "bi-cloud-rain-fill";
        public string BadgeText { get; set; } = "Weather Alert";
    }

    public class CropRecommendationItem
    {
        public int Id { get; set; }
        public string CropName { get; set; } = string.Empty;
        public string ScientificName { get; set; } = string.Empty;
        public string IconClass { get; set; } = "bi-flower2";
        public string MatchReason { get; set; } = string.Empty;
        public int MatchPercentage { get; set; } = 95;
        public string YieldEstimate { get; set; } = string.Empty;
        public CultivationGuideDetails CultivationGuide { get; set; } = new();
    }

    public class CultivationGuideDetails
    {
        public string GrowingSeason { get; set; } = string.Empty;
        public string WaterAndSoilRequirements { get; set; } = string.Empty;
        public string GrowingDuration { get; set; } = string.Empty;
        public string FertilizerNeeds { get; set; } = string.Empty;
        public string CommonPestsAndDiseases { get; set; } = string.Empty;
        public string Precautions { get; set; } = string.Empty;
    }
}
