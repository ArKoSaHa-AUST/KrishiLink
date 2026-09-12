namespace KrishiLink.Models.Entities
{
    /// <summary>
    /// Agrometeorological rule defining weather conditions that trigger crop disease/pest outbreaks.
    /// Based on Bangladesh Department of Agricultural Extension (DAE), BARI, and BRRI research.
    /// </summary>
    public class PestDiseaseRule
    {
        public int Id { get; set; }
        public string DiseaseName { get; set; } = string.Empty;
        public string BanglaName { get; set; } = string.Empty;
        public string PathogenOrPest { get; set; } = string.Empty;
        public string Category { get; set; } = "Fungal Disease"; // Fungal Disease, Insect Pest, Bacterial Disease, Viral Disease
        public List<string> TargetCrops { get; set; } = new();

        // Meteorological Thresholds
        public double MinTemp { get; set; }
        public double MaxTemp { get; set; }
        public double MinHumidity { get; set; }
        public double? MaxHumidity { get; set; }
        public List<string> MatchingWeatherConditions { get; set; } = new(); // e.g., "Foggy", "Rainy", "Cloudy", "Humid"
        public bool RequiresConsecutiveDays { get; set; } = false;

        // Alert Guidance
        public string Severity { get; set; } = "High"; // Critical, High, Moderate, Advisory
        public string Symptoms { get; set; } = string.Empty;
        public string BanglaSymptoms { get; set; } = string.Empty;
        public string TriggerReason { get; set; } = string.Empty;
        public string BanglaTriggerReason { get; set; } = string.Empty;
        public List<string> ActionableRemedies { get; set; } = new();
        public List<string> BanglaActionableRemedies { get; set; } = new();
        public string PreventiveSpray { get; set; } = string.Empty;
        public string OrganicControl { get; set; } = string.Empty;
        public string IconClass { get; set; } = "bi-exclamation-triangle-fill";
        public string BadgeClass { get; set; } = "danger";
    }

    /// <summary>
    /// Represents regional meteorological forecast and humidity telemetry for a district.
    /// </summary>
    public class RegionalWeatherForecast
    {
        public string District { get; set; } = "Bogra";
        public string Division { get; set; } = "Rajshahi";
        public double Temperature { get; set; } = 28.0;
        public double MinTemp { get; set; } = 24.0;
        public double MaxTemp { get; set; } = 32.0;
        public double Humidity { get; set; } = 88.0; // Relative humidity %
        public double RainProbability { get; set; } = 65.0; // Rain chance %
        public double WindSpeedKmh { get; set; } = 14.0;
        public string Condition { get; set; } = "Cloudy with Showers";
        public string BanglaCondition { get; set; } = "মেঘলা ও মাঝারি বৃষ্টি";
        public string ConditionIcon { get; set; } = "bi-cloud-rain-fill";
        public DateTime ForecastDate { get; set; } = DateTime.Today;
        public List<DailyForecastEntry> FiveDayForecast { get; set; } = new();
    }

    public class DailyForecastEntry
    {
        public DateTime Date { get; set; }
        public string DayName { get; set; } = string.Empty;
        public string BanglaDayName { get; set; } = string.Empty;
        public double MaxTemp { get; set; }
        public double MinTemp { get; set; }
        public double Humidity { get; set; }
        public double RainChance { get; set; }
        public string Condition { get; set; } = string.Empty;
        public string ConditionIcon { get; set; } = "bi-sun-fill";
    }

    /// <summary>
    /// Active evaluated alert resulting from matching current/forecast weather with disease rules.
    /// </summary>
    public class EvaluatedPestAlert
    {
        public int RuleId { get; set; }
        public string DiseaseName { get; set; } = string.Empty;
        public string BanglaName { get; set; } = string.Empty;
        public string PathogenOrPest { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<string> TargetCrops { get; set; } = new();
        public string Severity { get; set; } = "High"; // Critical, High, Moderate
        public string Symptoms { get; set; } = string.Empty;
        public string BanglaSymptoms { get; set; } = string.Empty;
        public string TriggerExplanation { get; set; } = string.Empty;
        public string BanglaTriggerExplanation { get; set; } = string.Empty;
        public List<string> ActionableRemedies { get; set; } = new();
        public List<string> BanglaActionableRemedies { get; set; } = new();
        public string PreventiveSpray { get; set; } = string.Empty;
        public string OrganicControl { get; set; } = string.Empty;
        public string IconClass { get; set; } = "bi-exclamation-triangle-fill";
        public string BadgeClass { get; set; } = "danger";
        public double RiskPercentage { get; set; } = 85.0;
    }
}
