using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KrishiLink.BLL.Services
{
    public interface IWeatherSuggestionService
    {
        Task<WeatherSuggestionViewModel?> GetProactiveSuggestionForFarmerAsync(string farmerId);
        Task<WeatherSuggestionViewModel> GenerateSuggestionForDistrictAndCropAsync(string district, string? cropName, int? month = null);
        Task<int> ProcessDailyFarmerSuggestionsAsync();
    }

    public class WeatherSuggestionService : IWeatherSuggestionService
    {
        private readonly IPestAlertService _pestAlertService;
        private readonly ICropCalendarService _cropCalendarService;
        private readonly INotificationService _notificationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IRepository<Notification> _notifications;
        private readonly ILogger<WeatherSuggestionService> _logger;

        public WeatherSuggestionService(
            IPestAlertService pestAlertService,
            ICropCalendarService cropCalendarService,
            INotificationService notificationService,
            UserManager<ApplicationUser> userManager,
            IRepository<Notification> notifications,
            ILogger<WeatherSuggestionService> logger)
        {
            _pestAlertService = pestAlertService;
            _cropCalendarService = cropCalendarService;
            _notificationService = notificationService;
            _userManager = userManager;
            _notifications = notifications;
            _logger = logger;
        }

        public async Task<WeatherSuggestionViewModel?> GetProactiveSuggestionForFarmerAsync(string farmerId)
        {
            if (string.IsNullOrWhiteSpace(farmerId)) return null;

            var farmer = await _userManager.FindByIdAsync(farmerId);
            if (farmer == null) return null;

            var district = farmer.District ?? OnboardingOptions.GuessDistrict(farmer.Location) ?? "Bogra";
            var crop = farmer.Specialization ?? "Rice (Boro)";

            var suggestion = await GenerateSuggestionForDistrictAndCropAsync(district, crop);
            suggestion.HasProfileCrop = !string.IsNullOrWhiteSpace(farmer.Specialization);
            suggestion.HasProfileDistrict = !string.IsNullOrWhiteSpace(farmer.District) || !string.IsNullOrWhiteSpace(farmer.Location);

            return suggestion;
        }

        public async Task<WeatherSuggestionViewModel> GenerateSuggestionForDistrictAndCropAsync(string district, string? cropName, int? month = null)
        {
            int currentMonth = month.HasValue && month.Value >= 1 && month.Value <= 12 ? month.Value : DateTime.Today.Month;
            string safeDistrict = string.IsNullOrWhiteSpace(district) ? "Bogra" : district.Trim();
            string rawCrop = string.IsNullOrWhiteSpace(cropName) ? "Rice (Boro)" : cropName.Trim();

            // 1. Fetch live 5-day weather telemetry for the district
            var weather = await _pestAlertService.GetRegionalWeatherAsync(safeDistrict);

            // 2. Evaluate rain forecast in the next 5 days
            bool rainAlert = false;
            double maxRainChance = weather.RainProbability;
            int rainWindowDays = 1;

            if (weather.RainProbability >= 45.0 || IsRainCondition(weather.Condition))
            {
                rainAlert = true;
                maxRainChance = Math.Max(maxRainChance, weather.RainProbability);
            }

            if (weather.FiveDayForecast != null && weather.FiveDayForecast.Any())
            {
                int dayIndex = 1;
                foreach (var day in weather.FiveDayForecast)
                {
                    if (day.RainChance >= 45.0 || IsRainCondition(day.Condition))
                    {
                        if (!rainAlert)
                        {
                            rainAlert = true;
                            rainWindowDays = dayIndex;
                        }
                        if (day.RainChance > maxRainChance)
                        {
                            maxRainChance = day.RainChance;
                        }
                    }
                    dayIndex++;
                }
            }

            // 3. Resolve Crop Details from Crop Calendar Reference Data
            var (matchedCropEntry, canonicalCropName, banglaCropName) = MatchCrop(rawCrop);
            string cropStage = "Growing";
            string banglaCropStage = "বাড়ন্ত পর্যায়";

            if (matchedCropEntry != null)
            {
                if (matchedCropEntry.HarvestingMonths.Contains(currentMonth))
                {
                    cropStage = "Harvesting";
                    banglaCropStage = "ফসল কাটার উপযুক্ত সময়";
                }
                else if (matchedCropEntry.SowingMonths.Contains(currentMonth))
                {
                    cropStage = "Sowing";
                    banglaCropStage = "বপন ও চারা রোপণ পর্ব";
                }
                else if (matchedCropEntry.GrowingMonths.Contains(currentMonth))
                {
                    cropStage = "Growing";
                    banglaCropStage = "গাছের দৈহিক বৃদ্ধি ও পরিপক্বতা পর্যায়";
                }
                else
                {
                    cropStage = "OffSeason";
                    banglaCropStage = "পরবর্তী মৌসুমের জন্য প্রস্তুতি";
                }
            }

            var suggestionItems = new List<WeatherSuggestionItem>();
            string overallRiskLevel = "Optimal";
            string overallBadgeClass = "success";
            string bannerTitle = $"Weather & Crop Advisory for {canonicalCropName} in {safeDistrict}";
            string banglaBannerTitle = $"{safeDistrict}-এ {banglaCropName} চাষে আবহাওয়া ও যন্ত্রপাতির পরামর্শ";
            string bannerMessage = $"Current conditions are normal for {canonicalCropName}. Track machinery and storage availability in {safeDistrict}.";
            string banglaBannerMessage = $"বর্তমান আবহাওয়ায় {banglaCropName}-এর চাষাবাদ স্বাভাবিক। {safeDistrict}-এ আধুনিক যন্ত্রপাতি ও গুদামের তথ্য জেনে রাখুন।";

            // 4. Generate Targeted Rules
            if (cropStage == "Harvesting")
            {
                if (rainAlert)
                {
                    overallRiskLevel = "Urgent";
                    overallBadgeClass = "danger";
                    bannerTitle = $"⚠️ Rain Forecasted During Your {canonicalCropName} Harvest in {safeDistrict}!";
                    banglaBannerTitle = $"⚠️ {safeDistrict}-এ {banglaCropName} তোলার মৌসুমে বৃষ্টির পূর্বাভাস!";
                    bannerMessage = $"Forecast indicates {maxRainChance:0}% rain probability over the next {Math.Max(1, rainWindowDays)}–3 days. Secure your harvested produce from moisture damage, discoloration, and mold by booking storage now.";
                    banglaBannerMessage = $"আগামী {Math.Max(1, rainWindowDays)}–৩ দিনের মধ্যে {safeDistrict}-এ {maxRainChance:0}% বৃষ্টির পূর্বাভাস রয়েছে। পাকা ফসল ভিজে ক্ষতি ও ছত্রাক আক্রমণ ঠেকাতে এখনই গুদামে জায়গা নিশ্চিত করুন।";

                    // Nudge 1: Storage Protection
                    suggestionItems.Add(new WeatherSuggestionItem
                    {
                        Id = "storage-urgent",
                        NudgeType = "StorageProtection",
                        Title = $"Reserve Storage in {safeDistrict} for {canonicalCropName}",
                        BanglaTitle = $"{safeDistrict}-এ {banglaCropName} সংরক্ষণে গুদাম বুকিং করুন",
                        Message = $"Heavy rain or moisture can ruin fresh harvest quality. Book dry storage or cold storage capacity in {safeDistrict} before rains start.",
                        BanglaMessage = $"বৃষ্টির পানিতে ভেজা ফসল নষ্ট হয়ে মূল্য কমে যেতে পারে। বৃষ্টি শুরুর আগেই {safeDistrict}-এ নিকটস্থ গুদাম বা কোল্ড স্টোরেজে জায়গা বুক করুন।",
                        ActionUrl = $"/Godown?location={Uri.EscapeDataString(safeDistrict)}",
                        ActionText = $"Reserve Storage in {safeDistrict}",
                        BanglaActionText = "গুদাম বা হিমাগারে জায়গা খুঁজুন",
                        ActionIcon = "bi-building-check",
                        BadgeClass = "danger",
                        BadgeText = "Urgent Storage Action",
                        BanglaBadgeText = "জরুরি গুদাম ব্যবস্থা",
                        IconClass = "bi-shield-fill-exclamation",
                        IsCritical = true,
                        Priority = 1
                    });

                    // Nudge 2: Rapid Mechanical Harvesting
                    suggestionItems.Add(new WeatherSuggestionItem
                    {
                        Id = "harvester-urgent",
                        NudgeType = "FastHarvesting",
                        Title = $"Rent Combine Harvester to Beat the Rain",
                        BanglaTitle = $"বৃষ্টির আগেই দ্রুত ফসল কাটতে কম্বাইন হারভেস্টার ভাড়া নিন",
                        Message = $"Finish harvesting days faster with an automated combine harvester or reaper before bad weather sets in.",
                        BanglaMessage = $"বৃষ্টিতে পাকা ধান বা ফসল মাটিতে শুয়ে পড়ার আগেই কম্বাইন হারভেস্টার দিয়ে কয়েক ঘণ্টার মধ্যে দ্রুত ফসল কেটে নিন।",
                        ActionUrl = $"/Equipment?location={Uri.EscapeDataString(safeDistrict)}&category={Uri.EscapeDataString("Combine Harvester")}",
                        ActionText = $"Find Harvesters in {safeDistrict}",
                        BanglaActionText = "নিকটস্থ হারভেস্টার খুঁজুন",
                        ActionIcon = "bi-truck",
                        BadgeClass = "warning",
                        BadgeText = "Fast Harvesting",
                        BanglaBadgeText = "দ্রুত ফসল সংগ্রহ",
                        IconClass = "bi-gear-wide-connected",
                        IsCritical = true,
                        Priority = 2
                    });
                }
                else
                {
                    overallRiskLevel = "Optimal";
                    overallBadgeClass = "success";
                    bannerTitle = $"☀️ Ideal Sunny Harvesting Window for {canonicalCropName} in {safeDistrict}";
                    banglaBannerTitle = $"☀️ {safeDistrict}-এ {banglaCropName} কাটার মোক্ষম রৌদ্রোজ্জ্বল আবহাওয়া";
                    bannerMessage = $"Clear and dry weather expected in {safeDistrict}. Take advantage of sunny days to complete harvesting and sun-drying.";
                    banglaBannerMessage = $"{safeDistrict}-এ রোদ ঝলমলে অনুকূল আবহাওয়া বিদ্যমান। এ সুযোগে ফসল কাটা ও রোদে শুকানোর কাজ দ্রুত সম্পন্ন করুন।";

                    suggestionItems.Add(new WeatherSuggestionItem
                    {
                        Id = "storage-prep",
                        NudgeType = "StorageProtection",
                        Title = $"Plan Post-Harvest Storage in {safeDistrict}",
                        BanglaTitle = $"{safeDistrict}-এ ফসল পরবর্তী গুদাম সংরক্ষণ নিশ্চিত করুন",
                        Message = $"Plan ahead to store dried {canonicalCropName} safely in certified grain warehouses or ventilated godowns.",
                        BanglaMessage = $"শুকানো {banglaCropName} নিরাপদ রাখতে নিকটস্থ অনুমোদিত গুদামে অগ্রিম বুকিং দিয়ে রাখুন।",
                        ActionUrl = $"/Godown?location={Uri.EscapeDataString(safeDistrict)}",
                        ActionText = "Browse Storage Facilities",
                        BanglaActionText = "গুদাম তালিকা দেখুন",
                        ActionIcon = "bi-building",
                        BadgeClass = "info",
                        BadgeText = "Recommended",
                        BanglaBadgeText = "পরামর্শ",
                        IconClass = "bi-box-seam",
                        IsCritical = false,
                        Priority = 2
                    });
                }
            }
            else if (cropStage == "Sowing")
            {
                overallRiskLevel = "Advisory";
                overallBadgeClass = "info";
                bannerTitle = $"🌱 Prime Sowing & Land Prep Window for {canonicalCropName}";
                banglaBannerTitle = $"🌱 {safeDistrict}-এ {banglaCropName} বপন ও জমি প্রস্তুতের উপযুক্ত সময়";
                bannerMessage = $"{DateTime.Today:MMMM} is the prime agricultural window for {canonicalCropName} in {safeDistrict}. Ensure fine seedbed tilth and timely seeding.";
                banglaBannerMessage = $"{DateTime.Today:MMMM} মাস {safeDistrict}-এ {banglaCropName} বীজ বপন ও জমি প্রস্তুতির প্রধান সময়। সময়মতো জমি চাষ ও সেচ ব্যবস্থা নিশ্চিত করুন।";

                suggestionItems.Add(new WeatherSuggestionItem
                {
                    Id = "tillage-seeding",
                    NudgeType = "TillageIrrigation",
                    Title = $"Rent Power Tiller or Tractor for Seedbed Prep",
                    BanglaTitle = $"জমি তৈরির জন্য পাওয়ার টিলার বা ট্রাক্টর ভাড়া নিন",
                    Message = $"Achieve ideal soil pulverized tilth before sowing by renting modern tillage machinery from nearby owners.",
                    BanglaMessage = $"বীজ গজানোর উপযুক্ত মাটি তৈরি করতে নিকটস্থ মালিকদের কাছ থেকে পাওয়ার টিলার বা ট্রাক্টর ভাড়া নিন।",
                    ActionUrl = $"/Equipment?location={Uri.EscapeDataString(safeDistrict)}&category={Uri.EscapeDataString("Power Tiller")}",
                    ActionText = $"Find Tillage Equipment",
                    BanglaActionText = "চাষের যন্ত্রপাতি খুঁজুন",
                    ActionIcon = "bi-tools",
                    BadgeClass = "success",
                    BadgeText = "Sowing Season",
                    BanglaBadgeText = "বপন মৌসুম",
                    IconClass = "bi-seedling",
                    IsCritical = false,
                    Priority = 1
                });

                if (weather.RainProbability < 20.0 && weather.Humidity < 65.0)
                {
                    suggestionItems.Add(new WeatherSuggestionItem
                    {
                        Id = "irrigation-pump",
                        NudgeType = "TillageIrrigation",
                        Title = $"Rent Irrigation Pump for Pre-Sowing Moisture",
                        BanglaTitle = $"জমিতে রসের জন্য সেচ পাম্প ভাড়া নিন",
                        Message = $"Dry weather forecasted in {safeDistrict}. Apply light pre-sowing irrigation to ensure uniform germination.",
                        BanglaMessage = $"শুষ্ক আবহাওয়ায় বীজের সমহারে অঙ্কুরোদগম নিশ্চিত করতে হালকা সেচের জন্য সেচ পাম্প ভাড়া নিন।",
                        ActionUrl = $"/Equipment?location={Uri.EscapeDataString(safeDistrict)}&category={Uri.EscapeDataString("Irrigation Pump")}",
                        ActionText = $"Find Irrigation Pumps",
                        BanglaActionText = "সেচ পাম্প খুঁজুন",
                        ActionIcon = "bi-droplet-fill",
                        BadgeClass = "info",
                        BadgeText = "Soil Moisture",
                        BanglaBadgeText = "মাটির আর্দ্রতা",
                        IconClass = "bi-droplet-half",
                        IsCritical = false,
                        Priority = 2
                    });
                }
            }
            else // Growing / Vegetative stage
            {
                // Check disease alerts
                var diseaseAlerts = await _pestAlertService.EvaluateAlertsAsync(weather, canonicalCropName);
                if (diseaseAlerts.Any(a => a.Severity == "Critical" || a.Severity == "High"))
                {
                    var topAlert = diseaseAlerts.First(a => a.Severity == "Critical" || a.Severity == "High");
                    overallRiskLevel = "High";
                    overallBadgeClass = "warning";
                    bannerTitle = $"⚠️ {topAlert.DiseaseName} Risk Alert for {canonicalCropName} in {safeDistrict}";
                    banglaBannerTitle = $"⚠️ {safeDistrict}-এ {banglaCropName}-এ {topAlert.BanglaName} রোগের ঝুঁকি!";
                    bannerMessage = $"Current humidity ({weather.Humidity:0}%) and temperature ({weather.Temperature:0.#}°C) match high disease risk for {canonicalCropName}. Take preventive action immediately.";
                    banglaBannerMessage = $"বর্তমান আর্দ্রতা ({weather.Humidity:0}%) ও তাপমাত্রা ({weather.Temperature:0.#}° সে.) {topAlert.BanglaName} বিস্তারের অনুকূল। অবিলম্বে আগাম সুরক্ষা ব্যবস্থা নিন।";

                    suggestionItems.Add(new WeatherSuggestionItem
                    {
                        Id = "disease-prevention",
                        NudgeType = "DiseasePrevention",
                        Title = $"Apply Prophylactic Spray for {topAlert.DiseaseName}",
                        BanglaTitle = $"{topAlert.BanglaName} প্রতিরোধে স্প্রে প্রয়োগ করুন",
                        Message = $"Spray recommended treatment: {topAlert.PreventiveSpray}. Inspect plant tillers and leaf undersides daily.",
                        BanglaMessage = $"প্রস্তাবিত প্রতিষেধক বা ছত্রাকনাশক স্প্রে করুন। প্রতিদিন গাছের গোড়া ও পাতার নিচের অংশ পর্যবেক্ষণ করুন।",
                        ActionUrl = $"/Advisory/Alerts?district={Uri.EscapeDataString(safeDistrict)}&crop={Uri.EscapeDataString(canonicalCropName)}",
                        ActionText = "View Disease Remedies & Guide",
                        BanglaActionText = "রোগের প্রতিকার ও সমাধান দেখুন",
                        ActionIcon = "bi-shield-exclamation",
                        BadgeClass = topAlert.BadgeClass,
                        BadgeText = $"{topAlert.Severity} Risk",
                        BanglaBadgeText = "রোগ ঝুঁকি",
                        IconClass = topAlert.IconClass,
                        IsCritical = true,
                        Priority = 1
                    });

                    suggestionItems.Add(new WeatherSuggestionItem
                    {
                        Id = "power-sprayer",
                        NudgeType = "EquipmentRental",
                        Title = $"Rent Power Sprayer for Uniform Foliar Application",
                        BanglaTitle = $"কার্যকর ওষুধ স্প্রে করার জন্য পাওয়ার স্প্রেয়ার ভাড়া নিন",
                        Message = $"Ensure quick and complete canopy coverage across your fields using a high-pressure Power Sprayer.",
                        BanglaMessage = $"পুরো জমিতে দ্রুত ও সমহারে স্প্রে করতে উচ্চ ক্ষমতাসম্পন্ন পাওয়ার স্প্রেয়ার ভাড়া নিন।",
                        ActionUrl = $"/Equipment?location={Uri.EscapeDataString(safeDistrict)}&category={Uri.EscapeDataString("Power Sprayer")}",
                        ActionText = "Find Power Sprayers",
                        BanglaActionText = "স্প্রেয়ার খুঁজুন",
                        ActionIcon = "bi-spraycan",
                        BadgeClass = "info",
                        BadgeText = "Equipment",
                        BanglaBadgeText = "যন্ত্রপাতি",
                        IconClass = "bi-shield-check",
                        IsCritical = false,
                        Priority = 2
                    });
                }
                else
                {
                    overallRiskLevel = "Optimal";
                    overallBadgeClass = "success";
                    bannerTitle = $"🌾 Active Vegetative Growth Stage for {canonicalCropName}";
                    banglaBannerTitle = $"🌾 {safeDistrict}-এ {banglaCropName}-এর সন্তোষজনক বৃদ্ধি পর্যায়";
                    bannerMessage = $"Weather conditions in {safeDistrict} are stable. Maintain weed-free plots and standard nutrition splits.";
                    banglaBannerMessage = $"{safeDistrict}-এ আবহাওয়া অনুকূল রয়েছে। সময়মতো নিড়ানি ও সুষম সার প্রয়োগ বজায় রাখুন।";

                    suggestionItems.Add(new WeatherSuggestionItem
                    {
                        Id = "crop-guide",
                        NudgeType = "CropGuidance",
                        Title = $"Explore Full Agrometeorological Advisory",
                        BanglaTitle = $"সার ও বালাইনাশক নির্দেশিকা দেখুন",
                        Message = $"Review official DAE fertilizer, irrigation, and pest management schedules for {canonicalCropName}.",
                        BanglaMessage = $"{banglaCropName} চাষের পূর্ণাঙ্গ সার, সেচ ও নিড়ানি ব্যবস্থাপনা গাইড দেখে নিন।",
                        ActionUrl = $"/Advisory/Calendar?search={Uri.EscapeDataString(canonicalCropName)}",
                        ActionText = "View Crop Timeline Guide",
                        BanglaActionText = "ফসলের ক্যালেন্ডার দেখুন",
                        ActionIcon = "bi-calendar-check",
                        BadgeClass = "success",
                        BadgeText = "Good Condition",
                        BanglaBadgeText = "অনুকূল অবস্থা",
                        IconClass = "bi-info-circle-fill",
                        IsCritical = false,
                        Priority = 3
                    });
                }
            }

            return new WeatherSuggestionViewModel
            {
                District = safeDistrict,
                Division = weather.Division,
                FarmerCrop = canonicalCropName,
                BanglaCrop = banglaCropName,
                CropStage = cropStage,
                BanglaCropStage = banglaCropStage,
                CurrentMonth = currentMonth,
                CurrentMonthName = DateTime.Today.ToString("MMMM"),
                Weather = weather,
                RainAlert = rainAlert,
                MaxRainChance = maxRainChance,
                RainWindowDays = rainWindowDays,
                OverallRiskLevel = overallRiskLevel,
                OverallBadgeClass = overallBadgeClass,
                BannerTitle = bannerTitle,
                BanglaBannerTitle = banglaBannerTitle,
                BannerMessage = bannerMessage,
                BanglaBannerMessage = banglaBannerMessage,
                Suggestions = suggestionItems.OrderBy(s => s.Priority).ToList()
            };
        }

        public async Task<int> ProcessDailyFarmerSuggestionsAsync()
        {
            try
            {
                var farmers = await _userManager.Users
                    .Where(u => u.UserRole == AppRoles.Farmer)
                    .ToListAsync();

                int generatedCount = 0;
                var recentCutoff = DateTime.UtcNow.AddDays(-5); // Anti-spam: max 1 suggestion notification per 5 days per user

                foreach (var farmer in farmers)
                {
                    var district = farmer.District ?? OnboardingOptions.GuessDistrict(farmer.Location);
                    if (string.IsNullOrWhiteSpace(district)) continue;

                    var crop = farmer.Specialization ?? "Rice (Boro)";
                    var suggestion = await GenerateSuggestionForDistrictAndCropAsync(district, crop);

                    var criticalNudge = suggestion.Suggestions.FirstOrDefault(s => s.IsCritical);
                    if (criticalNudge == null) continue;

                    // Check if a WeatherSuggestion notification was already sent to this farmer recently
                    var hasRecent = await _notifications.Query()
                        .AnyAsync(n => n.UserId == farmer.Id 
                                    && n.Type == NotificationTypes.WeatherSuggestion 
                                    && n.CreatedAt >= recentCutoff);

                    if (!hasRecent)
                    {
                        await _notificationService.CreateNotificationAsync(
                            farmer.Id,
                            criticalNudge.Title,
                            criticalNudge.Message,
                            criticalNudge.ActionUrl,
                            NotificationTypes.WeatherSuggestion,
                            sendEmail: true,
                            recipientEmail: farmer.Email
                        );
                        generatedCount++;
                    }
                }

                _logger.LogInformation("Processed daily proactive weather suggestions. Generated {Count} new notifications.", generatedCount);
                return generatedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing daily farmer weather suggestions.");
                return 0;
            }
        }

        private static bool IsRainCondition(string? condition)
        {
            if (string.IsNullOrWhiteSpace(condition)) return false;
            var c = condition.ToLowerInvariant();
            return c.Contains("rain") || c.Contains("shower") || c.Contains("drizzle") || c.Contains("storm") || c.Contains("বৃষ্টি");
        }

        private (CropCalendarEntry? Entry, string CanonicalName, string BanglaName) MatchCrop(string userCrop)
        {
            var crops = _cropCalendarService.GetAllCropsAsync().Result;
            var clean = userCrop.Trim();

            if (clean.Contains("Boro", StringComparison.OrdinalIgnoreCase))
            {
                var e = crops.FirstOrDefault(c => c.Name.Contains("Boro"));
                return (e, "Boro Rice", "বোরো ধান");
            }
            if (clean.Contains("Aman", StringComparison.OrdinalIgnoreCase))
            {
                var e = crops.FirstOrDefault(c => c.Name.Contains("Aman"));
                return (e, "T. Aman Rice", "আমন ধান");
            }
            if (clean.Contains("Aus", StringComparison.OrdinalIgnoreCase))
            {
                var e = crops.FirstOrDefault(c => c.Name.Contains("Aus"));
                return (e, "Aus Rice", "আউশ ধান");
            }
            if (clean.Contains("Potato", StringComparison.OrdinalIgnoreCase))
            {
                var e = crops.FirstOrDefault(c => c.Name.Contains("Potato"));
                return (e, "Potato", "গোল আলু");
            }
            if (clean.Contains("Wheat", StringComparison.OrdinalIgnoreCase))
            {
                var e = crops.FirstOrDefault(c => c.Name.Contains("Wheat"));
                return (e, "Wheat", "গম");
            }
            if (clean.Contains("Maize", StringComparison.OrdinalIgnoreCase) || clean.Contains("Corn", StringComparison.OrdinalIgnoreCase))
            {
                var e = crops.FirstOrDefault(c => c.Name.Contains("Maize"));
                return (e, "Hybrid Maize", "ভুট্টা");
            }
            if (clean.Contains("Jute", StringComparison.OrdinalIgnoreCase))
            {
                var e = crops.FirstOrDefault(c => c.Name.Contains("Jute"));
                return (e, "Jute", "পাট");
            }
            if (clean.Contains("Mustard", StringComparison.OrdinalIgnoreCase) || clean.Contains("Oilseed", StringComparison.OrdinalIgnoreCase))
            {
                var e = crops.FirstOrDefault(c => c.Name.Contains("Mustard"));
                return (e, "Mustard & Oilseeds", "সরিষা ও তৈলবীজ");
            }
            if (clean.Contains("Vegetable", StringComparison.OrdinalIgnoreCase) || clean.Contains("Tomato", StringComparison.OrdinalIgnoreCase))
            {
                var e = crops.FirstOrDefault(c => c.Name.Contains("Tomato"));
                return (e, "Winter Vegetables & Tomato", "শাকসবজি ও টমেটো");
            }
            if (clean.Contains("Pulse", StringComparison.OrdinalIgnoreCase) || clean.Contains("Lentil", StringComparison.OrdinalIgnoreCase))
            {
                var e = crops.FirstOrDefault(c => c.Name.Contains("Lentil"));
                return (e, "Pulses (Lentil)", "মসুর ও ডাল");
            }
            if (clean.Contains("Spice", StringComparison.OrdinalIgnoreCase) || clean.Contains("Onion", StringComparison.OrdinalIgnoreCase))
            {
                var e = crops.FirstOrDefault(c => c.Name.Contains("Onion"));
                return (e, "Onion & Spices", "পেঁয়াজ ও মসলা");
            }

            var fallback = crops.FirstOrDefault(c => c.Name.Contains(clean, StringComparison.OrdinalIgnoreCase)) ?? crops[0];
            return (fallback, fallback.Name, fallback.BanglaName);
        }
    }
}
