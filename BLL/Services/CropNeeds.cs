using KrishiLink.Models.Entities;

namespace KrishiLink.BLL.Services
{
    public enum CropNeedStage
    {
        LandPreparation,
        Harvest,
        PostHarvestStorage
    }

    /// <summary>One thing the farmer will likely rent for a crop, with the window it is needed in.</summary>
    public sealed record CropNeed(CropNeedStage Stage, string Category, bool IsStorage, DateTime From, DateTime To)
    {
        /// <summary>A pre-filtered, date-scoped marketplace search for this need.</summary>
        public string SearchUrl(string? district) => IsStorage
            ? AppLinks.GodownSearch(district, Category, From, To)
            : AppLinks.EquipmentSearch(district, Category, From, To);
    }

    /// <summary>Model for <c>Views/Shared/_CropNeeds.cshtml</c>.</summary>
    public sealed record CropNeedsStrip(IReadOnlyList<CropNeed> Needs, string? District);

    /// <summary>
    /// "What you'll need" for a crop, derived from its calendar stages: land preparation before sowing, harvest machinery
    /// in the harvest month, and storage after harvest. A small reviewed map, not a model: crops with no typical need at a
    /// stage (hand-picked vegetables, perishable fruit) simply get nothing for it.
    /// </summary>
    public static class CropNeeds
    {
        private const int EquipmentWindowDays = 7;
        private const int StorageWindowDays = 60;

        // Machinery at harvest, by calendar key or category. Absent = harvested by hand.
        private static readonly Dictionary<string, string> HarvestEquipmentByKey = new(StringComparer.Ordinal)
        {
            ["boro-rice"] = "Combine Harvester",
            ["t-aman-rice"] = "Combine Harvester",
            ["aus-rice"] = "Combine Harvester",
            ["wheat"] = "Combine Harvester",
            ["hybrid-maize"] = "Thresher"
        };

        private static readonly Dictionary<string, string> HarvestEquipmentByCategory = new(StringComparer.Ordinal)
        {
            ["Pulses"] = "Thresher",
            ["Oilseeds"] = "Thresher"
        };

        // Storage after harvest, by calendar key or category. Absent = sold fresh.
        private static readonly Dictionary<string, string> StorageByKey = new(StringComparer.Ordinal)
        {
            ["potato"] = "Cold Storage",
            ["winter-onion"] = "Dry Godown",
            ["summer-onion"] = "Dry Godown",
            ["garlic"] = "Dry Godown",
            ["chili"] = "Dry Godown",
            ["jute"] = "Dry Godown"
        };

        private static readonly Dictionary<string, string> StorageByCategory = new(StringComparer.Ordinal)
        {
            ["Cereals"] = "Grain Warehouse",
            ["Pulses"] = "Dry Godown",
            ["Oilseeds"] = "Dry Godown"
        };

        public static IReadOnlyList<CropNeed> For(CropCalendarEntry crop, DateTime today)
        {
            var needs = new List<CropNeed>();
            if (crop.SowingMonths.Count > 0)
            {
                var start = NextStart(crop.SowingMonths[0], today);
                needs.Add(new CropNeed(CropNeedStage.LandPreparation, "Power Tiller", false, start, start.AddDays(EquipmentWindowDays - 1)));
            }

            if (crop.HarvestingMonths.Count > 0)
            {
                var harvest = NextStart(crop.HarvestingMonths[0], today);
                var machine = HarvestEquipmentByKey.GetValueOrDefault(crop.Key) ?? HarvestEquipmentByCategory.GetValueOrDefault(crop.Category);
                if (machine is not null)
                    needs.Add(new CropNeed(CropNeedStage.Harvest, machine, false, harvest, harvest.AddDays(EquipmentWindowDays - 1)));

                var storage = StorageByKey.GetValueOrDefault(crop.Key) ?? StorageByCategory.GetValueOrDefault(crop.Category);
                if (storage is not null)
                {
                    var from = harvest.AddDays(EquipmentWindowDays);
                    needs.Add(new CropNeed(CropNeedStage.PostHarvestStorage, storage, true, from, from.AddDays(StorageWindowDays - 1)));
                }
            }
            return needs;
        }

        /// <summary>The next time <paramref name="month"/> comes round: today if we are in it, otherwise its first day.</summary>
        public static DateTime NextStart(int month, DateTime today)
        {
            if (month == today.Month) return today.Date;
            var year = month > today.Month ? today.Year : today.Year + 1;
            return new DateTime(year, month, 1);
        }
    }
}
