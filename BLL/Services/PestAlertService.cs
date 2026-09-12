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
        // 1. DAE, BARI & BRRI Agrometeorological Rule Knowledge Base
        private static readonly List<PestDiseaseRule> DiseaseRules = new()
        {
            new PestDiseaseRule
            {
                Id = 1,
                DiseaseName = "Rice Blast (Leaf & Neck Blast)",
                BanglaName = "ধানের ব্লাস্ট রোগ (পাতা ও শিষ ব্লাস্ট)",
                PathogenOrPest = "Pyricularia oryzae (Fungus)",
                Category = "Fungal Disease",
                TargetCrops = new List<string> { "Boro Rice", "T. Aman Rice", "Aus Rice", "Rice" },
                MinTemp = 20.0,
                MaxTemp = 29.0,
                MinHumidity = 85.0,
                MaxHumidity = 100.0,
                MatchingWeatherConditions = new List<string> { "Cloudy", "Rainy", "Foggy", "Humid", "Overcast", "Showers" },
                Severity = "Critical",
                Symptoms = "Spindle-shaped grayish-white spots on leaves with brown margins; blackened rotten neck node preventing grain filling.",
                BanglaSymptoms = "পাতায় দুই প্রান্ত চোখা লম্বাটে ধূসর দাগ; শিষের গোড়া কালো হয়ে পচে যায় এবং চিটা হয়ে যায়।",
                TriggerReason = "Moderate temperature (20°C–29°C) combined with prolonged high relative humidity (≥ 85%) and dewy leaf wetness creates optimal spore germination conditions.",
                BanglaTriggerReason = "২০°-২৯° সেলসিয়াস তাপমাত্রা ও ৮৫% এর বেশি আর্দ্রতার সাথে কুয়াশা বা শিশির জমে থাকা ধানের ব্লাস্ট ছত্রাকের বংশবৃদ্ধির জন্য অনুকূল।",
                ActionableRemedies = new List<string>
                {
                    "Immediately stop applying top-dressing Urea fertilizer to prevent disease escalation.",
                    "Maintain 3-5 cm standing water in the field to keep plant canopy cool.",
                    "Spray Tricyclazole 75WP (Trooper / Beam) at 0.75 g/L or Nativo 75WG at 0.6 g/L in late afternoon."
                },
                BanglaActionableRemedies = new List<string>
                {
                    "জমিতে ইউরিয়া সারের উপরিপ্রয়োগ তাৎক্ষণিকভাবে বন্ধ রাখুন।",
                    "ক্ষেতে ৩-৫ সেমি পানি ধরে রাখুন যাতে চারার গোড়া শুকিয়ে না যায়।",
                    "বিকালে ট্রাইসাইক্লাজোল ৭৫ ডব্লিউপি (ট্রুপার / বিম) ০.৭৫ গ্রাম/লিটার অথবা নেটিভো ৭৫ ডব্লিউজি ০.৬ গ্রাম/লিটার স্প্রে করুন।"
                },
                PreventiveSpray = "Tricyclazole 75WP (0.75 g/L) or Nativo 75WG (0.6 g/L)",
                OrganicControl = "Neem oil spray (3 ml/L) mixed with mild soapy water at early tillering stage.",
                IconClass = "bi-shield-fill-x",
                BadgeClass = "danger"
            },
            new PestDiseaseRule
            {
                Id = 2,
                DiseaseName = "Potato & Tomato Late Blight",
                BanglaName = "আলু ও টমেটোর নাবী ধসা (লেট ব্লাইট)",
                PathogenOrPest = "Phytophthora infestans (Oomycete)",
                Category = "Fungal Disease",
                TargetCrops = new List<string> { "Potato", "Tomato", "Winter Tomato" },
                MinTemp = 10.0,
                MaxTemp = 21.0,
                MinHumidity = 88.0,
                MaxHumidity = 100.0,
                MatchingWeatherConditions = new List<string> { "Foggy", "Overcast", "Cloudy", "Misty", "Humid" },
                RequiresConsecutiveDays = true,
                Severity = "Critical",
                Symptoms = "Water-soaked dark lesions on leaf tips that rapidly turn black; fluffy white fungal growth on undersides in morning dew; rotting tubers with foul odor.",
                BanglaSymptoms = "পাতার ডগায় পানিভেজা কালচে দাগ দ্রুত ছড়িয়ে পাতা ঝলসে যায়; সকালে পাতার নিচের পিঠে সাদা ছত্রাক দেখা যায়।",
                TriggerReason = "Cool temperatures (10°C–21°C) with dense fog, cloudy skies, and humidity > 88% create high late-blight epiphytotic risk within 48 hours.",
                BanglaTriggerReason = "১০°-২১° সেলসিয়াস ঠান্ডা আবহাওয়া, ঘন কুয়াশা ও ৮৮% এর বেশি আর্দ্রতা আলুর নাবী ধসা মহামারী তৈরির প্রধান কারণ।",
                ActionableRemedies = new List<string>
                {
                    "Stop field irrigation immediately — moist soil accelerates tuber rot.",
                    "Spray prophylactic contact fungicide Mancozeb (Dithane M-45 / Indofil) at 2 g/L before heavy fog sets in.",
                    "If active spots appear, immediately switch to systemic fungicide: Cymoxanil + Mancozeb (Secure / Acrobat) at 2.5 g/L."
                },
                BanglaActionableRemedies = new List<string>
                {
                    "ক্ষেতে সেচ দেওয়া অবিলম্বে বন্ধ করুন — ভেজা মাটিতে কন্দ পচে যায়।",
                    "কুয়াশা পড়ার আগেই আগাম প্রতিরোধক হিসেবে ম্যানকোজেব (ডাইথেন এম-৪৫) ২ গ্রাম/লিটার স্প্রে করুন।",
                    "আক্রান্ত হলে তাৎক্ষণিক প্রতিষেধক সিকিউর / এক্রোব্যাট ২.৫ গ্রাম/লিটার হারে পানিতে মিশিয়ে স্প্রে করুন।"
                },
                PreventiveSpray = "Mancozeb 80WP (2 g/L) or Cymoxanil + Mancozeb (2.5 g/L)",
                OrganicControl = "Bordeaux mixture (1%) or Copper oxychloride (2 g/L) applied uniformly to foliage.",
                IconClass = "bi-virus",
                BadgeClass = "danger"
            },
            new PestDiseaseRule
            {
                Id = 3,
                DiseaseName = "Brown Plant Hopper - BPH (Current Poka)",
                BanglaName = "বাদামি গাছফড়িং (কারেন্ট পোকা)",
                PathogenOrPest = "Nilaparvata lugens (Insect Pest)",
                Category = "Insect Pest",
                TargetCrops = new List<string> { "T. Aman Rice", "Boro Rice", "Rice" },
                MinTemp = 25.0,
                MaxTemp = 34.0,
                MinHumidity = 80.0,
                MaxHumidity = 98.0,
                MatchingWeatherConditions = new List<string> { "Warm & Humid", "Humid", "Cloudy", "Showers", "Overcast" },
                Severity = "High",
                Symptoms = "Circular drying patches ('Hopper Burn') where plants look burnt brown overnight due to sap sucking at the base of tillers.",
                BanglaSymptoms = "ধানক্ষেতের ভেতর গোল গোল জায়গা পুড়ে যাওয়ার মতো শুকিয়ে মরে যায় (হপার বার্ন)। গাছের গোড়ায় দলবদ্ধ পোকা দেখা যায়।",
                TriggerReason = "Warm, sultry humid weather (25°C–34°C, humidity ≥ 80%) in dense tillering plots triggers explosive reproductive cycle.",
                BanglaTriggerReason = "গুমোট গরম ও আর্দ্র আবহাওয়ায় (২৫°-৩৪° সে., আর্দ্রতা ৮০%+) ঘন ধানের চারার গোড়ায় বাদামি গাছফড়িং বিদ্যুৎ গতিতে বংশবৃদ্ধি করে।",
                ActionableRemedies = new List<string>
                {
                    "Create 30 cm alleyways ('Bili Kata') every 10-12 rows for sunlight and aeration to the tiller bases.",
                    "Drain all standing water from the field for 3-4 days to expose the insect colony.",
                    "Spray Pymetrozine (Chess 50WG) at 0.6 g/L or Dinotefuran (Oshin) directing spray nozzle directly at base of plants."
                },
                BanglaActionableRemedies = new List<string>
                {
                    "প্রতি ১০-১২ সারি পর পর বিলি কেটে (বিলি কাটা) আলো-বাতাস চলাচলের ব্যবস্থা করুন।",
                    "জমির পানি ৩-৪ দিনের জন্য পুরোপুরি নামিয়ে দিন যাতে পোকা মাটিতে পড়ে মারা যায়।",
                    "গাছের গোড়া লক্ষ্য করে পাইমেট্রোজিন (চেস ৫০ ডব্লিউজি) ০.৬ গ্রাম/লিটার স্প্রে করুন।"
                },
                PreventiveSpray = "Pymetrozine 50WG (0.6 g/L) or Dinotefuran 20SG",
                OrganicControl = "Conserve natural spider predators; place light traps at night to attract and destroy adult hoppers.",
                IconClass = "bi-bug-fill",
                BadgeClass = "warning"
            },
            new PestDiseaseRule
            {
                Id = 4,
                DiseaseName = "Wheat Blast",
                BanglaName = "গমের ব্লাস্ট রোগ",
                PathogenOrPest = "Magnaporthe oryzae (Fungus)",
                Category = "Fungal Disease",
                TargetCrops = new List<string> { "Wheat", "High-Yield Wheat" },
                MinTemp = 22.0,
                MaxTemp = 28.0,
                MinHumidity = 85.0,
                MaxHumidity = 100.0,
                MatchingWeatherConditions = new List<string> { "Cloudy", "Overcast", "Showers", "Rainy", "Misty" },
                Severity = "High",
                Symptoms = "Complete bleaching/whitening of wheat spikes above the infected rachis node while lower parts remain green; grains become shriveled and chaffy.",
                BanglaSymptoms = "গমের শিষের আক্রান্ত অংশের উপরের অংশ সাদা বা বিবর্ণ হয়ে শুকিয়ে যায় কিন্তু নিচের অংশ সবুজ থাকে; শিষের দানা চিটা হয়ে যায়।",
                TriggerReason = "Unseasonal warm, cloudy days with light rain or mist (22°C–28°C, humidity > 85%) during heading/flowering stage in Feb-March.",
                BanglaTriggerReason = "ফেব্রুয়ারি-মার্চে গমের শিষ বের হওয়ার সময় মেঘলা আকাশ, গুঁড়ি গুঁড়ি বৃষ্টি ও ৮৫%+ আর্দ্রতা থাকলে গম ব্লাস্টের মারাত্মক ঝুঁকি তৈরি হয়।",
                ActionableRemedies = new List<string>
                {
                    "Cultivate blast-resistant wheat variety BARI Gom-33 in southwestern and high-risk districts.",
                    "Spray Nativo 75WG (Tebuconazole + Trifloxystrobin) at 0.6 g/L twice: 1st at 50% heading and 2nd 12-14 days later.",
                    "Never harvest seed stock from infected fields for next year's planting."
                },
                BanglaActionableRemedies = new List<string>
                {
                    "ব্লাস্ট প্রতিরোধী জাত বারি গম-৩৩ চাষ করুন।",
                    "গমের শিষ বের হওয়ার সময় (৫০% হেডিং) একবার এবং ১২-১৪ দিন পর আরেকবার নেটিভো ৭৫ ডব্লিউজি ০.৬ গ্রাম/লিটার স্প্রে করুন।",
                    "আক্রান্ত ক্ষেতের গম কোনোভাবেই পরবর্তী বছরের বীজ হিসেবে সংরক্ষণ করবেন না।"
                },
                PreventiveSpray = "Nativo 75WG (0.6 g/L) or Amistar Top (1 ml/L)",
                OrganicControl = "Seed treatment with Trichoderma viride bio-fungicide (5 g/kg seed) before sowing.",
                IconClass = "bi-tsunami",
                BadgeClass = "warning"
            },
            new PestDiseaseRule
            {
                Id = 5,
                DiseaseName = "Mustard Aphids (Jab Poka)",
                BanglaName = "সরিষার জাবপোকা",
                PathogenOrPest = "Lipaphis erysimi (Insect Pest)",
                Category = "Insect Pest",
                TargetCrops = new List<string> { "Mustard", "Rapeseed", "Lentil" },
                MinTemp = 14.0,
                MaxTemp = 24.0,
                MinHumidity = 70.0,
                MaxHumidity = 88.0,
                MatchingWeatherConditions = new List<string> { "Cloudy", "Overcast", "Foggy", "Misty" },
                Severity = "Moderate",
                Symptoms = "Dense colonies of tiny greenish-black insects sucking sap from flower buds, pods, and tender shoots; leaves curl and turn black with sooty mold.",
                BanglaSymptoms = "সরিষার ফুল, কুঁড়ি ও কচি শুঁটিতে অসংখ্য ছোট কালচে পোকা রস চুষে খায়; ফুল শুকিয়ে যায় এবং ফলন মারাত্মকভাবে কমে যায়।",
                TriggerReason = "Cool, cloudy, overcast weather without rain (14°C–24°C, humidity 70%–88%) during flowering season (Dec–Jan).",
                BanglaTriggerReason = "পৌষ-মাঘ মাসে মেঘলা, কুয়াশাচ্ছন্ন ও বৃষ্টিহীন আবহাওয়া (১৪°-২৪° সে.) সরিষার জাবপোকার দ্রুত বিস্তারের প্রধান কারণ।",
                ActionableRemedies = new List<string>
                {
                    "Inspect flowering twigs in morning hours when aphids cluster densely.",
                    "Spray Imidacloprid (Admire 20SL / Gain) at 0.5 ml/L or Malathion 57EC at 2 ml/L in early morning before bees forage.",
                    "Set up yellow sticky traps (15-20 traps/acre) across the mustard field."
                },
                BanglaActionableRemedies = new List<string>
                {
                    "সকালে ফুল ও কচি ডাল ভালোভাবে পর্যবেক্ষণ করুন।",
                    "মৌমাছির ক্ষতি এড়াতে খুব সকালে ইমিডাক্লোপ্রিড (এডমায়ার ২০ এসএল) ০.৫ মিলি/লিটার স্প্রে করুন।",
                    "জমিতে প্রতি বিঘায় ৫-৬টি হলুদ আঠালো ফাঁদ (Yellow sticky trap) স্থাপন করুন।"
                },
                PreventiveSpray = "Imidacloprid 20SL (0.5 ml/L) or Acetamiprid 20SP (0.5 g/L)",
                OrganicControl = "Spray Neem seed kernel extract (NSKE 5%) or tobacco leaf decoction in evening.",
                IconClass = "bi-brightness-high-fill",
                BadgeClass = "warning"
            },
            new PestDiseaseRule
            {
                Id = 6,
                DiseaseName = "Fall Armyworm (FAW)",
                BanglaName = "ভুট্টার ফল আর্মিওয়ার্ম",
                PathogenOrPest = "Spodoptera frugiperda (Caterpillar)",
                Category = "Insect Pest",
                TargetCrops = new List<string> { "Maize", "Hybrid Maize", "Corn" },
                MinTemp = 24.0,
                MaxTemp = 33.0,
                MinHumidity = 60.0,
                MaxHumidity = 85.0,
                MatchingWeatherConditions = new List<string> { "Warm & Humid", "Clear & Warm", "Sunny", "Cloudy" },
                Severity = "High",
                Symptoms = "Shot-hole and window-pane feeding holes on leaves; copious sawdust-like frass (caterpillar droppings) inside whorls; boring into developing cobs.",
                BanglaSymptoms = "পাতার মাঝে জালি কাটা ও বড় বড় ফুটো দাগ; ভুট্টার মোচড় ও ভরের ভেতর করাতের গুঁড়োর মতো পোকার মল দেখা যায়।",
                TriggerReason = "Warm temperatures (24°C–33°C) and moderate humidity during maize vegetative whorl stage encourage rapid caterpillar feeding and egg hatching.",
                BanglaTriggerReason = "২৪°-৩৩° সেলসিয়াস গরম আবহাওয়ায় ভুট্টার বাড়ন্ত পর্যায়ে ফল আর্মিওয়ার্ম কীড়ার খাবার গ্রহণের হার কয়েকগুণ বেড়ে যায়।",
                ActionableRemedies = new List<string>
                {
                    "Install FAW Sex Pheromone traps (Spodo-lure) at 5 traps/bigha for mass trapping.",
                    "Handpick and crush egg masses and early-instar larvae during weekly scouting.",
                    "Apply Emamectin Benzoate (Proclaim 5SG) at 1 g/L or Chlorantraniliprole (Coragen) at 0.4 ml/L directly into whorls."
                },
                BanglaActionableRemedies = new List<string>
                {
                    "প্রতি বিঘায় ৪-৫টি সেক্স ফেরোমোন ফাঁদ স্থাপন করুন।",
                    "সপ্তাহে ২ দিন মাঠ পরিদর্শন করে পাতার নিচের ডিমের গাদা ও কচি কীড়া হাত দিয়ে পিষে মারুন।",
                    "কীড়া দেখা দিলে ইমমেকটিন বেনজয়েট (প্রোক্লেইম ৫ এসজি) ১ গ্রাম/লিটার হারে ভুট্টার ভরের ভেতর সরাসরি স্প্রে করুন।"
                },
                PreventiveSpray = "Emamectin Benzoate 5SG (1 g/L) or Spinetoram 11.7SC (0.5 ml/L)",
                OrganicControl = "Apply dry sand mixed with wood ash (1:1 ratio) directly into central plant whorls.",
                IconClass = "bi-sun-fill",
                BadgeClass = "warning"
            },
            new PestDiseaseRule
            {
                Id = 7,
                DiseaseName = "Rice Bacterial Leaf Blight (BLB)",
                BanglaName = "ধানের পাতা পোড়া রোগ (বিএলবি)",
                PathogenOrPest = "Xanthomonas oryzae pv. oryzae (Bacteria)",
                Category = "Bacterial Disease",
                TargetCrops = new List<string> { "T. Aman Rice", "Boro Rice", "Rice" },
                MinTemp = 25.0,
                MaxTemp = 35.0,
                MinHumidity = 85.0,
                MaxHumidity = 100.0,
                MatchingWeatherConditions = new List<string> { "Rainy", "Thunderstorm", "Showers", "Cloudy", "Humid" },
                Severity = "High",
                Symptoms = "Water-soaked lesions on leaf margins turning wavy yellow-white with milky bacterial ooze droplets in early morning; entire leaf dries up from tip downward.",
                BanglaSymptoms = "পাতার কিনারা দিয়ে ঢেউ খেলানো হলুদ বা সাদাটে দাগ হয়ে শুকিয়ে যায়; সকালে পাতার ওপর পুটুলির মতো ব্যাকটেরিয়ার রস দেখা যায়।",
                TriggerReason = "Heavy rainstorms, stormy winds, and humidity > 85% with warm temperatures cause leaf injury and rapid bacterial transmission.",
                BanglaTriggerReason = "ঝড়ো বাতাস ও ভারি বৃষ্টির সময় ধানের পাতায় ক্ষত সৃষ্টি হয়ে এবং ৮৫%+ আর্দ্রতায় ব্যাকটেরিয়ার দ্রুত সংক্রামণ ঘটে।",
                ActionableRemedies = new List<string>
                {
                    "Apply supplemental Muriate of Potash (MoP) at 10 kg/acre to boost cell wall immunity.",
                    "Stop top-dressing Nitrogen/Urea immediately.",
                    "Spray Copper Oxychloride (Cuprocaffaro / Champion) at 2 g/L mixed with Validamycin at 2 ml/L."
                },
                BanglaActionableRemedies = new List<string>
                {
                    "রোগের প্রকোপ কমাতে জমিতে বিঘা প্রতি ৫ কেজি অতিরিক্ত পটাশ সার (MoP) প্রয়োগ করুন।",
                    "ইউরিয়া সারের উপরিপ্রয়োগ সম্পূর্ণ বন্ধ রাখুন।",
                    "কপার অক্সিক্লোরাইড ২ গ্রাম/লিটার এবং ভ্যালিডামাইসিন ২ মিলি/লিটার মিশিয়ে স্প্রে করুন।"
                },
                PreventiveSpray = "Copper Oxychloride 50WP (2 g/L) + Validamycin 3L (2 ml/L)",
                OrganicControl = "Drain field water; apply fresh cow dung water supernatant (5%) as a biological barrier.",
                IconClass = "bi-flower2",
                BadgeClass = "warning"
            },
            new PestDiseaseRule
            {
                Id = 8,
                DiseaseName = "Eggplant Shoot & Fruit Borer (FSB)",
                BanglaName = "বেগুনের ডগা ও ফল ছিদ্রকারী পোকা",
                PathogenOrPest = "Leucinodes orbonalis (Insect Pest)",
                Category = "Insect Pest",
                TargetCrops = new List<string> { "Brinjal", "Eggplant", "Aubergine" },
                MinTemp = 25.0,
                MaxTemp = 36.0,
                MinHumidity = 75.0,
                MaxHumidity = 95.0,
                MatchingWeatherConditions = new List<string> { "Warm & Humid", "Cloudy", "Sunny", "Humid" },
                Severity = "Moderate",
                Symptoms = "Wilting and drooping of top tender shoots; round exit holes in brinjal fruits with black excreta inside.",
                BanglaSymptoms = "গাছের কচি ডগা নুয়ে শুকিয়ে মরে যায়; বেগুনের ভেতর পোকা ছিদ্র করে মল ত্যাগ করে ফল খাওয়ার অযোগ্য করে তোলে।",
                TriggerReason = "Warm and humid conditions (25°C–36°C, humidity ≥ 75%) promote year-round breeding of female moths.",
                BanglaTriggerReason = "উষ্ণ ও আর্দ্র আবহাওয়ায় বেগুনের জমিতে এই মথ পোকার ডিম পাড়ার হার ও কীড়ার আক্রমণ বৃদ্ধি পায়।",
                ActionableRemedies = new List<string>
                {
                    "Regularly clip off wilted shoots containing larvae and bury them deep in soil.",
                    "Install Lucin-lure sex pheromone traps (10-12 traps/acre) at canopy level.",
                    "Use bio-pesticide Bacillus thuringiensis (Bt) or Spinosad (Tracer 45SC) at 0.4 ml/L."
                },
                BanglaActionableRemedies = new List<string>
                {
                    "আক্রান্ত মরা ডগা পোকার কীড়াসহ কেটে মাটিতে পুঁতে ফেলুন।",
                    "জমিতে লিউসিন-লিউরের ফেরোমোন ফাঁদ (বিঘায় ৪-৫টি) পাতার উচ্চতায় ঝুলিয়ে দিন।",
                    "বায়ো-কীটনাশক স্পাইনোস্যাড (ট্রেসার ৪৫ এসসি) ০.৪ মিলি/লিটার স্প্রে করুন।"
                },
                PreventiveSpray = "Spinosad 45SC (0.4 ml/L) or Chlorantraniliprole 18.5SC",
                OrganicControl = "Plant Bt Brinjal for 100% natural immunity or spray microbial pesticide Beauveria bassiana.",
                IconClass = "bi-egg-fill",
                BadgeClass = "warning"
            },
            new PestDiseaseRule
            {
                Id = 9,
                DiseaseName = "Chili Anthracnose (Die-back & Fruit Rot)",
                BanglaName = "মরিচের অ্যানথ্রাকনোজ (ফল পচা ও ডাই-ব্যাক)",
                PathogenOrPest = "Colletotrichum capsici (Fungus)",
                Category = "Fungal Disease",
                TargetCrops = new List<string> { "Chili", "Green Pepper", "Pepper" },
                MinTemp = 24.0,
                MaxTemp = 32.0,
                MinHumidity = 80.0,
                MaxHumidity = 98.0,
                MatchingWeatherConditions = new List<string> { "Rainy", "Showers", "Humid", "Cloudy", "Warm & Humid" },
                Severity = "Moderate",
                Symptoms = "Circular sunken brown spots on ripe chili pods with concentric rings of fungal dots; drying of twigs from tip downward (die-back).",
                BanglaSymptoms = "পাকা মরিচে ভেতরের দিকে দেবে যাওয়া গোলাকার কালচে দাগ এবং ডাল ওপর থেকে নিচের দিকে শুকিয়ে মরে যাওয়া (ডাই-ব্যাক)।",
                TriggerReason = "Intermittent warm monsoon rains or heavy morning dew with humidity > 80% and temp 24°C–32°C.",
                BanglaTriggerReason = "মাঝারি গরমের সাথে গুঁড়ি গুঁড়ি বৃষ্টি ও ৮০% এর বেশি আর্দ্রতা মরিচের ফল পচা রোগের অনুকূল পরিবেশ তৈরি করে।",
                ActionableRemedies = new List<string>
                {
                    "Collect and destroy all diseased and fallen chilis to eliminate inoculum sources.",
                    "Spray Carbendazim (Autostin 50WP) at 2 g/L or Azoxystrobin + Difenoconazole (Amistar Top) at 1 ml/L at flowering stage.",
                    "Ensure swift furrow drainage so no standing water remains around chili roots."
                },
                BanglaActionableRemedies = new List<string>
                {
                    "আক্রান্ত ও ঝরে পড়া মরিচ কুড়িয়ে মাঠের বাইরে পুড়িয়ে ফেলুন।",
                    "ফুল ও ফল আসার সময় কার্বেনডাজিম (অটোস্টিন ৫০ ডব্লিউপি) ২ গ্রাম/লিটার বা এমিস্টার টপ ১ মিলি/লিটার স্প্রে করুন।",
                    "মরিচের জমিতে যাতে পানি জমে না থাকে সেজন্য নিকাশ নালা পরিষ্কার রাখুন।"
                },
                PreventiveSpray = "Carbendazim 50WP (2 g/L) or Difenoconazole 250EC (1 ml/L)",
                OrganicControl = "Seed treatment with hot water (50°C for 25 min) or Trichoderma harzianum bio-agent.",
                IconClass = "bi-fire",
                BadgeClass = "warning"
            }
        };

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

        public Task<RegionalWeatherForecast> GetRegionalWeatherAsync(string district)
        {
            string key = string.IsNullOrWhiteSpace(district) ? "Bogra" : district.Trim();

            // Handle Bogra / Bogura alternate spelling
            if (key.Contains("Bogura", StringComparison.OrdinalIgnoreCase) || key.Contains("Bogra", StringComparison.OrdinalIgnoreCase))
            {
                var baseF = DistrictForecasts["Bogra"];
                return Task.FromResult(new RegionalWeatherForecast
                {
                    District = "Bogra",
                    Division = baseF.Division,
                    Temperature = baseF.Temperature,
                    MinTemp = baseF.MinTemp,
                    MaxTemp = baseF.MaxTemp,
                    Humidity = baseF.Humidity,
                    RainProbability = baseF.RainProbability,
                    WindSpeedKmh = baseF.WindSpeedKmh,
                    Condition = baseF.Condition,
                    BanglaCondition = baseF.BanglaCondition,
                    ConditionIcon = baseF.ConditionIcon,
                    ForecastDate = baseF.ForecastDate,
                    FiveDayForecast = baseF.FiveDayForecast
                });
            }

            // Search dictionary by direct match or prefix (e.g. "Jessore, Khulna" -> "Jessore")
            foreach (var kvp in DistrictForecasts)
            {
                if (key.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(kvp.Value);
                }
            }

            // Default fallback
            var defaultF = DistrictForecasts["Bogra"];
            return Task.FromResult(new RegionalWeatherForecast
            {
                District = "Bogra",
                Division = defaultF.Division,
                Temperature = defaultF.Temperature,
                MinTemp = defaultF.MinTemp,
                MaxTemp = defaultF.MaxTemp,
                Humidity = defaultF.Humidity,
                RainProbability = defaultF.RainProbability,
                WindSpeedKmh = defaultF.WindSpeedKmh,
                Condition = defaultF.Condition,
                BanglaCondition = defaultF.BanglaCondition,
                ConditionIcon = defaultF.ConditionIcon,
                ForecastDate = defaultF.ForecastDate,
                FiveDayForecast = defaultF.FiveDayForecast
            });
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

                // If both temperature and humidity conditions match, rule is triggered!
                if (tempMatches && humidityMatches)
                {
                    double riskScore = CalculateRiskPercentage(weather, rule);

                    matchedAlerts.Add(new EvaluatedPestAlert
                    {
                        RuleId = rule.Id,
                        DiseaseName = rule.DiseaseName,
                        BanglaName = rule.BanglaName,
                        PathogenOrPest = rule.PathogenOrPest,
                        Category = rule.Category,
                        TargetCrops = rule.TargetCrops,
                        Severity = rule.Severity,
                        Symptoms = rule.Symptoms,
                        BanglaSymptoms = rule.BanglaSymptoms,
                        TriggerExplanation = $"Triggered by {weather.Temperature:0.#}°C temperature and {weather.Humidity:0.#}% relative humidity in {weather.District}.",
                        BanglaTriggerExplanation = $"{weather.District} জেলায় {weather.Temperature:0.#}° সে. তাপমাত্রা এবং {weather.Humidity:0.#}% আর্দ্রতার কারণে এই রোগ/পোকার ঝুঁকি তৈরি হয়েছে।",
                        ActionableRemedies = rule.ActionableRemedies,
                        BanglaActionableRemedies = rule.BanglaActionableRemedies,
                        PreventiveSpray = rule.PreventiveSpray,
                        OrganicControl = rule.OrganicControl,
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
                AllRulesEncyclopedia = DiseaseRules,
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
