using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KrishiLink.BLL.Helpers;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace KrishiLink.BLL.Services
{
    public interface ICropCalendarService
    {
        Task<CropCalendarIndexViewModel> GetCalendarModelAsync(
            string? search = null,
            string? category = null,
            string? season = null,
            string? division = null,
            int? month = null,
            string? stage = null);

        Task<CropCalendarEntry?> GetCropByIdAsync(int id);
        Task<List<CropCalendarEntry>> GetAllCropsAsync();
        Task<FarmerCropAdvisoryViewModel?> GetRecommendationForFarmerAsync(string farmerId, int? month = null);
        Task<FarmerCropAdvisoryViewModel?> GetRecommendationForCropAsync(string? cropName, string? district, int? month = null);
    }

    public class CropCalendarService : ICropCalendarService
    {
        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHostEnvironment _environment;

        private const string CacheKeyAllCrops = "CropCalendar:All";

        public CropCalendarService(
            ApplicationDbContext db,
            IMemoryCache cache,
            UserManager<ApplicationUser> userManager,
            IHostEnvironment environment)
        {
            _db = db;
            _cache = cache;
            _userManager = userManager;
            _environment = environment;
        }

        private static readonly List<MonthTimelineHeader> MonthHeaders = new()
        {
            new MonthTimelineHeader { MonthNumber = 1, EnglishShort = "Jan", EnglishFull = "January", BanglaName = "জানুয়ারি", BanglaMonthApprox = "পৌষ – মাঘ" },
            new MonthTimelineHeader { MonthNumber = 2, EnglishShort = "Feb", EnglishFull = "February", BanglaName = "ফেব্রুয়ারি", BanglaMonthApprox = "মাঘ – ফাল্গুন" },
            new MonthTimelineHeader { MonthNumber = 3, EnglishShort = "Mar", EnglishFull = "March", BanglaName = "মার্চ", BanglaMonthApprox = "ফাল্গুন – চৈত্র" },
            new MonthTimelineHeader { MonthNumber = 4, EnglishShort = "Apr", EnglishFull = "April", BanglaName = "এপ্রিল", BanglaMonthApprox = "চৈত্র – বৈশাখ" },
            new MonthTimelineHeader { MonthNumber = 5, EnglishShort = "May", EnglishFull = "May", BanglaName = "মে", BanglaMonthApprox = "বৈশাখ – জ্যৈষ্ঠ" },
            new MonthTimelineHeader { MonthNumber = 6, EnglishShort = "Jun", EnglishFull = "June", BanglaName = "জুন", BanglaMonthApprox = "জ্যৈষ্ঠ – আষাঢ়" },
            new MonthTimelineHeader { MonthNumber = 7, EnglishShort = "Jul", EnglishFull = "July", BanglaName = "জুলাই", BanglaMonthApprox = "আষাঢ় – শ্রাবণ" },
            new MonthTimelineHeader { MonthNumber = 8, EnglishShort = "Aug", EnglishFull = "August", BanglaName = "আগস্ট", BanglaMonthApprox = "শ্রাবণ – ভাদ্র" },
            new MonthTimelineHeader { MonthNumber = 9, EnglishShort = "Sep", EnglishFull = "September", BanglaName = "সেপ্টেম্বর", BanglaMonthApprox = "ভাদ্র – আশ্বিন" },
            new MonthTimelineHeader { MonthNumber = 10, EnglishShort = "Oct", EnglishFull = "October", BanglaName = "অক্টোবর", BanglaMonthApprox = "আশ্বিন – কার্তিক" },
            new MonthTimelineHeader { MonthNumber = 11, EnglishShort = "Nov", EnglishFull = "November", BanglaName = "নভেম্বর", BanglaMonthApprox = "কার্তিক – অগ্রহায়ণ" },
            new MonthTimelineHeader { MonthNumber = 12, EnglishShort = "Dec", EnglishFull = "December", BanglaName = "ডিসেম্বর", BanglaMonthApprox = "অগ্রহায়ণ – পৌষ" }
        };

        public async Task<List<CropCalendarEntry>> GetAllCropsAsync()
        {
            if (_cache.TryGetValue(CacheKeyAllCrops, out List<CropCalendarEntry>? cached) && cached != null && cached.Count > 0)
            {
                return cached;
            }

            List<CropCalendarEntry> crops = new();
            try
            {
                crops = await _db.CropCalendarEntries.AsNoTracking().ToListAsync();
            }
            catch
            {
                // Fallback in case of pre-migration state
            }

            if (crops.Count == 0)
            {
                // Before the first migration/seed has run: serve the same reviewed seed file the database is filled from.
                crops = CropCalendarSeed.Load(_environment.ContentRootPath).Select((crop, index) => { crop.Id = index + 1; return crop; }).ToList();
            }

            _cache.Set(CacheKeyAllCrops, crops, TimeSpan.FromHours(24));
            return crops;
        }

        public async Task<CropCalendarEntry?> GetCropByIdAsync(int id)
        {
            var crops = await GetAllCropsAsync();
            return crops.FirstOrDefault(c => c.Id == id);
        }

        public async Task<CropCalendarIndexViewModel> GetCalendarModelAsync(
            string? search = null,
            string? category = null,
            string? season = null,
            string? division = null,
            int? month = null,
            string? stage = null)
        {
            int currentMonth = BangladeshClock.Today.Month;
            int activeMonth = month.HasValue && month.Value >= 1 && month.Value <= 12 ? month.Value : currentMonth;
            // Old links may still say Barisal / Chittagong; the seed uses today's names.
            division = BangladeshGeo.Canonical(division);

            var allCrops = await GetAllCropsAsync();
            var filtered = allCrops.AsEnumerable();

            // 1. Search Query (English Name, Bangla Name, Scientific Name, or Varieties)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.Trim();
                filtered = filtered.Where(c =>
                    c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    c.BanglaName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    c.ScientificName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    c.PopularVarieties.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    c.MajorDistricts.Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            // 2. Category filter
            if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(c => c.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
            }

            // 3. Season filter
            if (!string.IsNullOrWhiteSpace(season) && !season.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(c => c.Season.Equals(season, StringComparison.OrdinalIgnoreCase) || c.Season == "YearRound");
            }

            // 4. Division / Region filter
            if (!string.IsNullOrWhiteSpace(division) && !division.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(c => c.Division == "All" || c.Division.Contains(division, StringComparison.OrdinalIgnoreCase));
            }

            // 5. Active Stage filter for Selected Month (All, Sowing, Growing, Harvesting)
            if (!string.IsNullOrWhiteSpace(stage) && !stage.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (stage.Equals("Sowing", StringComparison.OrdinalIgnoreCase))
                {
                    filtered = filtered.Where(c => c.SowingMonths.Contains(activeMonth));
                }
                else if (stage.Equals("Harvesting", StringComparison.OrdinalIgnoreCase))
                {
                    filtered = filtered.Where(c => c.HarvestingMonths.Contains(activeMonth));
                }
                else if (stage.Equals("Growing", StringComparison.OrdinalIgnoreCase))
                {
                    filtered = filtered.Where(c => c.GrowingMonths.Contains(activeMonth));
                }
            }

            var cropItems = filtered.Select(c => new CropCalendarItemViewModel
            {
                Id = c.Id,
                Name = c.Name,
                BanglaName = c.BanglaName,
                ScientificName = c.ScientificName,
                Category = c.Category,
                CategoryDisplay = GetCategoryDisplay(c.Category),
                Season = c.Season,
                SeasonDisplay = GetSeasonDisplay(c.Season),
                SowingMonths = c.SowingMonths,
                GrowingMonths = c.GrowingMonths,
                HarvestingMonths = c.HarvestingMonths,
                DurationDays = c.DurationDays,
                OptimalTemperature = c.OptimalTemperature,
                SoilTypes = c.SoilTypes,
                WaterRequirement = c.WaterRequirement,
                PopularVarieties = c.PopularVarieties,
                MajorDistricts = c.MajorDistricts,
                Division = c.Division,
                KeyTips = c.KeyTips,
                IconClass = c.IconClass,
                BadgeColor = c.BadgeColor,
                Key = c.Key,
                SoilTypesBn = c.SoilTypesBn,
                WaterRequirementBn = c.WaterRequirementBn,
                KeyTipsBn = c.KeyTipsBn,
                Source = c.Source
            }).ToList();

            // Setup Months Header with isCurrent flag
            var monthsList = MonthHeaders.Select(m => new MonthTimelineHeader
            {
                MonthNumber = m.MonthNumber,
                EnglishShort = m.EnglishShort,
                EnglishFull = m.EnglishFull,
                BanglaName = m.BanglaName,
                BanglaMonthApprox = m.BanglaMonthApprox,
                IsCurrent = m.MonthNumber == currentMonth
            }).ToList();

            var currentMonthHeader = monthsList.FirstOrDefault(m => m.MonthNumber == currentMonth) ?? monthsList[0];

            // Counts for Hero Bar based on all reference crops for current month
            int sowingNow = allCrops.Count(c => c.SowingMonths.Contains(currentMonth));
            int harvestingNow = allCrops.Count(c => c.HarvestingMonths.Contains(currentMonth));
            int growingNow = allCrops.Count(c => c.GrowingMonths.Contains(currentMonth));

            var viewModel = new CropCalendarIndexViewModel
            {
                Crops = cropItems,
                SearchQuery = search,
                SelectedCategory = category ?? "All",
                SelectedSeason = season ?? "All",
                SelectedDivision = division ?? "All",
                SelectedMonth = activeMonth,
                SelectedStage = stage ?? "All",

                CurrentMonth = currentMonth,
                CurrentMonthName = currentMonthHeader.EnglishFull,
                CurrentBanglaMonth = $"{currentMonthHeader.BanglaName} ({currentMonthHeader.BanglaMonthApprox})",
                CurrentSeason = GetCurrentSeasonName(currentMonth),

                TotalCropsCount = cropItems.Count,
                SowingNowCount = sowingNow,
                HarvestingNowCount = harvestingNow,
                GrowingNowCount = growingNow,

                Months = monthsList,
                Categories = GetCategoryOptions(),
                Seasons = GetSeasonOptions(),
                Divisions = GetDivisionOptions()
            };

            return viewModel;
        }

        public async Task<FarmerCropAdvisoryViewModel?> GetRecommendationForFarmerAsync(string farmerId, int? month = null)
        {
            if (string.IsNullOrWhiteSpace(farmerId)) return null;

            var user = await _userManager.FindByIdAsync(farmerId);
            if (user == null) return null;

            string? cropName = user.Specialization;
            string? district = user.District ?? user.Location;

            return await GetRecommendationForCropAsync(cropName, district, month);
        }

        public async Task<FarmerCropAdvisoryViewModel?> GetRecommendationForCropAsync(string? cropName, string? district, int? month = null)
        {
            int targetMonth = month.HasValue && month.Value >= 1 && month.Value <= 12 ? month.Value : BangladeshClock.Today.Month;
            var allCrops = await GetAllCropsAsync();
            if (allCrops.Count == 0) return null;

            CropCalendarEntry? matchedCrop = null;

            if (!string.IsNullOrWhiteSpace(cropName))
            {
                var cleanCrop = cropName.Trim();
                // 1. Direct match on ProfileCropName
                matchedCrop = allCrops.FirstOrDefault(c => string.Equals(c.ProfileCropName, cleanCrop, StringComparison.OrdinalIgnoreCase));

                // 2. Partial match on Name or BanglaName
                if (matchedCrop == null)
                {
                    matchedCrop = allCrops.FirstOrDefault(c =>
                        c.Name.Contains(cleanCrop, StringComparison.OrdinalIgnoreCase) ||
                        c.BanglaName.Contains(cleanCrop, StringComparison.OrdinalIgnoreCase));
                }

                // 3. Keyword matching for common onboarding strings
                if (matchedCrop == null)
                {
                    if (cleanCrop.Contains("Boro", StringComparison.OrdinalIgnoreCase))
                        matchedCrop = allCrops.FirstOrDefault(c => c.Name.Contains("Boro"));
                    else if (cleanCrop.Contains("Aman", StringComparison.OrdinalIgnoreCase))
                        matchedCrop = allCrops.FirstOrDefault(c => c.Name.Contains("Aman"));
                    else if (cleanCrop.Contains("Aus", StringComparison.OrdinalIgnoreCase))
                        matchedCrop = allCrops.FirstOrDefault(c => c.Name.Contains("Aus"));
                    else if (cleanCrop.Contains("Wheat", StringComparison.OrdinalIgnoreCase) || cleanCrop.Contains("গম"))
                        matchedCrop = allCrops.FirstOrDefault(c => c.Name.Contains("Wheat"));
                    else if (cleanCrop.Contains("Potato", StringComparison.OrdinalIgnoreCase) || cleanCrop.Contains("আলু"))
                        matchedCrop = allCrops.FirstOrDefault(c => c.Name.Contains("Potato"));
                    else if (cleanCrop.Contains("Jute", StringComparison.OrdinalIgnoreCase) || cleanCrop.Contains("পাট"))
                        matchedCrop = allCrops.FirstOrDefault(c => c.Name.Contains("Jute"));
                    else if (cleanCrop.Contains("Maize", StringComparison.OrdinalIgnoreCase) || cleanCrop.Contains("ভুট্টা"))
                        matchedCrop = allCrops.FirstOrDefault(c => c.Name.Contains("Maize"));
                    else if (cleanCrop.Contains("Mustard", StringComparison.OrdinalIgnoreCase) || cleanCrop.Contains("সরিষা"))
                        matchedCrop = allCrops.FirstOrDefault(c => c.Name.Contains("Mustard"));
                    else if (cleanCrop.Contains("Vegetables", StringComparison.OrdinalIgnoreCase) || cleanCrop.Contains("সবজি"))
                        matchedCrop = allCrops.FirstOrDefault(c => c.Category == "Vegetables");
                    else if (cleanCrop.Contains("Pulses", StringComparison.OrdinalIgnoreCase) || cleanCrop.Contains("ডাল"))
                        matchedCrop = allCrops.FirstOrDefault(c => c.Category == "Pulses");
                }
            }

            // If still no crop specified or matched, pick the primary active crop in season for current month
            if (matchedCrop == null)
            {
                matchedCrop = allCrops.FirstOrDefault(c => c.SowingMonths.Contains(targetMonth))
                    ?? allCrops.FirstOrDefault(c => c.GrowingMonths.Contains(targetMonth))
                    ?? allCrops.FirstOrDefault();
            }

            if (matchedCrop == null) return null;

            bool isSuitable = BangladeshGeo.IsDistrictSuitable(district, matchedCrop.Division);

            string currentPhase;
            string currentPhaseBangla;
            string phaseBadgeColor;
            string summary;
            string summaryBangla;

            if (matchedCrop.SowingMonths.Contains(targetMonth))
            {
                currentPhase = "sowing";
                currentPhaseBangla = "বপন ও চারা রোপণ পর্ব";
                phaseBadgeColor = "success";
                summary = $"Optimal sowing / transplanting window for {matchedCrop.Name}. Prepare well-tilled soil with organic compost and recommended basal fertilizer doses.";
                summaryBangla = $"{matchedCrop.BanglaName} বীজ বপন ও চারা রোপণের উপযুক্ত মৌসুম। সুষম সার ও জৈব সার প্রয়োগ করে জমি উত্তমরূপে তৈরি করুন।";
            }
            else if (matchedCrop.HarvestingMonths.Contains(targetMonth))
            {
                currentPhase = "harvesting";
                currentPhaseBangla = "ফসল কর্তন ও মাড়াই পর্ব";
                phaseBadgeColor = "warning";
                summary = $"Harvesting period for {matchedCrop.Name}. Harvest when 80-85% grains/pods show physiological maturity. Store in moisture-free godowns.";
                summaryBangla = $"{matchedCrop.BanglaName} কাটার উপযুক্ত সময়। শতকরা ৮০-৮৫ ভাগ পরিপক্ব হলে ফসল কর্তন ও শুকিয়ে নিরাপদ গুদামে সংরক্ষণ করুন।";
            }
            else if (matchedCrop.GrowingMonths.Contains(targetMonth))
            {
                currentPhase = "growing";
                currentPhaseBangla = "মাঠে বৃদ্ধি ও পরিচর্যা পর্যায়";
                phaseBadgeColor = "primary";
                summary = $"Active vegetative & growth phase for {matchedCrop.Name}. Maintain optimal soil moisture, top-dress urea/potash, and scout for pests.";
                summaryBangla = $"{matchedCrop.BanglaName} এর মাঠে বৃদ্ধি ও পরিচর্যা পর্যায়। পরিমিত সেচ দিন, সুষম উপরি প্রয়োগ করুন এবং নিয়মিত ক্ষতিকর পোকা পর্যবেক্ষণ করুন।";
            }
            else
            {
                currentPhase = "off-season";
                currentPhaseBangla = "মৌসুম বহির্ভূত প্রস্তুতি";
                phaseBadgeColor = "secondary";
                var sowingShortNames = matchedCrop.SowingMonths
                    .Select(m => MonthHeaders.FirstOrDefault(h => h.MonthNumber == m)?.EnglishShort ?? m.ToString());
                var sowingBanglaNames = matchedCrop.SowingMonths
                    .Select(m => MonthHeaders.FirstOrDefault(h => h.MonthNumber == m)?.BanglaName ?? m.ToString());
                summary = $"Currently off-season for {matchedCrop.Name}. Next active planting window begins in {string.Join(", ", sowingShortNames)}.";
                summaryBangla = $"{matchedCrop.BanglaName} এখন মৌসুম বহির্ভূত। পরবর্তী রোপণ মৌসুম শুরু হবে {string.Join(", ", sowingBanglaNames)} মাসে।";
            }

            var monthHeader = MonthHeaders.FirstOrDefault(m => m.MonthNumber == targetMonth)
                ?? new MonthTimelineHeader { MonthNumber = targetMonth, EnglishFull = "Current Month", BanglaName = "চলতি মাস" };

            return new FarmerCropAdvisoryViewModel
            {
                CropId = matchedCrop.Id,
                CropName = matchedCrop.Name,
                BanglaCropName = matchedCrop.BanglaName,
                ProfileCropName = matchedCrop.ProfileCropName ?? matchedCrop.Name,
                CurrentPhase = currentPhase,
                CurrentPhaseBangla = currentPhaseBangla,
                PhaseBadgeColor = phaseBadgeColor,
                AdvisorySummary = summary,
                AdvisorySummaryBangla = summaryBangla,
                KeyTips = matchedCrop.KeyTips,
                OptimalTemperature = matchedCrop.OptimalTemperature,
                WaterRequirement = matchedCrop.WaterRequirement,
                PopularVarieties = matchedCrop.PopularVarieties,
                Season = matchedCrop.Season,
                District = district ?? "Bangladesh",
                Month = targetMonth,
                MonthName = monthHeader.EnglishFull,
                BanglaMonthName = $"{monthHeader.BanglaName} ({monthHeader.BanglaMonthApprox})",
                IsSuitableDistrict = isSuitable
            };
        }

        private static string GetCategoryDisplay(string category) => category switch
        {
            "Cereals" => "Cereals (দানা শস্য)",
            "CashCrops" => "Cash Crops (অর্থকরী ফসল)",
            "Tubers" => "Tubers (কন্দ জাতীয়)",
            "Oilseeds" => "Oilseeds (তৈলবীজ)",
            "Pulses" => "Pulses (ডাল)",
            "Vegetables" => "Vegetables (শাকসবজি)",
            "Spices" => "Spices (মসলা)",
            "Fruits" => "Fruits (ফল)",
            _ => category
        };

        private static string GetSeasonDisplay(string season) => season switch
        {
            "Rabi" => "Rabi / Winter (রবি মৌসুম)",
            "Kharif-1" => "Kharif-1 / Early Summer (খরিফ-১)",
            "Kharif-2" => "Kharif-2 / Monsoon (খরিফ-২)",
            "YearRound" => "Year-Round (বারোমাসি)",
            _ => season
        };

        public static string GetCurrentSeasonName(int? month = null)
        {
            int m = month ?? BangladeshClock.Today.Month;
            return CropAdvisorService.SeasonForMonth(m) switch
            {
                "Rabi" => "Rabi Season (রবি মৌসুম - শীতকালীন)",
                "Kharif-1" => "Kharif-1 Season (খরিফ-১ - প্রাক-খরিফ / গ্রীষ্মকালীন)",
                "Kharif-2" => "Kharif-2 Season (খরিফ-২ - বর্ষাকালীন)",
                _ => "Rabi Season"
            };
        }

        private static List<SelectOptionItem> GetCategoryOptions() => new()
        {
            new("All", "All Categories", "সকল বিভাগ"),
            new("Cereals", "Cereals (Rice, Wheat, Maize)", "দানা শস্য (ধান, গম, ভুট্টা)"),
            new("CashCrops", "Cash Crops (Jute, Sugarcane)", "অর্থকরী ফসল (পাট, আখ)"),
            new("Tubers", "Tubers (Potato)", "কন্দ জাতীয় (আলু)"),
            new("Oilseeds", "Oilseeds (Mustard, Sunflower, Peanut)", "তৈলবীজ (সরিষা, সূর্যমুখী, চিনাবাদাম)"),
            new("Pulses", "Pulses (Lentil, Mung, Chickpea)", "ডাল জাতীয় (মসুর, মুগ, ছোলা)"),
            new("Vegetables", "Vegetables (Tomato, Brinjal, Cabbage)", "শাকসবজি (টমেটো, বেগুন, কপি)"),
            new("Spices", "Spices (Onion, Garlic, Chili)", "মসলা (পেঁয়াজ, রসুন, মরিচ)"),
            new("Fruits", "Fruits (Watermelon)", "ফল (তরমুজ)")
        };

        private static List<SelectOptionItem> GetSeasonOptions() => new()
        {
            new("All", "All Seasons", "সকল মৌসুম"),
            new("Rabi", "Rabi (Winter: Nov – Mar)", "রবি (শীতকাল: কার্তিক – ফাল্গুন)"),
            new("Kharif-1", "Kharif-1 (Early Summer: Apr – Jun)", "খরিফ-১ (গ্রীষ্মকাল: চৈত্র – জ্যৈষ্ঠ)"),
            new("Kharif-2", "Kharif-2 (Monsoon: Jul – Oct)", "খরিফ-২ (বর্ষাকাল: আষাঢ় – আশ্বিন)"),
            new("YearRound", "Year-Round / Annual", "বারোমাসি / বার্ষিক")
        };

        private static List<SelectOptionItem> GetDivisionOptions() => new()
        {
            new("All", "All Bangladesh", "সমগ্র বাংলাদেশ"),
            new("Rajshahi", "Rajshahi (Barind Tract)", "রাজশাহী (বরেন্দ্র অঞ্চল)"),
            new("Rangpur", "Rangpur (Northern Plains)", "রংপুর (উত্তরাঞ্চল)"),
            new("Khulna", "Khulna (South-West / Coastal)", "খুলনা (দক্ষিণ-পশ্চিম / উপকূল)"),
            new("Barishal", "Barishal (Southern Coastal Belt)", "বরিশাল (উপকূলীয় অঞ্চল)"),
            new("Dhaka", "Dhaka & Mymensingh", "ঢাকা ও ময়মনসিংহ"),
            new("Sylhet", "Sylhet (Haor & North-East)", "সিলেট (হাওর ও উত্তর-পূর্ব)"),
            new("Chattogram", "Chattogram & Cumilla", "চট্টগ্রাম ও কুমিল্লা")
        };
    }
}
