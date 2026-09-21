using System;
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

        /// <summary>Where these numbers came from, so the UI never presents an estimate as a live reading.</summary>
        public WeatherSource Source { get; set; } = WeatherSource.Live;

        /// <summary>When the underlying observation was retrieved from the provider (UTC).</summary>
        public DateTime RetrievedAtUtc { get; set; } = DateTime.UtcNow;

        public bool IsLive => Source == WeatherSource.Live;

        /// <summary>Short provenance label, e.g. "Live · 4 min ago" or "Estimated".</summary>
        public string FreshnessLabel => Source switch
        {
            WeatherSource.Live => $"Live · {Age()}",
            WeatherSource.Recent => $"Last reading · {Age()}",
            _ => "Estimated (forecast unavailable)"
        };

        private string Age()
        {
            var minutes = (int)Math.Max(0, (DateTime.UtcNow - RetrievedAtUtc).TotalMinutes);
            if (minutes < 1) return "just now";
            if (minutes < 60) return $"{minutes} min ago";
            var hours = minutes / 60;
            return hours < 24 ? $"{hours} h ago" : $"{hours / 24} d ago";
        }
    }

    /// <summary>Provenance of a weather reading.</summary>
    public enum WeatherSource
    {
        /// <summary>Fetched from Open-Meteo on this request.</summary>
        Live,

        /// <summary>Replayed from the last stored Open-Meteo reading because the provider was unreachable.</summary>
        Recent,

        /// <summary>Seasonal climatology, used only when no real reading exists at all.</summary>
        Estimated
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
