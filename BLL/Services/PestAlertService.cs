using KrishiLink.BLL.Helpers;
using KrishiLink.DAL;
using Microsoft.Extensions.Caching.Memory;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;

namespace KrishiLink.BLL.Services
{
    public interface IPestAlertService
    {
        Task<PestAlertsIndexViewModel> GetPestAlertsDashboardAsync(
            string? district = null,
            string? crop = null,
            double? customTemp = null,
            double? customHumidity = null,
            string? customCondition = null);

        Task<RegionalWeatherForecast> GetRegionalWeatherAsync(string district);
        Task<List<EvaluatedPestAlert>> EvaluateAlertsAsync(RegionalWeatherForecast weather, string? crop = null);
        Task<WeatherNoteItem?> GetWeatherAlertNoteAsync(string location);
        Task<List<PestDiseaseRule>> GetAllDiseaseRulesAsync();
    }

    public class PestAlertService : IPestAlertService
    {
        private readonly IWeatherService _weatherService;
        private readonly IHostEnvironment _environment;
        private readonly IMemoryCache? _cache;

        public PestAlertService(IWeatherService weatherService, IHostEnvironment environment, IMemoryCache? cache = null)
        {
            _weatherService = weatherService;
            _environment = environment;
            _cache = cache;
        }

        // 1. Agrometeorological rule knowledge base (DAE, BARI, BRRI): App_Data/seed/pest-rules.json, reviewed as data (ADV-05).
        private IReadOnlyList<PestDiseaseRule> DiseaseRules => PestRuleSeed.Cached(_environment.ContentRootPath);

        // 2. Regional Forecast Data Base for Bangladesh Districts
        private static readonly Dictionary<string, RegionalWeatherForecast> DistrictForecasts = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Bogra"] = new RegionalWeatherForecast
            {
                District = "Bogra",
                Division = "Rajshahi",
                Temperature = 28.0,
                MinTemp = 24.0,
                MaxTemp = 32.0,
                Humidity = 88.0,
                RainProbability = 65.0,
                WindSpeedKmh = 14.0,
                Condition = "Cloudy with Showers",
                BanglaCondition = "মেঘলা ও মাঝারি বৃষ্টি",
                ConditionIcon = "bi-cloud-rain-fill",
                ForecastDate = DateTime.Today,
                FiveDayForecast = Generate5DayForecast(28.0, 88.0, "Showers", "bi-cloud-rain-fill")
            },
            ["Dinajpur"] = new RegionalWeatherForecast
            {
                District = "Dinajpur",
                Division = "Rangpur",
                Temperature = 26.5,
                MinTemp = 22.0,
                MaxTemp = 30.0,
                Humidity = 92.0,
                RainProbability = 70.0,
                WindSpeedKmh = 12.0,
                Condition = "Overcast & High Humidity",
                BanglaCondition = "মেঘাচ্ছন্ন ও উচ্চ আর্দ্রতা",
                ConditionIcon = "bi-cloud-sun-fill",
                ForecastDate = DateTime.Today,
                FiveDayForecast = Generate5DayForecast(26.5, 92.0, "Cloudy", "bi-clouds-fill")
            },
            ["Rangpur"] = new RegionalWeatherForecast
            {
                District = "Rangpur",
                Division = "Rangpur",
                Temperature = 27.0,
                MinTemp = 23.0,
                MaxTemp = 31.0,
                Humidity = 90.0,
                RainProbability = 60.0,
                WindSpeedKmh = 15.0,
                Condition = "Monsoon Rain Showers",
                BanglaCondition = "মৌসুমি বৃষ্টিপাত",
                ConditionIcon = "bi-cloud-drizzle-fill",
                ForecastDate = DateTime.Today,
                FiveDayForecast = Generate5DayForecast(27.0, 90.0, "Showers", "bi-cloud-drizzle-fill")
            },
            ["Rajshahi"] = new RegionalWeatherForecast
            {
                District = "Rajshahi",
                Division = "Rajshahi",
                Temperature = 31.5,
                MinTemp = 25.0,
                MaxTemp = 35.0,
                Humidity = 78.0,
                RainProbability = 35.0,
                WindSpeedKmh = 16.0,
                Condition = "Warm & Partly Cloudy",
                BanglaCondition = "উষ্ণ ও আংশিক মেঘলা",
                ConditionIcon = "bi-sun-fill",
                ForecastDate = DateTime.Today,
                FiveDayForecast = Generate5DayForecast(31.5, 78.0, "Sunny", "bi-sun-fill")
            },
            ["Jessore"] = new RegionalWeatherForecast
            {
                District = "Jessore",
                Division = "Khulna",
                Temperature = 29.5,
                MinTemp = 24.5,
                MaxTemp = 33.0,
                Humidity = 84.0,
                RainProbability = 50.0,
                WindSpeedKmh = 13.0,
                Condition = "Humid with Scattered Clouds",
                BanglaCondition = "আর্দ্র ও বিক্ষিপ্ত মেঘ",
                ConditionIcon = "bi-cloud-sun-fill",
                ForecastDate = DateTime.Today,
                FiveDayForecast = Generate5DayForecast(29.5, 84.0, "Humid", "bi-cloud-sun-fill")
            },
            ["Barisal"] = new RegionalWeatherForecast
            {
                District = "Barisal",
                Division = "Barisal",
                Temperature = 28.5,
                MinTemp = 25.0,
                MaxTemp = 31.5,
                Humidity = 94.0,
                RainProbability = 80.0,
                WindSpeedKmh = 22.0,
                Condition = "Coastal Squally Rain",
                BanglaCondition = "উপকূলীয় দমকা হাওয়াসহ বৃষ্টি",
                ConditionIcon = "bi-cloud-lightning-rain-fill",
                ForecastDate = DateTime.Today,
                FiveDayForecast = Generate5DayForecast(28.5, 94.0, "Rainy", "bi-cloud-lightning-rain-fill")
            },
            ["Mymensingh"] = new RegionalWeatherForecast
            {
                District = "Mymensingh",
                Division = "Mymensingh",
                Temperature = 27.5,
                MinTemp = 23.5,
                MaxTemp = 31.0,
                Humidity = 89.0,
                RainProbability = 65.0,
                WindSpeedKmh = 11.0,
                Condition = "Cloudy with Light Dew",
                BanglaCondition = "মেঘলা ও হালকা শিশির",
                ConditionIcon = "bi-cloud-drizzle-fill",
                ForecastDate = DateTime.Today,
                FiveDayForecast = Generate5DayForecast(27.5, 89.0, "Cloudy", "bi-cloud-drizzle-fill")
            },
            ["Comilla"] = new RegionalWeatherForecast
            {
                District = "Comilla",
                Division = "Chittagong",
                Temperature = 29.0,
                MinTemp = 24.0,
                MaxTemp = 32.5,
                Humidity = 86.0,
                RainProbability = 55.0,
                WindSpeedKmh = 14.0,
                Condition = "Warm & Humid with Showers",
                BanglaCondition = "উষ্ণ, আর্দ্র ও মাঝারি বৃষ্টি",
                ConditionIcon = "bi-cloud-rain-fill",
                ForecastDate = DateTime.Today,
                FiveDayForecast = Generate5DayForecast(29.0, 86.0, "Showers", "bi-cloud-rain-fill")
            },
            ["Sylhet"] = new RegionalWeatherForecast
            {
                District = "Sylhet",
                Division = "Sylhet",
                Temperature = 26.0,
                MinTemp = 22.0,
                MaxTemp = 29.5,
                Humidity = 96.0,
                RainProbability = 85.0,
                WindSpeedKmh = 18.0,
                Condition = "Heavy Haor Basin Rain",
                BanglaCondition = "হাওর অঞ্চলে ভারি বর্ষণ",
                ConditionIcon = "bi-cloud-heavy-rain-fill",
                ForecastDate = DateTime.Today,
                FiveDayForecast = Generate5DayForecast(26.0, 96.0, "Heavy Rain", "bi-cloud-rain-heavy-fill")
            },
            ["Dhaka"] = new RegionalWeatherForecast
            {
                District = "Dhaka",
                Division = "Dhaka",
                Temperature = 30.0,
                MinTemp = 25.0,
                MaxTemp = 33.5,
                Humidity = 82.0,
                RainProbability = 45.0,
                WindSpeedKmh = 12.0,
                Condition = "Warm & Humid",
                BanglaCondition = "উষ্ণ ও আর্দ্র আবহাওয়া",
                ConditionIcon = "bi-sun-fill",
                ForecastDate = DateTime.Today,
                FiveDayForecast = Generate5DayForecast(30.0, 82.0, "Warm", "bi-sun-fill")
            }
        };

        // 3. Preset Weather Scenarios for Testing / Simulation
        private static readonly List<WeatherScenarioPreset> Presets = new()
        {
            new WeatherScenarioPreset
            {
                Id = "winter-fog",
                Name = "Dense Winter Fog (আলুর নাবী ধসা ঝুঁকি)",
                BanglaName = "শীতকালীন ঘন কুয়াশা (আলুর নাবী ধসা)",
                Temperature = 16.0,
                Humidity = 94.0,
                Condition = "Dense Fog & High Moisture",
                BanglaCondition = "ঘন কুয়াশা ও স্যাঁতসেঁতে আবহাওয়া",
                Description = "Cold winter days (14°C–18°C) with dense morning fog and humidity > 90% triggering severe Potato Late Blight.",
                BanglaDescription = "ঘন কুয়াশা এবং ৯৪% আর্দ্রতার কারণে আলুর নাবী ধসা রোগের চরম ঝুঁকি তৈরি হয়।",
                ExpectedDiseaseTrigger = "Potato & Tomato Late Blight (Critical)",
                IconClass = "bi-cloud-fog2-fill"
            },
            new WeatherScenarioPreset
            {
                Id = "monsoon-humid",
                Name = "Monsoon Humid Warmth (ধানের ব্লাস্ট ও বিএলবি)",
                BanglaName = "মৌসুমি বর্ষা ও আর্দ্রতা (ধানের ব্লাস্ট ও পাতা পোড়া)",
                Temperature = 26.5,
                Humidity = 92.0,
                Condition = "Monsoon Rain & High Humidity",
                BanglaCondition = "মৌসুমি বর্ষণ ও উচ্চ আর্দ্রতা",
                Description = "Moderate temp with torrential monsoon showers and humidity > 90% favoring Rice Blast, BLB, and BPH.",
                BanglaDescription = "২৬.৫° সে. তাপমাত্রা ও ৯২% আর্দ্রতায় ধানের ব্লাস্ট ও ব্যাকটেরিয়া পাতা পোড়া দ্রুত ছড়ায়।",
                ExpectedDiseaseTrigger = "Rice Blast (Critical), Rice BLB (High), BPH (High)",
                IconClass = "bi-cloud-lightning-rain-fill"
            },
            new WeatherScenarioPreset
            {
                Id = "spring-cloudy",
                Name = "Overcast Spring (গমের ব্লাস্ট ও জাবপোকা)",
                BanglaName = "বসন্তকালীন মেঘলা আকাশ (গমের ব্লাস্ট ও জাবপোকা)",
                Temperature = 23.0,
                Humidity = 86.0,
                Condition = "Overcast with Light Drizzle",
                BanglaCondition = "মেঘলা আকাশ ও গুঁড়ি গুঁড়ি বৃষ্টি",
                Description = "Spring overcast skies (22°C–24°C, humidity 86%) triggering Wheat Blast in heading stage and Mustard Aphids.",
                BanglaDescription = "ফেব্রুয়ারির মেঘলা ও আর্দ্র আবহাওয়ায় গমের শিষ ব্লাস্টে আক্রান্ত হতে পারে।",
                ExpectedDiseaseTrigger = "Wheat Blast (High), Mustard Aphids (Moderate)",
                IconClass = "bi-clouds-fill"
            },
            new WeatherScenarioPreset
            {
                Id = "sultry-summer",
                Name = "Sultry Summer Heat (কারেন্ট পোকা ও ফল আর্মিওয়ার্ম)",
                BanglaName = "গুমোট গরম আবহাওয়া (কারেন্ট পোকা ও আর্মিওয়ার্ম)",
                Temperature = 31.0,
                Humidity = 82.0,
                Condition = "Warm & Humid with Stagnant Air",
                BanglaCondition = "গুমোট উষ্ণ ও আর্দ্র আবহাওয়া",
                Description = "Sultry heat in dense tillering paddy plots triggering Brown Plant Hopper (Current Poka) and Maize Fall Armyworm.",
                BanglaDescription = "৩১° সে. গুমোট তাপে বাদামি গাছফড়িং ও ভুট্টার ফল আর্মিওয়ার্ম ডিম ফুটে দ্রুত বৃদ্ধি পায়।",
                ExpectedDiseaseTrigger = "Brown Plant Hopper (High), Fall Armyworm (High)",
                IconClass = "bi-sun-fill"
            },
            new WeatherScenarioPreset
            {
                Id = "dry-clear",
                Name = "Dry & Clear Weather (স্বাভাবিক / নিম্ন ঝুঁকি)",
                BanglaName = "শুষ্ক ও পরিষ্কার আবহাওয়া (স্বাভাবিক)",
                Temperature = 25.0,
                Humidity = 50.0,
                Condition = "Clear & Dry Skies",
                BanglaCondition = "পরিষ্কার রৌদ্রোজ্জ্বল ও শুষ্ক",
                Description = "Low humidity (50%) and sunny skies — unfavorable for fungal spore propagation. Low disease risk.",
                BanglaDescription = "৫০% কম আর্দ্রতা ও রোদ ঝলমলে আকাশ ছত্রাকের বংশবৃদ্ধির প্রতিকূল। রোগবালাইয়ের ঝুঁকি কম।",
                ExpectedDiseaseTrigger = "Low / No Critical Disease Risks",
                IconClass = "bi-brightness-high-fill"
            }
        };

        public Task<List<PestDiseaseRule>> GetAllDiseaseRulesAsync()
        {
            return Task.FromResult(DiseaseRules.ToList());
        }

        public async Task<RegionalWeatherForecast> GetRegionalWeatherAsync(string district)
        {
            return await _weatherService.GetForecastAsync(district);
        }

        public Task<List<EvaluatedPestAlert>> EvaluateAlertsAsync(RegionalWeatherForecast weather, string? crop = null)
        {
            var matchedAlerts = new List<EvaluatedPestAlert>();

            foreach (var rule in DiseaseRules)
            {
                // 1. Check Crop filter if specified
                if (!string.IsNullOrWhiteSpace(crop) && !crop.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    bool cropMatches = rule.TargetCrops.Any(tc =>
                        tc.Contains(crop, StringComparison.OrdinalIgnoreCase) ||
                        crop.Contains(tc, StringComparison.OrdinalIgnoreCase));

                    if (!cropMatches) continue;
                }

                // 2. Check Temperature Predicate
                bool tempMatches = weather.Temperature >= rule.MinTemp && weather.Temperature <= rule.MaxTemp;

                // 3. Check Humidity Predicate
                bool humidityMatches = weather.Humidity >= rule.MinHumidity &&
                                       (!rule.MaxHumidity.HasValue || weather.Humidity <= rule.MaxHumidity.Value);

                // 4. Consecutive days condition evaluation across 7-day telemetry
                bool consecutiveDaysMet = true;
                if (rule.RequiresConsecutiveDays && weather.FiveDayForecast != null && weather.FiveDayForecast.Count >= 2)
                {
                    int matchingConsecutiveDays = 0;
                    int maxConsecutive = 0;
                    foreach (var day in weather.FiveDayForecast)
                    {
                        bool dayTemp = day.MaxTemp <= rule.MaxTemp + 4 && day.MinTemp >= rule.MinTemp - 4;
                        bool dayHum = day.Humidity >= rule.MinHumidity - 5;
                        bool dayCond = rule.MatchingWeatherConditions.Any(c => day.Condition.Contains(c, StringComparison.OrdinalIgnoreCase));

                        if ((dayTemp && dayHum) || dayCond)
                        {
                            matchingConsecutiveDays++;
                            if (matchingConsecutiveDays > maxConsecutive) maxConsecutive = matchingConsecutiveDays;
                        }
                        else
                        {
                            matchingConsecutiveDays = 0;
                        }
                    }
                    consecutiveDaysMet = maxConsecutive >= 2;
                }

                // Trigger alert if core thresholds match or multi-day consecutive threat is detected
                if ((tempMatches && humidityMatches) || (rule.RequiresConsecutiveDays && consecutiveDaysMet))
                {
                    double riskScore = CalculateRiskPercentage(weather, rule);
                    if (rule.RequiresConsecutiveDays && consecutiveDaysMet)
                    {
                        riskScore = Math.Min(98.0, riskScore + 10.0);
                    }

                    string triggerExp = rule.RequiresConsecutiveDays && consecutiveDaysMet
                        ? $"Triggered by multi-day overcast/fog conditions with {weather.Temperature:0.#}°C temp and {weather.Humidity:0.#}% humidity in {weather.District}."
                        : $"Triggered by {weather.Temperature:0.#}°C temperature and {weather.Humidity:0.#}% relative humidity in {weather.District}.";

                    string banglaTriggerExp = rule.RequiresConsecutiveDays && consecutiveDaysMet
                        ? $"{weather.District} জেলায় টানা কয়েক দিনের মেঘলা/কুয়াশাচ্ছন্ন আবহাওয়া, {weather.Temperature:0.#}° সে. তাপমাত্রা ও {weather.Humidity:0.#}% আর্দ্রতার কারণে উচ্চ ঝুঁকি বিদ্যমান।"
                        : $"{weather.District} জেলায় {weather.Temperature:0.#}° সে. তাপমাত্রা এবং {weather.Humidity:0.#}% আর্দ্রতার কারণে এই রোগ/পোকার ঝুঁকি তৈরি হয়েছে।";

                    matchedAlerts.Add(new EvaluatedPestAlert
                    {
                        RuleId = rule.Id,
                        DiseaseName = rule.DiseaseName,
                        BanglaName = rule.BanglaName,
                        PathogenOrPest = rule.PathogenOrPest,
                        Category = rule.Category,
                        TargetCrops = rule.TargetCrops,
                        Severity = (rule.RequiresConsecutiveDays && consecutiveDaysMet) ? "Critical" : rule.Severity,
                        Symptoms = rule.Symptoms,
                        BanglaSymptoms = rule.BanglaSymptoms,
                        TriggerExplanation = triggerExp,
                        BanglaTriggerExplanation = banglaTriggerExp,
                        ActionableRemedies = rule.ActionableRemedies,
                        BanglaActionableRemedies = rule.BanglaActionableRemedies,
                        PreventiveSpray = rule.PreventiveSpray,
                        OrganicControl = rule.OrganicControl,
                        PreventiveSprayBn = rule.PreventiveSprayBn,
                        OrganicControlBn = rule.OrganicControlBn,
                        Source = rule.Source,
                        IconClass = rule.IconClass,
                        BadgeClass = rule.BadgeClass,
                        RiskPercentage = riskScore
                    });
                }
            }

            // Sort by Severity: Critical -> High -> Moderate
            var sorted = matchedAlerts
                .OrderBy(a => a.Severity == "Critical" ? 0 : (a.Severity == "High" ? 1 : 2))
                .ThenByDescending(a => a.RiskPercentage)
                .ToList();

            return Task.FromResult(sorted);
        }

        public async Task<PestAlertsIndexViewModel> GetPestAlertsDashboardAsync(
            string? district = null,
            string? crop = null,
            double? customTemp = null,
            double? customHumidity = null,
            string? customCondition = null)
        {
            // What-if simulations are never cached; the real forecast view is, for as long as the forecast holds (ADV-09).
            if (_cache is null || customTemp.HasValue || customHumidity.HasValue || !string.IsNullOrWhiteSpace(customCondition))
                return await BuildDashboardAsync(district, crop, customTemp, customHumidity, customCondition);
            var key = $"advisory:pests:{district}|{crop}|{BangladeshClock.Today:yyyyMMdd}";
            if (!_cache.TryGetValue(key, out PestAlertsIndexViewModel? cached) || cached is null)
            {
                cached = await BuildDashboardAsync(district, crop, null, null, null);
                _cache.Set(key, cached, WeatherSuggestionService.CacheFor);
            }
            return cached;
        }

        private async Task<PestAlertsIndexViewModel> BuildDashboardAsync(string? district, string? crop, double? customTemp, double? customHumidity, string? customCondition)
        {
            string distName = string.IsNullOrWhiteSpace(district) ? "Bogra" : district;
            var baseWeather = await GetRegionalWeatherAsync(distName);

            string safeCrop = "All";
            if (!string.IsNullOrWhiteSpace(crop))
            {
                var match = GetCropFilterOptions().FirstOrDefault(c => c.Value.Equals(crop.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match != null) safeCrop = match.Value;
            }

            string? safeCondition = null;
            if (!string.IsNullOrWhiteSpace(customCondition))
            {
                var match = Presets.FirstOrDefault(p => p.Condition.Equals(customCondition.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match != null) safeCondition = match.Condition;
            }

            bool isSimulated = customTemp.HasValue || customHumidity.HasValue || !string.IsNullOrWhiteSpace(safeCondition);

            var activeWeather = new RegionalWeatherForecast
            {
                District = baseWeather.District,
                Division = baseWeather.Division,
                Temperature = customTemp ?? baseWeather.Temperature,
                MinTemp = customTemp.HasValue ? customTemp.Value - 4 : baseWeather.MinTemp,
                MaxTemp = customTemp.HasValue ? customTemp.Value + 4 : baseWeather.MaxTemp,
                Humidity = customHumidity ?? baseWeather.Humidity,
                RainProbability = baseWeather.RainProbability,
                WindSpeedKmh = baseWeather.WindSpeedKmh,
                Condition = safeCondition ?? baseWeather.Condition,
                BanglaCondition = safeCondition != null ? (Presets.FirstOrDefault(p => p.Condition == safeCondition)?.BanglaCondition ?? safeCondition) : baseWeather.BanglaCondition,
                ConditionIcon = baseWeather.ConditionIcon,
                ForecastDate = DateTime.Today,
                FiveDayForecast = baseWeather.FiveDayForecast
            };

            var alerts = await EvaluateAlertsAsync(activeWeather, safeCrop);

            return new PestAlertsIndexViewModel
            {
                SelectedDistrict = activeWeather.District,
                SelectedCrop = safeCrop,
                IsCustomSimulated = isSimulated,
                CustomTemp = customTemp,
                CustomHumidity = customHumidity,
                CustomCondition = safeCondition,
                Weather = activeWeather,
                ActiveAlerts = alerts,
                AllRulesEncyclopedia = DiseaseRules.ToList(),
                Districts = GetDistrictOptions(),
                Crops = GetCropFilterOptions(),
                PresetScenarios = Presets
            };
        }

        public async Task<WeatherNoteItem?> GetWeatherAlertNoteAsync(string location)
        {
            var weather = await GetRegionalWeatherAsync(location);
            var alerts = await EvaluateAlertsAsync(weather);

            if (alerts.Any())
            {
                var top = alerts.First();
                return new WeatherNoteItem
                {
                    Title = $"{top.DiseaseName} Risk in {weather.District} ({weather.Temperature:0.#}°C, {weather.Humidity:0.#}% Humidity)",
                    Message = $"{top.TriggerExplanation} Symptoms: {top.Symptoms} Primary Action: {top.ActionableRemedies.FirstOrDefault()}",
                    IconClass = top.IconClass,
                    BadgeText = $"{top.Severity} Disease Risk"
                };
            }

            return new WeatherNoteItem
            {
                Title = $"Normal Weather Conditions in {weather.District}",
                Message = $"Current Temperature is {weather.Temperature:0.#}°C with {weather.Humidity:0.#}% humidity. No critical agrometeorological disease outbreaks detected for today.",
                IconClass = "bi-sun-fill",
                BadgeText = "Weather Advisory"
            };
        }

        private static double CalculateRiskPercentage(RegionalWeatherForecast w, PestDiseaseRule r)
        {
            double tempCenter = (r.MinTemp + r.MaxTemp) / 2.0;
            double tempSpan = Math.Max(1.0, (r.MaxTemp - r.MinTemp) / 2.0);
            double tempScore = Math.Clamp(1.0 - (Math.Abs(w.Temperature - tempCenter) / tempSpan), 0.0, 1.0);

            double humExcess = Math.Max(0.0, w.Humidity - r.MinHumidity);
            double humScore = Math.Clamp(0.70 + (humExcess / 30.0) * 0.30, 0.70, 1.0);

            double baseScore = (tempScore * 0.40 + humScore * 0.60) * 100.0;

            // Ensure critical/high alerts reflect urgent threshold breaches
            if (r.Severity == "Critical")
            {
                baseScore = Math.Max(80.0, baseScore);
            }
            else if (r.Severity == "High")
            {
                baseScore = Math.Max(70.0, baseScore);
            }

            return Math.Round(Math.Clamp(baseScore, 60.0, 98.0), 0);
        }

        private static List<DailyForecastEntry> Generate5DayForecast(double baseTemp, double baseHum, string baseCond, string icon)
        {
            var list = new List<DailyForecastEntry>();
            var today = DateTime.Today;

            string[] dayNames = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
            string[] banglaDays = { "রবি", "সোম", "মঙ্গল", "বুধ", "বৃহস্পতি", "শুক্র", "শনি" };

            for (int i = 0; i < 5; i++)
            {
                var d = today.AddDays(i);
                int dayOfWeek = (int)d.DayOfWeek;
                list.Add(new DailyForecastEntry
                {
                    Date = d,
                    DayName = i == 0 ? "Today" : dayNames[dayOfWeek],
                    BanglaDayName = i == 0 ? "আজ" : banglaDays[dayOfWeek],
                    MaxTemp = Math.Round(baseTemp + (i % 2 == 0 ? 2 : 1), 0),
                    MinTemp = Math.Round(baseTemp - 4 + (i % 3 == 0 ? 1 : 0), 0),
                    Humidity = Math.Round(Math.Clamp(baseHum + (i * 2 - 3), 70, 98), 0),
                    RainChance = Math.Round(Math.Clamp(baseHum - 20 + (i * 5), 20, 90), 0),
                    Condition = baseCond,
                    ConditionIcon = icon
                });
            }

            return list;
        }

        private static List<SelectOptionItem> GetDistrictOptions() => new()
        {
            new("Bogra", "Bogra (Rajshahi Division)", "বগুড়া (রাজশাহী বিভাগ)"),
            new("Dinajpur", "Dinajpur (Rangpur Division)", "দিনাজপুর (রংপুর বিভাগ)"),
            new("Rangpur", "Rangpur (Rangpur Division)", "রংপুর (রংপুর বিভাগ)"),
            new("Rajshahi", "Rajshahi (Barind Tract)", "রাজশাহী (বরেন্দ্র অঞ্চল)"),
            new("Jessore", "Jessore (Khulna Division)", "যশোর (খুলনা বিভাগ)"),
            new("Barisal", "Barisal (Coastal Belt)", "বরিশাল (উপকূলীয় অঞ্চল)"),
            new("Mymensingh", "Mymensingh (Central Lowland)", "ময়মনসিংহ (ময়মনসিংহ বিভাগ)"),
            new("Comilla", "Comilla (Chittagong Division)", "কুমিল্লা (চট্টগ্রাম বিভাগ)"),
            new("Sylhet", "Sylhet (Haor Basin)", "সিলেট (হাওর অঞ্চল)"),
            new("Dhaka", "Dhaka (Central Plains)", "ঢাকা (ঢাকা বিভাগ)")
        };

        private static List<SelectOptionItem> GetCropFilterOptions() => new()
        {
            new("All", "All Crops & Plants", "সকল ফসল"),
            new("Rice", "Rice (Boro, Aman, Aus)", "ধান (বোরো, আমন, আউশ)"),
            new("Potato", "Potato (আলু)", "গোল আলু"),
            new("Wheat", "Wheat (গম)", "গম"),
            new("Maize", "Maize (ভুট্টা)", "হাইব্রিড ভুট্টা"),
            new("Mustard", "Mustard (সরিষা)", "সরিষা"),
            new("Tomato", "Tomato (টমেটো)", "টমেটো"),
            new("Brinjal", "Brinjal (বেগুন)", "বেগুন"),
            new("Chili", "Chili (মরিচ)", "মরিচ")
        };
    }
}
