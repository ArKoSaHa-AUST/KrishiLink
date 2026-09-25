using System.Text.Json;

namespace KrishiLink.DAL;

/// <summary>
/// Reference lists that used to be C# literals (QLT-04): Bangladesh's divisions and districts (<c>districts.json</c>) and the
/// marketplace vocabulary — crops, equipment categories, storage types (<c>categories.json</c>). Reviewed as data with no
/// admin screen, the same way as the crop calendar and pest rules; a malformed file stops startup with a clear message.
/// </summary>
public static class ReferenceDataSeed
{
    public const string DistrictsPath = "App_Data/seed/districts.json";
    public const string CategoriesPath = "App_Data/seed/categories.json";
    public const int SchemaVersion = 1;
    public const int DivisionCount = 8;
    public const int DistrictCount = 64;

    public sealed record District(string Name, string Division, IReadOnlyList<string> Aliases, double Lat, double Lng);

    public sealed record Geography(IReadOnlyList<string> Divisions, IReadOnlyList<District> Districts);

    public sealed record Categories(IReadOnlyList<string> Crops, IReadOnlyList<string> EquipmentCategories, IReadOnlyList<string> StorageTypes);

    private sealed class DistrictsFile
    {
        public int SchemaVersion { get; set; }
        public List<string> Divisions { get; set; } = new();
        public List<DistrictRow> Districts { get; set; } = new();
    }

    private sealed class DistrictRow
    {
        public string Name { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public List<string> Aliases { get; set; } = new();
        public double? Lat { get; set; }
        public double? Lng { get; set; }
    }

    private sealed class CategoriesFile
    {
        public int SchemaVersion { get; set; }
        public List<string> Crops { get; set; } = new();
        public List<string> EquipmentCategories { get; set; } = new();
        public List<string> StorageTypes { get; set; } = new();
    }

    private static readonly Lazy<Geography> GeographyLazy = new(() => LoadGeography(AppContext.BaseDirectory));
    private static readonly Lazy<Categories> CategoriesLazy = new(() => LoadCategories(AppContext.BaseDirectory));

    /// <summary>The published copy next to the app; the static helpers (BangladeshGeo, OnboardingOptions) read this.</summary>
    public static Geography CurrentGeography => GeographyLazy.Value;
    public static Categories CurrentCategories => CategoriesLazy.Value;

    public static Geography LoadGeography(string contentRoot)
    {
        var file = Read<DistrictsFile>(contentRoot, DistrictsPath);
        var problems = new List<string>();
        if (file.SchemaVersion != SchemaVersion) problems.Add($"schemaVersion is {file.SchemaVersion}; this build understands {SchemaVersion}.");
        if (file.Divisions.Count != DivisionCount || file.Divisions.Distinct(StringComparer.OrdinalIgnoreCase).Count() != DivisionCount)
            problems.Add($"divisions must list the {DivisionCount} divisions once each.");
        if (file.Districts.Count != DistrictCount) problems.Add($"districts has {file.Districts.Count} entries; Bangladesh has {DistrictCount}.");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in file.Districts)
        {
            var label = string.IsNullOrWhiteSpace(d.Name) ? "(unnamed district)" : d.Name;
            if (string.IsNullOrWhiteSpace(d.Name)) problems.Add("A district has no name.");
            if (!file.Divisions.Contains(d.Division, StringComparer.OrdinalIgnoreCase)) problems.Add($"{label}: division '{d.Division}' is not in divisions.");
            foreach (var name in d.Aliases.Prepend(d.Name).Where(n => !string.IsNullOrWhiteSpace(n)))
                if (!seen.Add(name.Trim())) problems.Add($"{label}: '{name}' is used by more than one district.");
            if (d.Lat is not (>= 20.0 and <= 27.0) || d.Lng is not (>= 87.5 and <= 93.0)) problems.Add($"{label}: lat/lng must be inside Bangladesh.");
        }
        Fail(DistrictsPath, problems);

        return new Geography(
            file.Divisions.Select(d => d.Trim()).ToList(),
            file.Districts.Select(d => new District(d.Name.Trim(), d.Division.Trim(),
                d.Aliases.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList(), d.Lat!.Value, d.Lng!.Value)).ToList());
    }

    public static Categories LoadCategories(string contentRoot)
    {
        var file = Read<CategoriesFile>(contentRoot, CategoriesPath);
        var problems = new List<string>();
        if (file.SchemaVersion != SchemaVersion) problems.Add($"schemaVersion is {file.SchemaVersion}; this build understands {SchemaVersion}.");
        foreach (var (name, list) in new[] { ("crops", file.Crops), ("equipmentCategories", file.EquipmentCategories), ("storageTypes", file.StorageTypes) })
        {
            if (list.Count == 0) problems.Add($"{name} is empty.");
            if (list.Any(string.IsNullOrWhiteSpace)) problems.Add($"{name} has an empty entry.");
            if (list.Distinct(StringComparer.OrdinalIgnoreCase).Count() != list.Count) problems.Add($"{name} lists an entry twice.");
        }
        Fail(CategoriesPath, problems);
        return new Categories(file.Crops.Select(c => c.Trim()).ToList(), file.EquipmentCategories.Select(c => c.Trim()).ToList(), file.StorageTypes.Select(c => c.Trim()).ToList());
    }

    private static T Read<T>(string contentRoot, string relativePath) where T : class
    {
        var path = Path.Combine(contentRoot, relativePath);
        if (!File.Exists(path)) throw new InvalidOperationException($"Reference data not found at {path}. It must be published with the app.");
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new InvalidOperationException($"{relativePath} is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{relativePath} is not valid JSON: {ex.Message}", ex);
        }
    }

    private static void Fail(string relativePath, List<string> problems)
    {
        if (problems.Count > 0)
            throw new InvalidOperationException($"{relativePath} is invalid:{Environment.NewLine}- " + string.Join(Environment.NewLine + "- ", problems));
    }
}
