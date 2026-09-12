namespace KrishiLink.Models.Entities
{
    public class WeatherData
    {
        public int Id { get; set; }
        public string Location { get; set; } = string.Empty;
        public string? District { get; set; }
        public double Temperature { get; set; }
        public double TemperatureMax { get; set; }
        public double TemperatureMin { get; set; }
        public double HumidityMax { get; set; }
        public double HumidityMin { get; set; }
        public double PrecipitationMm { get; set; }
        public double PrecipitationProbability { get; set; }
        public int WeatherCode { get; set; }
        public string Condition { get; set; } = string.Empty;
        public DateTime ForecastDate { get; set; }
        public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    }
}

