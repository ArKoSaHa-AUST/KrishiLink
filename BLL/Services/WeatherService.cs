using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using KrishiLink.BLL.Helpers;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace KrishiLink.BLL.Services
{
    public interface IWeatherService
    {
        Task<RegionalWeatherForecast> GetForecastAsync(string district, CancellationToken cancellationToken = default);
        Task<List<WeatherData>> GetCachedOrLiveDailyAsync(string district, int days = 7, CancellationToken cancellationToken = default);
    }

    public class WeatherService : IWeatherService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<WeatherService> _logger;

        public WeatherService(
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            ApplicationDbContext db,
            ILogger<WeatherService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _db = db;
            _logger = logger;
        }

        public async Task<RegionalWeatherForecast> GetForecastAsync(string district, CancellationToken cancellationToken = default)
        {
            var cleanDistrict = string.IsNullOrWhiteSpace(district) ? "Bogra" : district.Trim();
            if (cleanDistrict.Equals("Bogura", StringComparison.OrdinalIgnoreCase)) cleanDistrict = "Bogra";
            if (cleanDistrict.Equals("Jessore", StringComparison.OrdinalIgnoreCase)) cleanDistrict = "Jashore";
            if (cleanDistrict.Equals("Barisal", StringComparison.OrdinalIgnoreCase)) cleanDistrict = "Barishal";
            if (cleanDistrict.Equals("Chittagong", StringComparison.OrdinalIgnoreCase)) cleanDistrict = "Chattogram";
            if (cleanDistrict.Equals("Comilla", StringComparison.OrdinalIgnoreCase)) cleanDistrict = "Cumilla";

            string cacheKey = $"Weather:Forecast:{cleanDistrict}";
            if (_cache.TryGetValue(cacheKey, out RegionalWeatherForecast? cached) && cached != null)
            {
                return cached;
            }

            var coords = BangladeshGeo.GetCoordinates(cleanDistrict);
            var division = BangladeshGeo.GetDivision(cleanDistrict);

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);

                var url = string.Format(
                    CultureInfo.InvariantCulture,
                    "https://api.open-meteo.com/v1/forecast?latitude={0:F4}&longitude={1:F4}&daily=weathercode,temperature_2m_max,temperature_2m_min,precipitation_sum,precipitation_probability_max,relative_humidity_2m_max,relative_humidity_2m_min,wind_speed_10m_max&timezone=Asia%2FDhaka",
                    coords.Latitude,
                    coords.Longitude);

                var response = await client.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    var meteoData = JsonSerializer.Deserialize<OpenMeteoResponse>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (meteoData?.Daily != null && meteoData.Daily.Time.Count > 0)
                    {
                        var forecast = MapToRegionalForecast(cleanDistrict, division, meteoData.Daily);

                        // Persist or update in DB
                        try
                        {
                            await SaveForecastToDbAsync(cleanDistrict, forecast, meteoData.Daily, cancellationToken);
                        }
                        catch (Exception dbEx)
                        {
                            _logger.LogWarning(dbEx, "Failed to persist weather forecast to DB for {District}", cleanDistrict);
                        }

                        _cache.Set(cacheKey, forecast, TimeSpan.FromHours(3));
                        return forecast;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live weather fetch from Open-Meteo failed for {District}. Falling back to cached/simulated weather.", cleanDistrict);
            }

            // Fallback: Check DB cached rows
            try
            {
                var dbRows = await _db.WeatherData
                    .AsNoTracking()
                    .Where(w => w.District == cleanDistrict || w.Location == cleanDistrict)
                    .OrderByDescending(w => w.ForecastDate)
                    .Take(7)
                    .ToListAsync(cancellationToken);

                if (dbRows.Count > 0)
                {
                    var latest = dbRows[0];
                    var (cond, banglaCond, icon) = ResolveWeatherCode(latest.WeatherCode);
                    var fallbackForecast = new RegionalWeatherForecast
                    {
                        District = cleanDistrict,
                        Division = division,
                        Temperature = latest.Temperature > 0 ? latest.Temperature : 28.0,
                        MinTemp = latest.TemperatureMin > 0 ? latest.TemperatureMin : 22.0,
                        MaxTemp = latest.TemperatureMax > 0 ? latest.TemperatureMax : 32.0,
                        Humidity = latest.HumidityMax > 0 ? (latest.HumidityMax + latest.HumidityMin) / 2.0 : 75.0,
                        RainProbability = latest.PrecipitationProbability,
                        WindSpeedKmh = 12.0,
                        Condition = string.IsNullOrWhiteSpace(latest.Condition) ? cond : latest.Condition,
                        BanglaCondition = banglaCond,
                        ConditionIcon = icon,
                        ForecastDate = latest.ForecastDate,
                        FiveDayForecast = dbRows.Select(r =>
                        {
                            var (c, bc, ic) = ResolveWeatherCode(r.WeatherCode);
                            return new DailyForecastEntry
                            {
                                Date = r.ForecastDate,
                                DayName = r.ForecastDate.ToString("ddd", CultureInfo.InvariantCulture),
                                BanglaDayName = GetBanglaDayName(r.ForecastDate.DayOfWeek),
                                MaxTemp = r.TemperatureMax,
                                MinTemp = r.TemperatureMin,
                                Humidity = (r.HumidityMax + r.HumidityMin) / 2.0,
                                RainChance = r.PrecipitationProbability,
                                Condition = string.IsNullOrWhiteSpace(r.Condition) ? c : r.Condition,
                                ConditionIcon = ic
                            };
                        }).ToList()
                    };

                    _cache.Set(cacheKey, fallbackForecast, TimeSpan.FromMinutes(30));
                    return fallbackForecast;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DB weather lookup failed for {District}", cleanDistrict);
            }

            // Ultimate Fallback: Realistic seasonal baseline
            var simulated = GenerateSeasonalFallback(cleanDistrict, division);
            _cache.Set(cacheKey, simulated, TimeSpan.FromMinutes(15));
            return simulated;
        }

        public async Task<List<WeatherData>> GetCachedOrLiveDailyAsync(string district, int days = 7, CancellationToken cancellationToken = default)
        {
            var forecast = await GetForecastAsync(district, cancellationToken);
            return forecast.FiveDayForecast.Take(days).Select(f => new WeatherData
            {
                District = forecast.District,
                Location = forecast.District,
                ForecastDate = f.Date,
                Temperature = (f.MaxTemp + f.MinTemp) / 2.0,
                TemperatureMax = f.MaxTemp,
                TemperatureMin = f.MinTemp,
                HumidityMax = f.Humidity,
                HumidityMin = Math.Max(30, f.Humidity - 25),
                PrecipitationProbability = f.RainChance,
                Condition = f.Condition,
                FetchedAt = DateTime.UtcNow
            }).ToList();
        }

        private async Task SaveForecastToDbAsync(
            string district,
            RegionalWeatherForecast forecast,
            OpenMeteoDaily daily,
            CancellationToken cancellationToken)
        {
            for (int i = 0; i < daily.Time.Count && i < 7; i++)
            {
                if (!DateTime.TryParse(daily.Time[i], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    continue;
                }

                var code = daily.WeatherCode != null && i < daily.WeatherCode.Count ? daily.WeatherCode[i] : 0;
                var maxT = daily.TemperatureMax != null && i < daily.TemperatureMax.Count ? daily.TemperatureMax[i] : 30.0;
                var minT = daily.TemperatureMin != null && i < daily.TemperatureMin.Count ? daily.TemperatureMin[i] : 22.0;
                var maxH = daily.HumidityMax != null && i < daily.HumidityMax.Count ? daily.HumidityMax[i] : 85.0;
                var minH = daily.HumidityMin != null && i < daily.HumidityMin.Count ? daily.HumidityMin[i] : 60.0;
                var precipSum = daily.PrecipitationSum != null && i < daily.PrecipitationSum.Count ? daily.PrecipitationSum[i] : 0.0;
                var precipProb = daily.PrecipitationProbability != null && i < daily.PrecipitationProbability.Count ? daily.PrecipitationProbability[i] : 0.0;
                var (cond, _, _) = ResolveWeatherCode(code);

                var existing = await _db.WeatherData
                    .FirstOrDefaultAsync(w => (w.District == district || w.Location == district) && w.ForecastDate.Date == date.Date, cancellationToken);

                if (existing != null)
                {
                    existing.District = district;
                    existing.Location = district;
                    existing.TemperatureMax = maxT;
                    existing.TemperatureMin = minT;
                    existing.Temperature = Math.Round((maxT + minT) / 2.0, 1);
                    existing.HumidityMax = maxH;
                    existing.HumidityMin = minH;
                    existing.PrecipitationMm = precipSum;
                    existing.PrecipitationProbability = precipProb;
                    existing.WeatherCode = code;
                    existing.Condition = cond;
                    existing.FetchedAt = DateTime.UtcNow;
                }
                else
                {
                    _db.WeatherData.Add(new WeatherData
                    {
                        District = district,
                        Location = district,
                        ForecastDate = date,
                        TemperatureMax = maxT,
                        TemperatureMin = minT,
                        Temperature = Math.Round((maxT + minT) / 2.0, 1),
                        HumidityMax = maxH,
                        HumidityMin = minH,
                        PrecipitationMm = precipSum,
                        PrecipitationProbability = precipProb,
                        WeatherCode = code,
                        Condition = cond,
                        FetchedAt = DateTime.UtcNow
                    });
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        private static RegionalWeatherForecast MapToRegionalForecast(string district, string division, OpenMeteoDaily daily)
        {
            var dailyEntries = new List<DailyForecastEntry>();

            for (int i = 0; i < daily.Time.Count; i++)
            {
                if (!DateTime.TryParse(daily.Time[i], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    continue;
                }

                var code = daily.WeatherCode != null && i < daily.WeatherCode.Count ? daily.WeatherCode[i] : 0;
                var maxT = daily.TemperatureMax != null && i < daily.TemperatureMax.Count ? daily.TemperatureMax[i] : 30.0;
                var minT = daily.TemperatureMin != null && i < daily.TemperatureMin.Count ? daily.TemperatureMin[i] : 22.0;
                var maxH = daily.HumidityMax != null && i < daily.HumidityMax.Count ? daily.HumidityMax[i] : 85.0;
                var minH = daily.HumidityMin != null && i < daily.HumidityMin.Count ? daily.HumidityMin[i] : 60.0;
                var precipProb = daily.PrecipitationProbability != null && i < daily.PrecipitationProbability.Count ? daily.PrecipitationProbability[i] : 0.0;
                var (cond, banglaCond, icon) = ResolveWeatherCode(code);

                dailyEntries.Add(new DailyForecastEntry
                {
                    Date = date,
                    DayName = date.ToString("ddd", CultureInfo.InvariantCulture),
                    BanglaDayName = GetBanglaDayName(date.DayOfWeek),
                    MaxTemp = Math.Round(maxT, 1),
                    MinTemp = Math.Round(minT, 1),
                    Humidity = Math.Round((maxH + minH) / 2.0, 1),
                    RainChance = Math.Round(precipProb, 0),
                    Condition = cond,
                    ConditionIcon = icon
                });
            }

            var todayEntry = dailyEntries.FirstOrDefault() ?? new DailyForecastEntry
            {
                Date = DateTime.Today,
                DayName = "Today",
                BanglaDayName = "আজ",
                MaxTemp = 30.0,
                MinTemp = 22.0,
                Humidity = 75.0,
                RainChance = 20.0,
                Condition = "Clear",
                ConditionIcon = "bi-sun-fill"
            };

            var (todayCond, todayBanglaCond, todayIcon) = ResolveWeatherCode(daily.WeatherCode?.FirstOrDefault() ?? 0);

            return new RegionalWeatherForecast
            {
                District = district,
                Division = division,
                Temperature = Math.Round((todayEntry.MaxTemp + todayEntry.MinTemp) / 2.0, 1),
                MinTemp = todayEntry.MinTemp,
                MaxTemp = todayEntry.MaxTemp,
                Humidity = todayEntry.Humidity,
                RainProbability = todayEntry.RainChance,
                WindSpeedKmh = daily.WindSpeed != null && daily.WindSpeed.Count > 0 ? Math.Round(daily.WindSpeed[0], 1) : 12.0,
                Condition = todayCond,
                BanglaCondition = todayBanglaCond,
                ConditionIcon = todayIcon,
                ForecastDate = todayEntry.Date,
                FiveDayForecast = dailyEntries
            };
        }

        public static (string Condition, string BanglaCondition, string Icon) ResolveWeatherCode(int code)
        {
            return code switch
            {
                0 => ("Clear Sky", "পরিষ্কার আকাশ", "bi-sun-fill"),
                1 => ("Mainly Clear", "প্রায় মেঘমুক্ত আকাশ", "bi-brightness-high-fill"),
                2 => ("Partly Cloudy", "আংশিক মেঘলা", "bi-cloud-sun-fill"),
                3 => ("Overcast", "মেঘলা আকাশ", "bi-clouds-fill"),
                45 or 48 => ("Dense Fog", "ঘন কুয়াশা", "bi-cloud-fog2-fill"),
                51 or 53 or 55 => ("Light Drizzle", "গুঁড়ি গুঁড়ি বৃষ্টি", "bi-cloud-drizzle-fill"),
                61 or 63 or 65 => ("Rainy", "বৃষ্টিপাত", "bi-cloud-rain-fill"),
                71 or 73 or 75 => ("Cold Wave", "শৈত্যপ্রবাহ", "bi-snow"),
                80 or 81 or 82 => ("Rain Showers", "ভারী বর্ষণ", "bi-cloud-rain-heavy-fill"),
                95 or 96 or 99 => ("Thunderstorm", "বজ্রঝড় / কালবৈশাখী", "bi-cloud-lightning-rain-fill"),
                _ => ("Clear & Sunny", "পরিষ্কার ও রৌদ্রোজ্জ্বল", "bi-sun-fill")
            };
        }

        private static string GetBanglaDayName(DayOfWeek day) => day switch
        {
            DayOfWeek.Sunday => "রবিবার",
            DayOfWeek.Monday => "সোমবার",
            DayOfWeek.Tuesday => "মঙ্গলবার",
            DayOfWeek.Wednesday => "বুধবার",
            DayOfWeek.Thursday => "বৃহস্পতিবার",
            DayOfWeek.Friday => "শুক্রবার",
            DayOfWeek.Saturday => "শনিবার",
            _ => "আজ"
        };

        private static RegionalWeatherForecast GenerateSeasonalFallback(string district, string division)
        {
            int month = DateTime.Now.Month;
            double temp = month switch
            {
                12 or 1 or 2 => 18.0,
                3 or 4 or 5 => 32.0,
                6 or 7 or 8 => 29.0,
                _ => 26.0
            };
            double humidity = month switch
            {
                6 or 7 or 8 or 9 => 88.0,
                12 or 1 => 80.0,
                _ => 65.0
            };
            double rainProb = month is >= 5 and <= 9 ? 60.0 : 15.0;
            string cond = month is >= 6 and <= 8 ? "Monsoon Showers" : (month is 12 or 1 ? "Mild Foggy Morning" : "Clear & Sunny");
            string banglaCond = month is >= 6 and <= 8 ? "মৌসুমি বর্ষা" : (month is 12 or 1 ? "শীতকালীন কুয়াশা" : "পরিষ্কার রোদ");
            string icon = month is >= 6 and <= 8 ? "bi-cloud-rain-fill" : (month is 12 or 1 ? "bi-cloud-fog2-fill" : "bi-sun-fill");

            var entries = new List<DailyForecastEntry>();
            for (int i = 0; i < 7; i++)
            {
                var d = DateTime.Today.AddDays(i);
                entries.Add(new DailyForecastEntry
                {
                    Date = d,
                    DayName = d.ToString("ddd", CultureInfo.InvariantCulture),
                    BanglaDayName = GetBanglaDayName(d.DayOfWeek),
                    MaxTemp = temp + 3,
                    MinTemp = temp - 4,
                    Humidity = humidity,
                    RainChance = rainProb,
                    Condition = cond,
                    ConditionIcon = icon
                });
            }

            return new RegionalWeatherForecast
            {
                District = district,
                Division = division,
                Temperature = temp,
                MinTemp = temp - 4,
                MaxTemp = temp + 3,
                Humidity = humidity,
                RainProbability = rainProb,
                WindSpeedKmh = 12.0,
                Condition = cond,
                BanglaCondition = banglaCond,
                ConditionIcon = icon,
                ForecastDate = DateTime.Today,
                FiveDayForecast = entries
            };
        }
    }

    public class OpenMeteoResponse
    {
        [JsonPropertyName("daily")]
        public OpenMeteoDaily? Daily { get; set; }
    }

    public class OpenMeteoDaily
    {
        [JsonPropertyName("time")]
        public List<string> Time { get; set; } = new();

        [JsonPropertyName("weathercode")]
        public List<int>? WeatherCode { get; set; }

        [JsonPropertyName("temperature_2m_max")]
        public List<double>? TemperatureMax { get; set; }

        [JsonPropertyName("temperature_2m_min")]
        public List<double>? TemperatureMin { get; set; }

        [JsonPropertyName("relative_humidity_2m_max")]
        public List<double>? HumidityMax { get; set; }

        [JsonPropertyName("relative_humidity_2m_min")]
        public List<double>? HumidityMin { get; set; }

        [JsonPropertyName("precipitation_sum")]
        public List<double>? PrecipitationSum { get; set; }

        [JsonPropertyName("precipitation_probability_max")]
        public List<double>? PrecipitationProbability { get; set; }

        [JsonPropertyName("wind_speed_10m_max")]
        public List<double>? WindSpeed { get; set; }
    }
}
