using System.Collections.Concurrent;
using System.Text.Json;
using KrishiLink.Models.Entities;

namespace KrishiLink.DAL;

/// <summary>
/// The pest and disease rules live in <c>App_Data/seed/pest-rules.json</c> so thresholds and remedies can be reviewed and
/// tuned by an agronomist without C# changes or an admin screen. Validated on load; a bad file fails startup loudly.
/// </summary>
public static class PestRuleSeed
{
    public const string RelativePath = "App_Data/seed/pest-rules.json";
    public const int SchemaVersion = 1;

    public static readonly IReadOnlySet<string> Severities = new HashSet<string>(StringComparer.Ordinal) { "Critical", "High", "Moderate", "Advisory" };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly ConcurrentDictionary<string, IReadOnlyList<PestDiseaseRule>> Cache = new(StringComparer.Ordinal);

    private sealed class SeedFile
    {
        public int SchemaVersion { get; set; }
        public List<PestDiseaseRule> Rules { get; set; } = new();
    }

    public static string PathFor(string contentRoot) => Path.Combine(contentRoot, RelativePath);

    /// <summary>Loaded once per content root; the rules only change with a deployment.</summary>
    public static IReadOnlyList<PestDiseaseRule> Cached(string contentRoot) => Cache.GetOrAdd(contentRoot, Load);

    public static IReadOnlyList<PestDiseaseRule> Load(string contentRoot)
    {
        var path = PathFor(contentRoot);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Pest rule seed not found at {path}. It must be published with the app.");

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
        return file!.Rules;
    }

    private static List<string> Validate(SeedFile? file)
    {
        var problems = new List<string>();
        if (file is null)
        {
            problems.Add("The file is empty.");
            return problems;
        }
        if (file.SchemaVersion != SchemaVersion)
            problems.Add($"schemaVersion is {file.SchemaVersion}; this build understands {SchemaVersion}.");
        if (file.Rules.Count == 0)
            problems.Add("rules is empty.");
        foreach (var dup in file.Rules.GroupBy(r => r.Id).Where(g => g.Count() > 1))
            problems.Add($"Duplicate id {dup.Key}.");

        foreach (var r in file.Rules)
        {
            var label = $"rule {r.Id} ({r.DiseaseName})";
            void Require(string? value, string field)
            {
                if (string.IsNullOrWhiteSpace(value)) problems.Add($"{label}: {field} is required.");
            }

            if (r.Id <= 0) problems.Add($"{label}: id must be positive.");
            Require(r.DiseaseName, "diseaseName");
            Require(r.BanglaName, "banglaName");
            Require(r.Symptoms, "symptoms");
            Require(r.BanglaSymptoms, "banglaSymptoms");
            Require(r.TriggerReason, "triggerReason");
            Require(r.BanglaTriggerReason, "banglaTriggerReason");
            Require(r.Source, "source");
            if (r.TargetCrops.Count == 0) problems.Add($"{label}: targetCrops is empty.");
            if (r.ActionableRemedies.Count == 0) problems.Add($"{label}: actionableRemedies is empty.");
            if (!Severities.Contains(r.Severity)) problems.Add($"{label}: severity '{r.Severity}' must be one of {string.Join(", ", Severities)}.");
            if (!(r.MinTemp < r.MaxTemp) || r.MinTemp < -10 || r.MaxTemp > 60) problems.Add($"{label}: minTemp must be below maxTemp, within -10..60 °C.");
            if (r.MinHumidity is < 0 or > 100 || r.MaxHumidity is < 0 or > 100 || (r.MaxHumidity is { } max && max < r.MinHumidity))
                problems.Add($"{label}: humidity thresholds must be 0-100 with min <= max.");
        }
        return problems;
    }
}
