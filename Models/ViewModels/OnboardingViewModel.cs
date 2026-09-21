using System.ComponentModel.DataAnnotations;
using KrishiLink.Models.Entities;

using KrishiLink.BLL.Helpers;

namespace KrishiLink.Models.ViewModels
{
    /// <summary>
    /// Post-registration setup. Only the profile fields the user has not filled in yet are asked for;
    /// the role was already chosen at registration and is shown read-only.
    /// </summary>
    public class OnboardingViewModel
    {
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = AppRoles.Farmer;

        public bool AskDistrict { get; set; }
        public bool AskSpecialization { get; set; }

        [Display(Name = "District")]
        public string? District { get; set; }

        public string? Specialization { get; set; }

        public IReadOnlyList<string> SpecializationOptions => OnboardingOptions.SpecializationsFor(Role);

        public string RoleLabel => Role switch
        {
            AppRoles.EquipmentOwner => "Equipment Owner",
            AppRoles.GodownOwner => "Godown Owner",
            _ => "Farmer"
        };

        public string SpecializationLabel => Role switch
        {
            AppRoles.EquipmentOwner => "What type of equipment will you list first?",
            AppRoles.GodownOwner => "What type of storage do you offer?",
            _ => "What is your main crop?"
        };
    }

    /// <summary>Fixed choice lists used by onboarding (and shown on the profile).</summary>
    public static class OnboardingOptions
    {
        /// <summary>The 64 districts, sourced from BangladeshGeo so there is one geography table.</summary>
        public static IReadOnlyList<string> Districts => BangladeshGeo.AllDistricts;

        public static readonly IReadOnlyList<string> Crops = new[]
        {
            "Rice (Boro)", "Rice (Aman)", "Rice (Aus)", "Wheat", "Maize", "Potato", "Jute", "Vegetables", "Pulses", "Oilseeds", "Fruits", "Other"
        };

        // Kept in sync with the category/type <select> lists on the listing forms
        public static readonly IReadOnlyList<string> EquipmentCategories = new[]
        {
            "Tractor", "Power Tiller", "Combine Harvester", "Seed Drill / Seeder", "Power Sprayer", "Irrigation Pump", "Thresher", "Other"
        };

        public static readonly IReadOnlyList<string> StorageTypes = new[]
        {
            "Cold Storage", "Grain Warehouse", "Multi-Chamber Cold Storage", "Seed Vault", "Silo Facility", "Dry Godown"
        };

        public static IReadOnlyList<string> SpecializationsFor(string role) => role switch
        {
            AppRoles.EquipmentOwner => EquipmentCategories,
            AppRoles.GodownOwner => StorageTypes,
            _ => Crops
        };

        // Pre-2018 spellings still in common use
        private static readonly Dictionary<string, string> DistrictAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Bogra"] = "Bogura",
            ["Comilla"] = "Cumilla",
            ["Jessore"] = "Jashore",
            ["Chittagong"] = "Chattogram",
            ["Barisal"] = "Barishal",
            ["Nawabganj"] = "Chapainawabganj",
            ["Maulvibazar"] = "Moulvibazar"
        };

        /// <summary>Best-effort district guess from a free-text location like "Shibganj, Bogra".</summary>
        public static string? GuessDistrict(string? location)
        {
            if (string.IsNullOrWhiteSpace(location)) return null;
            var tail = location.Split(',').Last().Trim();
            if (DistrictAliases.TryGetValue(tail, out var modern)) tail = modern;
            return Districts.FirstOrDefault(d => d.Equals(tail, StringComparison.OrdinalIgnoreCase));
        }
    }
}
