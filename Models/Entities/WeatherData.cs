namespace KrishiLink.Models.Entities
{
    public class WeatherData
    {
        public int Id { get; set; }
        public string Location { get; set; } = string.Empty;
        public double Temperature { get; set; }
        public string Condition { get; set; } = string.Empty;
        public DateTime ForecastDate { get; set; }
    }
}
