namespace KrishiLink.Models.Entities
{
    /// <summary>
    /// Represents a reference entry in the Crop Planting & Harvesting Calendar
    /// based on Bangladesh Department of Agricultural Extension (DAE) guidelines.
    /// </summary>
    public class CropCalendarEntry
    {
        public int Id { get; set; }

        /// <summary>English common name (e.g., Boro Rice, Wheat, Potato)</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Bangla name (e.g., বোরো ধান, গম, গোল আলু)</summary>
        public string BanglaName { get; set; } = string.Empty;

        /// <summary>Botanical / Scientific name (e.g., Oryza sativa)</summary>
        public string ScientificName { get; set; } = string.Empty;

        /// <summary>
        /// Crop category: Cereals, CashCrops, Tubers, Oilseeds, Pulses, Vegetables, Spices, Fruits
        /// </summary>
        public string Category { get; set; } = "Cereals";

        /// <summary>
        /// Cropping season: Rabi (Winter), Kharif-1 (Early Summer), Kharif-2 (Monsoon), YearRound
        /// </summary>
        public string Season { get; set; } = "Rabi";

        /// <summary>Months (1-12) during which sowing / transplanting / seedbed prep occurs</summary>
        public List<int> SowingMonths { get; set; } = new();

        /// <summary>Months (1-12) during which vegetative growth and tillering occurs</summary>
        public List<int> GrowingMonths { get; set; } = new();

        /// <summary>Months (1-12) during which harvesting / reaping occurs</summary>
        public List<int> HarvestingMonths { get; set; } = new();

        /// <summary>Estimated field duration from seed to harvest (e.g., "140–160 Days")</summary>
        public string DurationDays { get; set; } = string.Empty;

        /// <summary>Optimal temperature range (e.g., "20°C – 32°C")</summary>
        public string OptimalTemperature { get; set; } = string.Empty;

        /// <summary>Suitable soil types (e.g., "Clay Loam, Alluvial Silt")</summary>
        public string SoilTypes { get; set; } = string.Empty;

        /// <summary>Water and irrigation requirement profile</summary>
        public string WaterRequirement { get; set; } = string.Empty;

        /// <summary>Popular high-yielding (HYV) or hybrid varieties in Bangladesh</summary>
        public string PopularVarieties { get; set; } = string.Empty;

        /// <summary>Major growing districts / geographical zones in Bangladesh</summary>
        public string MajorDistricts { get; set; } = string.Empty;

        /// <summary>
        /// Specific division suitability (e.g., "All", "Rajshahi", "Rangpur", "Khulna", "Barisal", "Sylhet", "Dhaka", "Chittagong")
        /// </summary>
        public string Division { get; set; } = "All";

        /// <summary>Practical agronomic tips and management precautions</summary>
        public string KeyTips { get; set; } = string.Empty;

        /// <summary>Bootstrap Icon class for UI badge (e.g., "bi-flower2", "bi-sun-fill")</summary>
        public string IconClass { get; set; } = "bi-flower2";

        /// <summary>Visual theme color / badge style (e.g., "success", "warning", "info", "primary")</summary>
        public string BadgeColor { get; set; } = "success";

        /// <summary>Canonical mapping to onboarding farmer crop options (e.g. "Rice (Boro)", "Potato", "Wheat")</summary>
        public string? ProfileCropName { get; set; }
    }
}
