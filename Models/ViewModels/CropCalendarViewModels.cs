namespace KrishiLink.Models.ViewModels
{
    /// <summary>
    /// ViewModel for the interactive Crop Calendar page under Advisory.
    /// </summary>
    public class CropCalendarIndexViewModel
    {
        public List<CropCalendarItemViewModel> Crops { get; set; } = new();

        // Active Filter Parameters
        public string? SearchQuery { get; set; }
        public string? SelectedCategory { get; set; }
        public string? SelectedSeason { get; set; }
        public string? SelectedDivision { get; set; }
        public int SelectedMonth { get; set; }
        public string? SelectedStage { get; set; } = "All"; // All, Sowing, Growing, Harvesting

        // Current Calendar Context
        public int CurrentMonth { get; set; } = DateTime.Now.Month;
        public string CurrentMonthName { get; set; } = string.Empty;
        public string CurrentBanglaMonth { get; set; } = string.Empty;
        public string CurrentSeason { get; set; } = string.Empty;

        // Metric Counters
        public int TotalCropsCount { get; set; }
        public int SowingNowCount { get; set; }
        public int HarvestingNowCount { get; set; }
        public int GrowingNowCount { get; set; }

        // Dropdown & Filter Options
        public List<SelectOptionItem> Categories { get; set; } = new();
        public List<SelectOptionItem> Seasons { get; set; } = new();
        public List<SelectOptionItem> Divisions { get; set; } = new();
        public List<MonthTimelineHeader> Months { get; set; } = new();
    }

    public class CropCalendarItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string BanglaName { get; set; } = string.Empty;
        public string ScientificName { get; set; } = string.Empty;
        public string Category { get; set; } = "Cereals";
        public string CategoryDisplay { get; set; } = string.Empty;
        public string Season { get; set; } = "Rabi";
        public string SeasonDisplay { get; set; } = string.Empty;
        public List<int> SowingMonths { get; set; } = new();
        public List<int> GrowingMonths { get; set; } = new();
        public List<int> HarvestingMonths { get; set; } = new();
        public string DurationDays { get; set; } = string.Empty;
        public string OptimalTemperature { get; set; } = string.Empty;
        public string SoilTypes { get; set; } = string.Empty;
        public string WaterRequirement { get; set; } = string.Empty;
        public string PopularVarieties { get; set; } = string.Empty;
        public string MajorDistricts { get; set; } = string.Empty;
        public string Division { get; set; } = "All";
        public string KeyTips { get; set; } = string.Empty;
        public string IconClass { get; set; } = "bi-flower2";
        public string BadgeColor { get; set; } = "success";

        public bool IsSowingInMonth(int month) => SowingMonths.Contains(month);
        public bool IsGrowingInMonth(int month) => GrowingMonths.Contains(month);
        public bool IsHarvestingInMonth(int month) => HarvestingMonths.Contains(month);

        public string GetPhaseForMonth(int month)
        {
            if (IsSowingInMonth(month)) return "sowing";
            if (IsHarvestingInMonth(month)) return "harvesting";
            if (IsGrowingInMonth(month)) return "growing";
            return "none";
        }

        public string GetCurrentMonthStatus(int currentMonth)
        {
            if (IsSowingInMonth(currentMonth)) return "Sowing Now";
            if (IsHarvestingInMonth(currentMonth)) return "Harvesting Now";
            if (IsGrowingInMonth(currentMonth)) return "In Field (Growing)";
            return "Off Season";
        }

        public string GetCurrentMonthBanglaStatus(int currentMonth)
        {
            if (IsSowingInMonth(currentMonth)) return "রোপণ/বপনের সময়";
            if (IsHarvestingInMonth(currentMonth)) return "ফসল তোলার সময়";
            if (IsGrowingInMonth(currentMonth)) return "মাঠে বর্ধনশীল";
            return "মৌসুম বহির্ভূত";
        }
    }

    public class MonthTimelineHeader
    {
        public int MonthNumber { get; set; }
        public string EnglishShort { get; set; } = string.Empty;
        public string EnglishFull { get; set; } = string.Empty;
        public string BanglaName { get; set; } = string.Empty;
        public string BanglaMonthApprox { get; set; } = string.Empty;
        public bool IsCurrent { get; set; }
    }

    public class SelectOptionItem
    {
        public string Value { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string BanglaText { get; set; } = string.Empty;

        public SelectOptionItem() { }

        public SelectOptionItem(string value, string text, string banglaText)
        {
            Value = value;
            Text = text;
            BanglaText = banglaText;
        }
    }
}
