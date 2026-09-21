using KrishiLink.BLL.Helpers;
using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.DAL
{
    /// <summary>
    /// Seeds application roles and crop reference data in every environment after migrations
    /// have been applied separately. Accounts are provisioned through Supabase Auth, not here.
    /// </summary>
    public static class DbInitializer
    {
        // Stable, application-specific PostgreSQL transaction lock key ("KrishiLink" seed namespace).
        // All instances must use this same key before checking or inserting reference data.
        private const long ReferenceDataLockKey = 5931888534012390475;

        public static async Task InitializeAsync(IServiceProvider services)
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({ReferenceDataLockKey})");

            foreach (var role in AppRoles.All)
            {
                if (await roleManager.RoleExistsAsync(role))
                    continue;

                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded)
                {
                    var errors = string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
                    throw new InvalidOperationException($"Failed to seed role '{role}': {errors}");
                }
            }

            await SeedCropCalendarAsync(db);
            await transaction.CommitAsync();
        }

        private static async Task SeedCropCalendarAsync(ApplicationDbContext db)
        {
            var entries = new List<CropCalendarEntry>
            {
                new()
                {
                    Name = "Boro Rice (HYV & Hybrid)",
                    BanglaName = "বোরো ধান (উফশী ও হাইব্রিড)",
                    ScientificName = "Oryza sativa",
                    Category = "Cereals",
                    Season = "Rabi",
                    ProfileCropName = "Rice (Boro)",
                    SowingMonths = new List<int> { 11, 12, 1 },
                    GrowingMonths = new List<int> { 1, 2, 3 },
                    HarvestingMonths = new List<int> { 4, 5 },
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
                new()
                {
                    Name = "T. Aman Rice (Transplanted Aman)",
                    BanglaName = "রোপা আমন ধান (উফশী)",
                    ScientificName = "Oryza sativa",
                    Category = "Cereals",
                    Season = "Kharif-2",
                    ProfileCropName = "Rice (Aman)",
                    SowingMonths = new List<int> { 6, 7, 8 },
                    GrowingMonths = new List<int> { 8, 9, 10 },
                    HarvestingMonths = new List<int> { 11, 12 },
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
                new()
                {
                    Name = "Aus Rice (Upland & Transplanted)",
                    BanglaName = "আউশ ধান (বোনা ও রোপা)",
                    ScientificName = "Oryza sativa",
                    Category = "Cereals",
                    Season = "Kharif-1",
                    ProfileCropName = "Rice (Aus)",
                    SowingMonths = new List<int> { 3, 4 },
                    GrowingMonths = new List<int> { 4, 5, 6 },
                    HarvestingMonths = new List<int> { 6, 7 },
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
                new()
                {
                    Name = "High-Yield Wheat",
                    BanglaName = "উচ্চফলনশীল গম",
                    ScientificName = "Triticum aestivum",
                    Category = "Cereals",
                    Season = "Rabi",
                    ProfileCropName = "Wheat",
                    SowingMonths = new List<int> { 11, 12 },
                    GrowingMonths = new List<int> { 12, 1, 2 },
                    HarvestingMonths = new List<int> { 3, 4 },
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
                new()
                {
                    Name = "Hybrid Maize (Corn)",
                    BanglaName = "হাইব্রিড ভুট্টা",
                    ScientificName = "Zea mays",
                    Category = "Cereals",
                    Season = "Rabi",
                    ProfileCropName = "Maize",
                    SowingMonths = new List<int> { 10, 11, 12 },
                    GrowingMonths = new List<int> { 11, 12, 1, 2 },
                    HarvestingMonths = new List<int> { 3, 4, 5 },
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
                new()
                {
                    Name = "Potato (Winter Table & Seed)",
                    BanglaName = "গোল আলু (ডায়মন্ট ও কার্ডিনাল)",
                    ScientificName = "Solanum tuberosum",
                    Category = "Tubers",
                    Season = "Rabi",
                    ProfileCropName = "Potato",
                    SowingMonths = new List<int> { 10, 11, 12 },
                    GrowingMonths = new List<int> { 11, 12, 1 },
                    HarvestingMonths = new List<int> { 1, 2, 3 },
                    DurationDays = "85 – 100 Days",
                    OptimalTemperature = "15°C – 22°C (Cool nights needed for tuberization)",
                    SoilTypes = "Sandy Loam, Silt Loam with organic matter (বেলে-দোআঁশ)",
                    WaterRequirement = "Medium — 3-4 light irrigations; strictly avoid waterlogging",
                    PopularVarieties = "Diamant, Cardinal, Granola, Asterix, BARI Alu 7, BARI Alu 41",
                    MajorDistricts = "Munshiganj, Bogra, Rangpur, Dinajpur, Joypurhat, Nilphamari",
                    Division = "All",
                    KeyTips = "Earthing-up at 25 and 45 days. Stop irrigation 10-12 days before harvest and dehaulm to harden tuber skin.",
                    IconClass = "bi-circle-fill",
                    BadgeColor = "primary"
                },
                new()
                {
                    Name = "Jute (Toshe & Deshi Golden Fiber)",
                    BanglaName = "তোষা ও দেশী পাট (সোনালী আঁশ)",
                    ScientificName = "Corchorus olitorius / capsularis",
                    Category = "CashCrops",
                    Season = "Kharif-1",
                    ProfileCropName = "Jute",
                    SowingMonths = new List<int> { 3, 4, 5 },
                    GrowingMonths = new List<int> { 4, 5, 6, 7 },
                    HarvestingMonths = new List<int> { 7, 8, 9 },
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
                new()
                {
                    Name = "Mustard & Rapeseed",
                    BanglaName = "উচ্চফলনশীল সরিষা",
                    ScientificName = "Brassica napus / campestris",
                    Category = "Oilseeds",
                    Season = "Rabi",
                    ProfileCropName = "Oilseeds",
                    SowingMonths = new List<int> { 10, 11 },
                    GrowingMonths = new List<int> { 11, 12, 1 },
                    HarvestingMonths = new List<int> { 1, 2 },
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
                new()
                {
                    Name = "Lentil (Masur Dal)",
                    BanglaName = "মসুর ডাল",
                    ScientificName = "Lens culinaris",
                    Category = "Pulses",
                    Season = "Rabi",
                    ProfileCropName = "Pulses",
                    SowingMonths = new List<int> { 10, 11 },
                    GrowingMonths = new List<int> { 11, 12, 1 },
                    HarvestingMonths = new List<int> { 2, 3 },
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
                new()
                {
                    Name = "Chickpea (Chhola)",
                    BanglaName = "ছোলা",
                    ScientificName = "Cicer arietinum",
                    Category = "Pulses",
                    Season = "Rabi",
                    ProfileCropName = "Pulses",
                    SowingMonths = new List<int> { 10, 11 },
                    GrowingMonths = new List<int> { 11, 12, 1, 2 },
                    HarvestingMonths = new List<int> { 3, 4 },
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
                new()
                {
                    Name = "Mungbean (Mug Dal)",
                    BanglaName = "মুগ ডাল (গ্রীষ্ম ও খরিফ)",
                    ScientificName = "Vigna radiata",
                    Category = "Pulses",
                    Season = "Kharif-1",
                    ProfileCropName = "Pulses",
                    SowingMonths = new List<int> { 2, 3, 8 },
                    GrowingMonths = new List<int> { 3, 4, 9 },
                    HarvestingMonths = new List<int> { 4, 5, 10 },
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
                new()
                {
                    Name = "Winter Onion",
                    BanglaName = "শীতকালীন পেঁয়াজ (তাহেরপুরী ও বারি)",
                    ScientificName = "Allium cepa",
                    Category = "Spices",
                    Season = "Rabi",
                    ProfileCropName = "Spices",
                    SowingMonths = new List<int> { 10, 11, 12 },
                    GrowingMonths = new List<int> { 12, 1, 2 },
                    HarvestingMonths = new List<int> { 3, 4 },
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
                new()
                {
                    Name = "Summer & Monsoon Onion",
                    BanglaName = "গ্রীষ্মকালীন ও বর্ষাকালীন পেঁয়াজ",
                    ScientificName = "Allium cepa",
                    Category = "Spices",
                    Season = "Kharif-1",
                    ProfileCropName = "Spices",
                    SowingMonths = new List<int> { 2, 3, 4 },
                    GrowingMonths = new List<int> { 3, 4, 5 },
                    HarvestingMonths = new List<int> { 6, 7, 8 },
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
                new()
                {
                    Name = "Garlic (Winter & Zero Tillage)",
                    BanglaName = "রসুন (বিনা চাষ ও সাধারণ)",
                    ScientificName = "Allium sativum",
                    Category = "Spices",
                    Season = "Rabi",
                    ProfileCropName = "Spices",
                    SowingMonths = new List<int> { 10, 11 },
                    GrowingMonths = new List<int> { 11, 12, 1, 2 },
                    HarvestingMonths = new List<int> { 3, 4 },
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
                new()
                {
                    Name = "Chili / Green & Red Pepper",
                    BanglaName = "কাঁচা ও শুকনো মরিচ",
                    ScientificName = "Capsicum annuum",
                    Category = "Spices",
                    Season = "Rabi",
                    ProfileCropName = "Spices",
                    SowingMonths = new List<int> { 10, 11, 3 },
                    GrowingMonths = new List<int> { 11, 12, 4 },
                    HarvestingMonths = new List<int> { 1, 2, 3, 4, 5 },
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
                new()
                {
                    Name = "Winter Tomato (HYV & Hybrid)",
                    BanglaName = "শীতকালীন টমেটো (উফশী ও হাইব্রিড)",
                    ScientificName = "Solanum lycopersicum",
                    Category = "Vegetables",
                    Season = "Rabi",
                    ProfileCropName = "Vegetables",
                    SowingMonths = new List<int> { 9, 10, 11 },
                    GrowingMonths = new List<int> { 11, 12 },
                    HarvestingMonths = new List<int> { 1, 2, 3 },
                    DurationDays = "90 – 110 Days",
                    OptimalTemperature = "18°C – 27°C (Nights of 15–18°C trigger flowering)",
                    SoilTypes = "Rich Loam, Sandy Loam (উর্বর দোআঁশ)",
                    WaterRequirement = "Medium — regular furrow irrigation; avoid splashing leaves to prevent blight",
                    PopularVarieties = "BARI Tomato 14, BARI Tomato 15, Ratan, Bahar, Beautiful",
                    MajorDistricts = "Jessore, Comilla, Dinajpur, Bogra, Rajshahi, Chittagong",
                    Division = "All",
                    KeyTips = "Staking with bamboo poles improves fruit size and prevents fungal soil rot. Apply Boron to stop fruit cracking.",
                    IconClass = "bi-egg-fill",
                    BadgeColor = "info"
                },
                new()
                {
                    Name = "Brinjal / Eggplant (Aubergine)",
                    BanglaName = "বেগুন (উফশী ও বিটি বেগুন)",
                    ScientificName = "Solanum melongena",
                    Category = "Vegetables",
                    Season = "YearRound",
                    ProfileCropName = "Vegetables",
                    SowingMonths = new List<int> { 8, 9, 10, 4 },
                    GrowingMonths = new List<int> { 10, 11, 5 },
                    HarvestingMonths = new List<int> { 11, 12, 1, 2, 6, 7 },
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
                new()
                {
                    Name = "Cabbage & Cauliflower",
                    BanglaName = "বাঁধাকপি ও ফুলকপি",
                    ScientificName = "Brassica oleracea",
                    Category = "Vegetables",
                    Season = "Rabi",
                    ProfileCropName = "Vegetables",
                    SowingMonths = new List<int> { 9, 10, 11 },
                    GrowingMonths = new List<int> { 11, 12 },
                    HarvestingMonths = new List<int> { 12, 1, 2 },
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
                new()
                {
                    Name = "Country Bean (Sheem)",
                    BanglaName = "শিম (দেশী ও উফশী)",
                    ScientificName = "Lablab purpureus",
                    Category = "Vegetables",
                    Season = "Rabi",
                    ProfileCropName = "Vegetables",
                    SowingMonths = new List<int> { 6, 7, 8 },
                    GrowingMonths = new List<int> { 9, 10, 11 },
                    HarvestingMonths = new List<int> { 11, 12, 1, 2, 3 },
                    DurationDays = "140 – 180 Days",
                    OptimalTemperature = "18°C – 28°C",
                    SoilTypes = "Fertile Sandy Loam, Clay Loam (উর্বর দোআঁশ)",
                    WaterRequirement = "Medium — trellis cultivation with trench drainage",
                    PopularVarieties = "BARI Sheem 1, BARI Sheem 6, IPSA Sheem 2, Rupban",
                    MajorDistricts = "Chattogram, Cox's Bazar, Cumilla, Jessore, Mymensingh",
                    Division = "All",
                    KeyTips = "Provide sturdy bamboo macha/trellis. Control aphids and pod borers at early flowering stage.",
                    IconClass = "bi-flower3",
                    BadgeColor = "info"
                },
                new()
                {
                    Name = "Watermelon (Coastal & Char)",
                    BanglaName = "তরমুজ (উপকূলীয় ও চর)",
                    ScientificName = "Citrullus lanatus",
                    Category = "Fruits",
                    Season = "Rabi",
                    ProfileCropName = "Fruits",
                    SowingMonths = new List<int> { 12, 1 },
                    GrowingMonths = new List<int> { 1, 2 },
                    HarvestingMonths = new List<int> { 3, 4, 5 },
                    DurationDays = "80 – 95 Days",
                    OptimalTemperature = "24°C – 35°C (Warm sun raises sugar content)",
                    SoilTypes = "Sandy Loam, River Char lands (বেলে-দোআঁশ ও নদীর চর)",
                    WaterRequirement = "Medium — pit method with localized basin watering; avoid flooding vines",
                    PopularVarieties = "Dragon, Black Diamond, Pakiza, Sweet Miracle, Big Top",
                    MajorDistricts = "Patuakhali (Galachipa), Bhola, Barisal, Barguna, Noakhali, Natore",
                    Division = "Barisal, Chittagong, Khulna",
                    KeyTips = "Major coastal cash crop. Lay dry straw under developing melons to prevent soil dampness and spot marks.",
                    IconClass = "bi-heart-fill",
                    BadgeColor = "success"
                },
                new()
                {
                    Name = "Sugarcane (Commercial Annual)",
                    BanglaName = "আখ (বার্ষিক অর্থকরী ফসল)",
                    ScientificName = "Saccharum officinarum",
                    Category = "CashCrops",
                    Season = "YearRound",
                    ProfileCropName = "Other",
                    SowingMonths = new List<int> { 10, 11, 2, 3 },
                    GrowingMonths = new List<int> { 12, 1, 4, 5, 6, 7, 8, 9, 10 },
                    HarvestingMonths = new List<int> { 11, 12, 1, 2, 3 },
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
                new()
                {
                    Name = "Sunflower (Saline-Tolerant Oilseed)",
                    BanglaName = "সূর্যমুখী (লবণাক্ততা সহনশীল)",
                    ScientificName = "Helianthus annuus",
                    Category = "Oilseeds",
                    Season = "Rabi",
                    ProfileCropName = "Oilseeds",
                    SowingMonths = new List<int> { 11, 12 },
                    GrowingMonths = new List<int> { 12, 1, 2 },
                    HarvestingMonths = new List<int> { 3, 4 },
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
                new()
                {
                    Name = "Groundnut / Peanut (Char & Sandy Soils)",
                    BanglaName = "চীনাবাদাম (চর ও বেলে দোআঁশ)",
                    ScientificName = "Arachis hypogaea",
                    Category = "Oilseeds",
                    Season = "Rabi",
                    ProfileCropName = "Oilseeds",
                    SowingMonths = new List<int> { 11, 12, 5 },
                    GrowingMonths = new List<int> { 12, 1, 2, 6, 7 },
                    HarvestingMonths = new List<int> { 3, 4, 9 },
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

            var existingNames = (await db.CropCalendarEntries
                .Select(entry => entry.Name)
                .ToListAsync()).ToHashSet(StringComparer.Ordinal);
            var missingEntries = entries.Where(entry => !existingNames.Contains(entry.Name)).ToList();
            if (missingEntries.Count == 0)
                return;

            await db.CropCalendarEntries.AddRangeAsync(missingEntries);
            await db.SaveChangesAsync();
        }
    }
}
