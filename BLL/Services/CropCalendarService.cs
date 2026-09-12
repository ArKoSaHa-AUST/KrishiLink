using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;

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
    }

    public class CropCalendarService : ICropCalendarService
    {
        private static readonly List<CropCalendarEntry> ReferenceCrops = new()
        {
            new CropCalendarEntry
            {
                Id = 1,
                Name = "Boro Rice (HYV & Hybrid)",
                BanglaName = "বোরো ধান (উফশী ও হাইব্রিড)",
                ScientificName = "Oryza sativa",
                Category = "Cereals",
                Season = "Rabi",
                SowingMonths = new List<int> { 11, 12, 1 }, // Nov - Jan (Seedbed & Transplanting)
                GrowingMonths = new List<int> { 1, 2, 3 },    // Jan - Mar (Tillering & Panicle)
                HarvestingMonths = new List<int> { 4, 5 },    // Apr - May
                DurationDays = "140 – 160 Days",
                OptimalTemperature = "20°C – 32°C",
                SoilTypes = "Clay Loam, Alluvial Silt (এঁটেল-দোআঁশ)",
                WaterRequirement = "High — continuous standing water (3-5 cm) during tillering",
                PopularVarieties = "BRRI dhan 28, BRRI dhan 29, BRRI dhan 89, BRRI dhan 92, Bangabandhu dhan 100",
                MajorDistricts = "Mymensingh, Bogra, Dinajpur, Naogaon, Kishoreganj, Sunamganj (Haor)",
                Division = "All",
                KeyTips = "Transplant 30-35 day seedlings with 2-3 seedlings per hill. Apply urea in 3 equal splits. Drain water 10-12 days before harvest.",
                IconClass = "bi-flower2",
                BadgeColor = "success"
            },
            new CropCalendarEntry
            {
                Id = 2,
                Name = "T. Aman Rice (Transplanted Aman)",
                BanglaName = "রোপা আমন ধান (উফশী)",
                ScientificName = "Oryza sativa",
                Category = "Cereals",
                Season = "Kharif-2",
                SowingMonths = new List<int> { 6, 7, 8 },     // Jun - Aug (Seedbed in Jun-Jul, Transplanting in Jul-Aug)
                GrowingMonths = new List<int> { 8, 9, 10 },   // Aug - Oct (Vegetative & Booting)
                HarvestingMonths = new List<int> { 11, 12 },  // Nov - Dec (Hemanta harvest)
                DurationDays = "115 – 140 Days",
                OptimalTemperature = "22°C – 35°C",
                SoilTypes = "Clay Loam, Silt Loam (দোআঁশ ও এঁটেল)",
                WaterRequirement = "Medium to High — mainly rainfed with supplemental irrigation during dry spells",
                PopularVarieties = "BRRI dhan 49, BRRI dhan 75, BRRI dhan 87, Bina dhan-7, Swarna",
                MajorDistricts = "Rangpur, Dinajpur, Bogra, Rajshahi, Jessore, Barisal, Comilla",
                Division = "All",
                KeyTips = "Transplant 25-30 day seedlings by August 15 for optimal yield. Watch for stem borer and rice blast during humid vegetative stage.",
                IconClass = "bi-flower2",
                BadgeColor = "success"
            },
            new CropCalendarEntry
            {
                Id = 3,
                Name = "Aus Rice (Upland & Transplanted)",
                BanglaName = "আউশ ধান (বোনা ও রোপা)",
                ScientificName = "Oryza sativa",
                Category = "Cereals",
                Season = "Kharif-1",
                SowingMonths = new List<int> { 3, 4 },        // Mar - Apr
                GrowingMonths = new List<int> { 4, 5, 6 },    // Apr - Jun
                HarvestingMonths = new List<int> { 6, 7 },    // Jun - Jul
                DurationDays = "95 – 110 Days",
                OptimalTemperature = "25°C – 35°C",
                SoilTypes = "Sandy Loam, Alluvial Silt (বেলে-দোআঁশ)",
                WaterRequirement = "Medium — relies on pre-monsoon rains (Kalbaishakhi)",
                PopularVarieties = "BRRI dhan 48, BRRI dhan 82, BRRI dhan 85, Nerica-1",
                MajorDistricts = "Kushtia, Meherpur, Comilla, Chittagong, Sylhet, Mymensingh",
                Division = "All",
                KeyTips = "Fast maturing crop fits neatly between Boro and Aman. Early weeding is crucial in direct seeded Aus plots.",
                IconClass = "bi-flower2",
                BadgeColor = "success"
            },
            new CropCalendarEntry
            {
                Id = 4,
                Name = "High-Yield Wheat",
                BanglaName = "উচ্চফলনশীল গম",
                ScientificName = "Triticum aestivum",
                Category = "Cereals",
                Season = "Rabi",
                SowingMonths = new List<int> { 11, 12 },      // Nov - Dec (Best Nov 15-30)
                GrowingMonths = new List<int> { 12, 1, 2 },   // Dec - Feb
                HarvestingMonths = new List<int> { 3, 4 },    // Mar - Apr
                DurationDays = "105 – 115 Days",
                OptimalTemperature = "15°C – 25°C",
                SoilTypes = "Well-drained Loam, Sandy Loam (সুনিষ্কাশিত দোআঁশ)",
                WaterRequirement = "Low to Medium — 3 light irrigations (CRI stage at 18-21 days, flowering, and grain fill)",
                PopularVarieties = "BARI Gom 30, BARI Gom 32, BARI Gom 33 (Blast Resistant), WMRI Gom 1",
                MajorDistricts = "Dinajpur, Thakurgaon, Rajshahi, Pabna, Kushtia, Chuadanga, Meherpur",
                Division = "Rajshahi, Rangpur, Khulna",
                KeyTips = "Sow before December 10 to avoid terminal heat stress in March. Use certified blast-resistant BARI Gom 33 seed.",
                IconClass = "bi-tsunami",
                BadgeColor = "warning"
            },
            new CropCalendarEntry
            {
                Id = 5,
                Name = "Hybrid Maize (Corn)",
                BanglaName = "হাইব্রিড ভুট্টা",
                ScientificName = "Zea mays",
                Category = "Cereals",
                Season = "Rabi",
                SowingMonths = new List<int> { 10, 11, 12 },  // Oct - Dec
                GrowingMonths = new List<int> { 11, 12, 1, 2 },// Nov - Feb
                HarvestingMonths = new List<int> { 3, 4, 5 }, // Mar - May
                DurationDays = "135 – 145 Days",
                OptimalTemperature = "18°C – 32°C",
                SoilTypes = "Deep fertile Loam, Clay Loam (উর্বর দোআঁশ)",
                WaterRequirement = "Medium — furrow irrigation at knee-high, tasseling, and grain filling stages",
                PopularVarieties = "BARI Hybrid Maize 9, BARI Hybrid Maize 11, PAC 759, Pioneer 3355, Sunshine-55",
                MajorDistricts = "Dinajpur, Chuadanga, Lalmonirhat, Bogra, Manikganj, Rajshahi",
                Division = "All",
                KeyTips = "Maintain 60 cm row and 20 cm plant spacing. Monitor weekly for Fall Armyworm and install pheromone traps.",
                IconClass = "bi-sun-fill",
                BadgeColor = "warning"
            },
            new CropCalendarEntry
            {
                Id = 6,
                Name = "Potato (Winter Table & Seed)",
                BanglaName = "গোল আলু (ডায়মন্ট ও কার্ডিনাল)",
                ScientificName = "Solanum tuberosum",
                Category = "Tubers",
                Season = "Rabi",
                SowingMonths = new List<int> { 10, 11, 12 },  // Oct - Dec
                GrowingMonths = new List<int> { 11, 12, 1 },  // Nov - Jan
                HarvestingMonths = new List<int> { 1, 2, 3 }, // Jan - Mar
                DurationDays = "85 – 100 Days",
                OptimalTemperature = "15°C – 22°C (Cool nights essential for tuberization)",
                SoilTypes = "Sandy Loam, Silt Loam with organic matter (বেলে-দোআঁশ)",
                WaterRequirement = "Medium — 3-4 light irrigations; strictly avoid waterlogging",
                PopularVarieties = "Diamant, Cardinal, Granola, Asterix, BARI Alu 7, BARI Alu 41",
                MajorDistricts = "Munshiganj, Bogra, Rangpur, Dinajpur, Joypurhat, Nilphamari",
                Division = "All",
                KeyTips = "Earthing-up at 25 and 45 days. Stop irrigation 10-12 days before harvest and dehaulm to harden tuber skin.",
                IconClass = "bi-circle-fill",
                BadgeColor = "primary"
            },
            new CropCalendarEntry
            {
                Id = 7,
                Name = "Jute (Toshe & Deshi Golden Fiber)",
                BanglaName = "তোষা ও দেশী পাট (সোনালী আঁশ)",
                ScientificName = "Corchorus olitorius / capsularis",
                Category = "CashCrops",
                Season = "Kharif-1",
                SowingMonths = new List<int> { 3, 4, 5 },     // Mar - May
                GrowingMonths = new List<int> { 4, 5, 6, 7 }, // Apr - Jul
                HarvestingMonths = new List<int> { 7, 8, 9 }, // Jul - Sep
                DurationDays = "110 – 120 Days",
                OptimalTemperature = "24°C – 37°C with high humidity",
                SoilTypes = "Alluvial Silt, Clay Loam (পলি ও এঁটেল দোআঁশ)",
                WaterRequirement = "High — thrives in heavy monsoon rains and needs slow-moving retting water",
                PopularVarieties = "O-9897 (Toshe), Robi-1, BJRI Deshi Pat 8, Chaitali Pat",
                MajorDistricts = "Faridpur, Jessore, Rajbari, Jamalpur, Sirajganj, Mymensingh, Rangpur",
                Division = "All",
                KeyTips = "Harvest when 50% plants are in pod formation for strongest fiber. Ret in clean, slow-moving water for bright golden sheen.",
                IconClass = "bi-layers-fill",
                BadgeColor = "success"
            },
            new CropCalendarEntry
            {
                Id = 8,
                Name = "Mustard & Rapeseed",
                BanglaName = "উচ্চফলনশীল সরিষা",
                ScientificName = "Brassica napus / campestris",
                Category = "Oilseeds",
                Season = "Rabi",
                SowingMonths = new List<int> { 10, 11 },      // Oct - Nov
                GrowingMonths = new List<int> { 11, 12, 1 },  // Nov - Jan
                HarvestingMonths = new List<int> { 1, 2 },    // Jan - Feb
                DurationDays = "70 – 85 Days",
                OptimalTemperature = "15°C – 25°C",
                SoilTypes = "Loam, Sandy Loam (দোআঁশ ও বেলে-দোআঁশ)",
                WaterRequirement = "Low — 1-2 light irrigations at pre-flowering and pod filling",
                PopularVarieties = "BARI Sharisha 14, BARI Sharisha 17, BARI Sharisha 18, Bina Sharisha 4",
                MajorDistricts = "Tangail, Sirajganj, Manikganj, Jessore, Magura, Comilla",
                Division = "All",
                KeyTips = "Short duration crop perfectly bridges Aman harvest and late Boro transplanting. Control aphids during yellow bloom.",
                IconClass = "bi-brightness-high-fill",
                BadgeColor = "warning"
            },
            new CropCalendarEntry
            {
                Id = 9,
                Name = "Lentil (Masur Dal)",
                BanglaName = "মসুর ডাল",
                ScientificName = "Lens culinaris",
                Category = "Pulses",
                Season = "Rabi",
                SowingMonths = new List<int> { 10, 11 },      // Oct - Nov
                GrowingMonths = new List<int> { 11, 12, 1 },  // Nov - Jan
                HarvestingMonths = new List<int> { 2, 3 },    // Feb - Mar
                DurationDays = "100 – 110 Days",
                OptimalTemperature = "15°C – 25°C",
                SoilTypes = "Well-drained Loam, Silt Loam (সুনিষ্কাশিত দোআঁশ)",
                WaterRequirement = "Low — mostly rainfed residual moisture; sensitive to water stagnation",
                PopularVarieties = "BARI Masur 6, BARI Masur 7, BARI Masur 8, Bina Masur 5",
                MajorDistricts = "Faridpur, Jessore, Kushtia, Rajshahi, Magura, Pabna",
                Division = "Rajshahi, Khulna, Dhaka",
                KeyTips = "Treat seeds with bio-fertilizer (Rhizobium) before sowing. Harvest in morning when pods are brown to prevent shattering.",
                IconClass = "bi-dot",
                BadgeColor = "primary"
            },
            new CropCalendarEntry
            {
                Id = 10,
                Name = "Chickpea (Chhola)",
                BanglaName = "ছোলা",
                ScientificName = "Cicer arietinum",
                Category = "Pulses",
                Season = "Rabi",
                SowingMonths = new List<int> { 10, 11 },      // Oct - Nov
                GrowingMonths = new List<int> { 11, 12, 1, 2 },// Nov - Feb
                HarvestingMonths = new List<int> { 3, 4 },    // Mar - Apr
                DurationDays = "120 – 130 Days",
                OptimalTemperature = "18°C – 26°C",
                SoilTypes = "Deep Loam, Clay Loam in Barind areas (বরেন্দ্র অঞ্চলের দোআঁশ)",
                WaterRequirement = "Low — drought hardy; requires zero standing water",
                PopularVarieties = "BARI Chhola 5, BARI Chhola 9, BARI Chhola 10",
                MajorDistricts = "Rajshahi, Chapainawabganj, Naogaon, Kushtia, Jessore",
                Division = "Rajshahi, Khulna",
                KeyTips = "Excellent cash pulse for dry high Barind tract after early Aman rice harvest.",
                IconClass = "bi-dot",
                BadgeColor = "primary"
            },
            new CropCalendarEntry
            {
                Id = 11,
                Name = "Mungbean (Mug Dal)",
                BanglaName = "মুগ ডাল (গ্রীষ্ম ও খরিফ)",
                ScientificName = "Vigna radiata",
                Category = "Pulses",
                Season = "Kharif-1",
                SowingMonths = new List<int> { 2, 3, 8 },     // Feb - Mar (Late Rabi/Summer) & Aug (Kharif-2)
                GrowingMonths = new List<int> { 3, 4, 9 },    // Mar - Apr & Sep
                HarvestingMonths = new List<int> { 4, 5, 10 },// Apr - May & Oct
                DurationDays = "60 – 65 Days",
                OptimalTemperature = "25°C – 35°C",
                SoilTypes = "Well-drained Sandy Loam, Silt Loam (বেলে-দোআঁশ)",
                WaterRequirement = "Low to Medium — short duration pulse",
                PopularVarieties = "BARI Mung 6, BARI Mung 8, Bina Mung 8",
                MajorDistricts = "Patuakhali, Bhola, Barisal, Jhenaidah, Jessore, Natore",
                Division = "Barisal, Khulna, Rajshahi",
                KeyTips = "Short 60-day crop. Pods can be picked in 2 flushes. Crop residue enriches soil nitrogen.",
                IconClass = "bi-dot",
                BadgeColor = "primary"
            },
            new CropCalendarEntry
            {
                Id = 12,
                Name = "Winter Onion",
                BanglaName = "শীতকালীন পেঁয়াজ (তাহেরপুরী ও বারি)",
                ScientificName = "Allium cepa",
                Category = "Spices",
                Season = "Rabi",
                SowingMonths = new List<int> { 10, 11, 12 },  // Seedbed Oct-Nov, Transplant Dec
                GrowingMonths = new List<int> { 12, 1, 2 },   // Dec - Feb
                HarvestingMonths = new List<int> { 3, 4 },    // Mar - Apr
                DurationDays = "90 – 105 Days after transplanting",
                OptimalTemperature = "13°C – 24°C",
                SoilTypes = "Fertile Sandy Loam with high organic matter (উর্বর বেলে-দোআঁশ)",
                WaterRequirement = "Medium — regular light irrigations; stop 15 days before harvest",
                PopularVarieties = "BARI Piaz 1, BARI Piaz 4, Taherpuri, Faridpuri Bhati",
                MajorDistricts = "Pabna, Faridpur, Rajshahi, Kushtia, Natore, Manikganj",
                Division = "All",
                KeyTips = "Cure bulbs in shade for 3-5 days after digging. Ensure godown storage is well-ventilated and dry.",
                IconClass = "bi-record-circle-fill",
                BadgeColor = "danger"
            },
            new CropCalendarEntry
            {
                Id = 13,
                Name = "Summer & Monsoon Onion",
                BanglaName = "গ্রীষ্মকালীন ও বর্ষাকালীন পেঁয়াজ",
                ScientificName = "Allium cepa",
                Category = "Spices",
                Season = "Kharif-1",
                SowingMonths = new List<int> { 2, 3, 4 },     // Feb - Apr
                GrowingMonths = new List<int> { 3, 4, 5 },    // Mar - May
                HarvestingMonths = new List<int> { 6, 7, 8 }, // Jun - Aug
                DurationDays = "90 – 100 Days",
                OptimalTemperature = "25°C – 35°C",
                SoilTypes = "Raised Bed Sandy Loam (উঁচু বেড বেলে-দোআঁশ)",
                WaterRequirement = "Medium — requires polythene rain shelters or raised bed drainage during monsoon showers",
                PopularVarieties = "BARI Piaz 5, Summer King",
                MajorDistricts = "Kushtia, Meherpur, Pabna, Rajshahi, Bogra",
                Division = "Khulna, Rajshahi",
                KeyTips = "High-profit off-season crop. Raised bed cultivation with transparent polythene tunnel prevents bulb rotting.",
                IconClass = "bi-record-circle-fill",
                BadgeColor = "danger"
            },
            new CropCalendarEntry
            {
                Id = 14,
                Name = "Garlic (Winter & Zero Tillage)",
                BanglaName = "রসুন (বিনা চাষ ও সাধারণ)",
                ScientificName = "Allium sativum",
                Category = "Spices",
                Season = "Rabi",
                SowingMonths = new List<int> { 10, 11 },      // Oct - Nov
                GrowingMonths = new List<int> { 11, 12, 1, 2 },// Nov - Feb
                HarvestingMonths = new List<int> { 3, 4 },    // Mar - Apr
                DurationDays = "120 – 135 Days",
                OptimalTemperature = "15°C – 25°C",
                SoilTypes = "Clay Loam, Silt Loam with paddy straw mulch (কাদা দোআঁশ)",
                WaterRequirement = "Medium — zero tillage method preserves mud moisture under straw",
                PopularVarieties = "BARI Roshun 1, BARI Roshun 2, Natore Local, Chalanbeel Local",
                MajorDistricts = "Natore (Gurudaspur, Baraigram), Pabna, Rajshahi, Dinajpur",
                Division = "Rajshahi, Rangpur",
                KeyTips = "Zero-tillage garlic on muddy soil directly after Aman harvest covered by rice straw mulch cuts cost by 40%.",
                IconClass = "bi-record-circle-fill",
                BadgeColor = "danger"
            },
            new CropCalendarEntry
            {
                Id = 15,
                Name = "Chili / Green & Red Pepper",
                BanglaName = "কাঁচা ও শুকনো মরিচ",
                ScientificName = "Capsicum annuum",
                Category = "Spices",
                Season = "Rabi",
                SowingMonths = new List<int> { 10, 11, 3 },   // Oct-Nov (Rabi) & Mar (Kharif)
                GrowingMonths = new List<int> { 11, 12, 4 },  // Nov-Dec & Apr
                HarvestingMonths = new List<int> { 1, 2, 3, 4, 5 }, // Jan - May (Continuous pickings)
                DurationDays = "150 – 180 Days",
                OptimalTemperature = "20°C – 30°C",
                SoilTypes = "Sandy Loam, Alluvial Riverbed (বেলে-দোআঁশ ও চর অঞ্চল)",
                WaterRequirement = "Medium — sensitive to excess water and root rot",
                PopularVarieties = "BARI Morich 1, BARI Morich 2, Bogra Bindu, Jamalpur Local",
                MajorDistricts = "Bogra (Sariakandi), Jamalpur, Chandpur, Faridpur, Panchagarh",
                Division = "All",
                KeyTips = "Multiple pickings throughout spring. Dry on clean concrete yards or solar dryers for premium red color.",
                IconClass = "bi-fire",
                BadgeColor = "danger"
            },
            new CropCalendarEntry
            {
                Id = 16,
                Name = "Winter Tomato (HYV & Hybrid)",
                BanglaName = "শীতকালীন টমেটো (উফশী ও হাইব্রিড)",
                ScientificName = "Solanum lycopersicum",
                Category = "Vegetables",
                Season = "Rabi",
                SowingMonths = new List<int> { 9, 10, 11 },   // Seedbed Sep-Oct, Transplant Oct-Nov
                GrowingMonths = new List<int> { 11, 12 },     // Nov - Dec
                HarvestingMonths = new List<int> { 1, 2, 3 }, // Jan - Mar
                DurationDays = "90 – 110 Days",
                OptimalTemperature = "18°C – 27°C (Night temp 15°C-18°C triggers flowering)",
                SoilTypes = "Rich Loam, Sandy Loam (উর্বর দোআঁশ)",
                WaterRequirement = "Medium — regular furrow irrigation; avoid splashing leaves to prevent blight",
                PopularVarieties = "BARI Tomato 14, BARI Tomato 15, Ratan, Bahar, Beautiful",
                MajorDistricts = "Jessore, Comilla, Dinajpur, Bogra, Rajshahi, Chittagong",
                Division = "All",
                KeyTips = "Staking with bamboo poles improves fruit size and prevents fungal soil rot. Apply Boron to stop fruit cracking.",
                IconClass = "bi-egg-fill",
                BadgeColor = "info"
            },
            new CropCalendarEntry
            {
                Id = 17,
                Name = "Brinjal / Eggplant (Aubergine)",
                BanglaName = "বেগুন (উফশী ও বিটি বেগুন)",
                ScientificName = "Solanum melongena",
                Category = "Vegetables",
                Season = "YearRound",
                SowingMonths = new List<int> { 8, 9, 10, 4 }, // Aug - Oct (Rabi) & Apr (Kharif)
                GrowingMonths = new List<int> { 10, 11, 5 },  // Oct - Nov & May
                HarvestingMonths = new List<int> { 11, 12, 1, 2, 6, 7 }, // Nov-Feb & Jun-Jul
                DurationDays = "130 – 160 Days",
                OptimalTemperature = "22°C – 32°C",
                SoilTypes = "Deep fertile Silt Loam, Clay Loam (গভীর উর্বর দোআঁশ)",
                WaterRequirement = "Medium — regular irrigation at 10-12 day intervals",
                PopularVarieties = "Bt Brinjal 1-4, BARI Begun 8, BARI Begun 10, Singnath, Islampuri",
                MajorDistricts = "Jessore, Bogra, Mymensingh, Rangpur, Comilla, Jamalpur",
                Division = "All",
                KeyTips = "Bt Brinjal offers 100% natural immunity against Fruit and Shoot Borer without toxic chemical sprays.",
                IconClass = "bi-egg-fill",
                BadgeColor = "info"
            },
            new CropCalendarEntry
            {
                Id = 18,
                Name = "Cabbage & Cauliflower",
                BanglaName = "বাঁধাকপি ও ফুলকপি",
                ScientificName = "Brassica oleracea",
                Category = "Vegetables",
                Season = "Rabi",
                SowingMonths = new List<int> { 9, 10, 11 },   // Seedbed Sep-Oct, Transplant Oct-Nov
                GrowingMonths = new List<int> { 11, 12 },     // Nov - Dec
                HarvestingMonths = new List<int> { 12, 1, 2 },// Dec - Feb
                DurationDays = "75 – 90 Days",
                OptimalTemperature = "15°C – 22°C",
                SoilTypes = "Heavy Loam, Clay Loam with rich compost (সারসমৃদ্ধ এঁটেল-দোআঁশ)",
                WaterRequirement = "Medium — continuous soil moisture needed for compact head formation",
                PopularVarieties = "Snow White, Green Express, Atlas, BARI Fulkopi 1, BARI Bandhakopi 2",
                MajorDistricts = "Bogra, Jessore, Rangpur, Comilla, Dhaka, Rajshahi",
                Division = "All",
                KeyTips = "Tie outer leaves around cauliflower curds (blanching) 5-7 days before harvest for spotless white heads.",
                IconClass = "bi-circle-square",
                BadgeColor = "info"
            },
            new CropCalendarEntry
            {
                Id = 19,
                Name = "Watermelon (Coastal & Char)",
                BanglaName = "তরমুজ (উপকূলীয় ও চর)",
                ScientificName = "Citrullus lanatus",
                Category = "Fruits",
                Season = "Rabi",
                SowingMonths = new List<int> { 12, 1 },       // Dec - Jan
                GrowingMonths = new List<int> { 1, 2 },       // Jan - Feb
                HarvestingMonths = new List<int> { 3, 4, 5 }, // Mar - May (Spring/Summer)
                DurationDays = "80 – 95 Days",
                OptimalTemperature = "24°C – 35°C (Warm sunshine promotes high sugar content)",
                SoilTypes = "Sandy Loam, River Char lands (বেলে-দোআঁশ ও নদীর চর)",
                WaterRequirement = "Medium — pit method with localized basin watering; avoid flooding vines",
                PopularVarieties = "Dragon, Black Diamond, Pakiza, Sweet Miracle, Big Top",
                MajorDistricts = "Patuakhali (Galachipa), Bhola, Barisal, Barguna, Noakhali, Natore",
                Division = "Barisal, Chittagong, Khulna",
                KeyTips = "Major coastal cash crop. Lay dry straw under developing melons to prevent soil dampness and spot marks.",
                IconClass = "bi-heart-fill",
                BadgeColor = "success"
            },
            new CropCalendarEntry
            {
                Id = 20,
                Name = "Sugarcane (Commercial Annual)",
                BanglaName = "আখ (বার্ষিক অর্থকরী ফসল)",
                ScientificName = "Saccharum officinarum",
                Category = "CashCrops",
                Season = "YearRound",
                SowingMonths = new List<int> { 10, 11, 2, 3 },// Oct - Nov & Feb - Mar
                GrowingMonths = new List<int> { 12, 1, 4, 5, 6, 7, 8, 9, 10 }, // Year-round vegetative
                HarvestingMonths = new List<int> { 11, 12, 1, 2, 3 }, // Nov - Mar (Sugar mill crushing season)
                DurationDays = "300 – 360 Days (10-12 Months)",
                OptimalTemperature = "26°C – 38°C",
                SoilTypes = "Deep fertile Loam, Silt Loam (গভীর উর্বর দোআঁশ)",
                WaterRequirement = "High — long duration crop with multiple trench irrigations",
                PopularVarieties = "Isd 37, Isd 39, Isd 40, BSRI Akh 42, BSRI Akh 45",
                MajorDistricts = "Joypurhat, Kushtia, Chuadanga, Natore, Faridpur, Thakurgaon",
                Division = "Rajshahi, Khulna, Rangpur",
                KeyTips = "Tie canes together in clumps to prevent storm lodging. Intercrop with mustard, potato, or onion in first 90 days.",
                IconClass = "bi-tree-fill",
                BadgeColor = "success"
            },
            new CropCalendarEntry
            {
                Id = 21,
                Name = "Sunflower (Saline-Tolerant Oilseed)",
                BanglaName = "সূর্যমুখী (লবণাক্ততা সহনশীল)",
                ScientificName = "Helianthus annuus",
                Category = "Oilseeds",
                Season = "Rabi",
                SowingMonths = new List<int> { 11, 12 },      // Nov - Dec
                GrowingMonths = new List<int> { 12, 1, 2 },   // Dec - Feb
                HarvestingMonths = new List<int> { 3, 4 },    // Mar - Apr
                DurationDays = "90 – 105 Days",
                OptimalTemperature = "20°C – 28°C",
                SoilTypes = "Sandy Loam, Coastal Silt with mild salinity (উপকূলীয় বেলে-দোআঁশ)",
                WaterRequirement = "Low to Medium — 2 light irrigations; drought and moderate salt tolerant",
                PopularVarieties = "BARI Surjomukhi 2, BARI Surjomukhi 3, Hysun 33, Pacific 298",
                MajorDistricts = "Patuakhali, Barguna, Bhola, Satkhira, Khulna, Rajshahi",
                Division = "Barisal, Khulna",
                KeyTips = "Excellent crop for southern coastal belt where high salinity restricts winter Boro rice.",
                IconClass = "bi-brightness-high-fill",
                BadgeColor = "warning"
            },
            new CropCalendarEntry
            {
                Id = 22,
                Name = "Groundnut / Peanut (Char & Sandy Soils)",
                BanglaName = "চীনাবাদাম (চর ও বেলে দোআঁশ)",
                ScientificName = "Arachis hypogaea",
                Category = "Oilseeds",
                Season = "Rabi",
                SowingMonths = new List<int> { 11, 12, 5 },   // Nov-Dec (Rabi) & May (Kharif)
                GrowingMonths = new List<int> { 12, 1, 2, 6, 7 }, // Dec-Feb & Jun-Jul
                HarvestingMonths = new List<int> { 3, 4, 9 }, // Mar-Apr & Sep
                DurationDays = "120 – 140 Days",
                OptimalTemperature = "22°C – 32°C",
                SoilTypes = "Sandy Loam, River Char sandbeds (বেলে ও চরের বেলে-দোআঁশ)",
                WaterRequirement = "Low — thrives on riverbank sands",
                PopularVarieties = "BARI Chinabadam 8, BARI Chinabadam 9, Dhaka-1, Bina Chinabadam 4",
                MajorDistricts = "Kishoreganj, Jamalpur, Sirajganj, Tangail, Faridpur, Noakhali (Char)",
                Division = "All",
                KeyTips = "Apply Gypsum (Sulphur & Calcium) at 30 days for hard, full kernel shells. Harvest when inner shell turns dark.",
                IconClass = "bi-nut-fill",
                BadgeColor = "warning"
            }
        };

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

        public Task<List<CropCalendarEntry>> GetAllCropsAsync()
        {
            return Task.FromResult(ReferenceCrops.ToList());
        }

        public Task<CropCalendarEntry?> GetCropByIdAsync(int id)
        {
            var crop = ReferenceCrops.FirstOrDefault(c => c.Id == id);
            return Task.FromResult(crop);
        }

        public Task<CropCalendarIndexViewModel> GetCalendarModelAsync(
            string? search = null,
            string? category = null,
            string? season = null,
            string? division = null,
            int? month = null,
            string? stage = null)
        {
            int currentMonth = DateTime.Now.Month;
            int activeMonth = month.HasValue && month.Value >= 1 && month.Value <= 12 ? month.Value : currentMonth;

            var filtered = ReferenceCrops.AsQueryable();

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
                BadgeColor = c.BadgeColor
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
            int sowingNow = ReferenceCrops.Count(c => c.SowingMonths.Contains(currentMonth));
            int harvestingNow = ReferenceCrops.Count(c => c.HarvestingMonths.Contains(currentMonth));
            int growingNow = ReferenceCrops.Count(c => c.GrowingMonths.Contains(currentMonth));

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

            return Task.FromResult(viewModel);
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

        private static string GetCurrentSeasonName(int month) => month switch
        {
            11 or 12 or 1 or 2 or 3 => "Rabi Season (রবি মৌসুম - শীতকালীন)",
            4 or 5 or 6 or 7 => "Kharif-1 Season (খরিফ-১ - প্রাক-খরিফ / গ্রীষ্মকালীন)",
            8 or 9 or 10 => "Kharif-2 Season (খরিফ-২ - বর্ষাকালীন)",
            _ => "Rabi Season"
        };

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
            new("Barisal", "Barisal (Southern Coastal Belt)", "বরিশাল (উপকূলীয় অঞ্চল)"),
            new("Dhaka", "Dhaka & Mymensingh", "ঢাকা ও ময়মনসিংহ"),
            new("Sylhet", "Sylhet (Haor & North-East)", "সিলেট (হাওর ও উত্তর-পূর্ব)"),
            new("Chittagong", "Chittagong & Comilla", "চট্টগ্রাম ও কুমিল্লা")
        };
    }
}
