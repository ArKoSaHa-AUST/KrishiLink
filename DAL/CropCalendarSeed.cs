using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using KrishiLink.Models.Entities;

namespace KrishiLink.DAL;

/// <summary>
/// The crop calendar lives in <c>App_Data/seed/crop-calendar.json</c> so an agronomist can review and correct it without
/// touching C# or needing an admin screen. The file is validated on load and a bad file fails startup loudly rather than
/// seeding half-broken advice.
/// </summary>
public static class CropCalendarSeed
{
    public const string RelativePath = "App_Data/seed/crop-calendar.json";
    public const int SchemaVersion = 1;

    public static readonly IReadOnlySet<string> Seasons = new HashSet<string>(StringComparer.Ordinal) { "Rabi", "Kharif-1", "Kharif-2", "YearRound" };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly Regex KeyPattern = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant);

    private sealed class SeedFile
    {
        public int SchemaVersion { get; set; }
        public List<CropCalendarEntry> Crops { get; set; } = new();
    }

    public static string PathFor(string contentRoot) => Path.Combine(contentRoot, RelativePath);

    /// <summary>Reads and validates the seed. Throws <see cref="InvalidOperationException"/> naming every problem found.</summary>
    public static IReadOnlyList<CropCalendarEntry> Load(string contentRoot)
    {
        var path = PathFor(contentRoot);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Crop calendar seed not found at {path}. It must be published with the app.");

        SeedFile? file;
        try
        {
            file = JsonSerializer.Deserialize<SeedFile>(File.ReadAllText(path), Json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{RelativePath} is not valid JSON: {ex.Message}", ex);
        }

        var problems = Validate(file);
        if (problems.Count > 0)
            throw new InvalidOperationException($"{RelativePath} is invalid:{Environment.NewLine}- " + string.Join(Environment.NewLine + "- ", problems));
        return file!.Crops;
    }

    internal static List<string> Validate(object? parsed)
    {
        var problems = new List<string>();
        if (parsed is not SeedFile file)
        {
            problems.Add("The file is empty.");
            return problems;
        }
        if (file.SchemaVersion != SchemaVersion)
            problems.Add($"schemaVersion is {file.SchemaVersion}; this build understands {SchemaVersion}.");
        if (file.Crops.Count == 0)
            problems.Add("crops is empty.");

        foreach (var dup in file.Crops.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            problems.Add($"Duplicate name '{dup.Key}'.");
        foreach (var dup in file.Crops.GroupBy(c => c.Key, StringComparer.Ordinal).Where(g => g.Count() > 1))
            problems.Add($"Duplicate key '{dup.Key}'.");

        foreach (var c in file.Crops)
        {
            var label = string.IsNullOrWhiteSpace(c.Key) ? c.Name : c.Key;
            void Require(string? value, string field)
            {
                if (string.IsNullOrWhiteSpace(value)) problems.Add($"{label}: {field} is required.");
            }

            Require(c.Name, "name");
            Require(c.BanglaName, "banglaName");
            Require(c.Category, "category");
            Require(c.SoilTypes, "soilTypes");
            Require(c.WaterRequirement, "waterRequirement");
            Require(c.KeyTips, "keyTips");
            Require(c.Source, "source");
            if (!KeyPattern.IsMatch(c.Key ?? string.Empty)) problems.Add($"{label}: key must be a lowercase slug such as 'boro-rice'.");
            if (!Seasons.Contains(c.Season)) problems.Add($"{label}: season '{c.Season}' must be one of {string.Join(", ", Seasons)}.");
            if (c.SowingMonths.Count == 0) problems.Add($"{label}: sowingMonths is empty.");
            if (c.HarvestingMonths.Count == 0) problems.Add($"{label}: harvestingMonths is empty.");
            if (c.SowingMonths.Concat(c.GrowingMonths).Concat(c.HarvestingMonths).Any(m => m is < 1 or > 12))
                problems.Add($"{label}: months must be 1-12.");
            if (!Enum.IsDefined(c.WaterNeed)) problems.Add($"{label}: waterNeed must be Low, Medium or High.");
            if (c.MinPh.HasValue != c.MaxPh.HasValue || (c.MinPh is { } lo && c.MaxPh is { } hi && !(lo >= 3 && hi <= 10 && lo < hi)))
                problems.Add($"{label}: minPh/maxPh must both be set, within 3-10, with min < max.");
            if (c.TypicalYieldPerAcreMin.HasValue != c.TypicalYieldPerAcreMax.HasValue
                || (c.TypicalYieldPerAcreMin is { } yl && c.TypicalYieldPerAcreMax is { } yh && !(yl > 0 && yl <= yh)))
                problems.Add($"{label}: typicalYieldPerAcreMin/Max must both be set with 0 < min <= max.");
        }
        return problems;
    }

    /// <summary>Copies seed content onto a stored row, keeping its database identity.</summary>
    public static void CopyInto(CropCalendarEntry source, CropCalendarEntry target)
    {
        target.Key = source.Key;
        target.Name = source.Name;
        target.BanglaName = source.BanglaName;
        target.ScientificName = source.ScientificName;
        target.Category = source.Category;
        target.Season = source.Season;
        target.ProfileCropName = source.ProfileCropName;
        target.SowingMonths = source.SowingMonths.ToList();
        target.GrowingMonths = source.GrowingMonths.ToList();
        target.HarvestingMonths = source.HarvestingMonths.ToList();
        target.DurationDays = source.DurationDays;
        target.OptimalTemperature = source.OptimalTemperature;
        target.SoilTypes = source.SoilTypes;
        target.SoilTypesBn = source.SoilTypesBn;
        target.WaterRequirement = source.WaterRequirement;
        target.WaterRequirementBn = source.WaterRequirementBn;
        target.WaterNeed = source.WaterNeed;
        target.MinPh = source.MinPh;
        target.MaxPh = source.MaxPh;
        target.TypicalYieldPerAcreMin = source.TypicalYieldPerAcreMin;
        target.TypicalYieldPerAcreMax = source.TypicalYieldPerAcreMax;
        target.PopularVarieties = source.PopularVarieties;
        target.MajorDistricts = source.MajorDistricts;
        target.Division = source.Division;
        target.KeyTips = source.KeyTips;
        target.KeyTipsBn = source.KeyTipsBn;
        target.Source = source.Source;
        target.IconClass = source.IconClass;
        target.BadgeColor = source.BadgeColor;
    }
}
