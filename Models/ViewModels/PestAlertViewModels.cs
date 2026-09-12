using KrishiLink.Models.Entities;

namespace KrishiLink.Models.ViewModels
{
    /// <summary>
    /// ViewModel for the Pest & Disease Alert Dashboard page (/Advisory/Alerts).
    /// </summary>
    public class PestAlertsIndexViewModel
    {
        public string SelectedDistrict { get; set; } = "Bogra";
        public string? SelectedCrop { get; set; } = "All";
        public bool IsCustomSimulated { get; set; } = false;

        // Custom simulation parameters
        public double? CustomTemp { get; set; }
        public double? CustomHumidity { get; set; }
        public string? CustomCondition { get; set; }

        // Live Weather Forecast Data
        public RegionalWeatherForecast Weather { get; set; } = new();

        // Active Triggered Alerts
        public List<EvaluatedPestAlert> ActiveAlerts { get; set; } = new();

        // All Agrometeorological Rules for Reference
        public List<PestDiseaseRule> AllRulesEncyclopedia { get; set; } = new();

        // Dropdown options
        public List<SelectOptionItem> Districts { get; set; } = new();
        public List<SelectOptionItem> Crops { get; set; } = new();
        public List<WeatherScenarioPreset> PresetScenarios { get; set; } = new();

        // Summary Statistics
        public int CriticalAlertsCount => ActiveAlerts.Count(a => a.Severity == "Critical");
        public int HighAlertsCount => ActiveAlerts.Count(a => a.Severity == "High");
        public int ModerateAlertsCount => ActiveAlerts.Count(a => a.Severity == "Moderate");
    }

    public class WeatherScenarioPreset
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string BanglaName { get; set; } = string.Empty;
        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public string Condition { get; set; } = string.Empty;
        public string BanglaCondition { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string BanglaDescription { get; set; } = string.Empty;
        public string ExpectedDiseaseTrigger { get; set; } = string.Empty;
        public string IconClass { get; set; } = "bi-cloud-fog2-fill";
    }
}
