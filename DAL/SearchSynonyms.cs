using System.Collections.Concurrent;
using System.Text.Json;

namespace KrishiLink.DAL;

/// <summary>
/// Bangla ↔ English marketplace vocabulary from <c>App_Data/seed/search-synonyms.json</c> (DIS-01): a search for "ধান" also
/// finds listings titled "paddy" or "rice". Reviewed as data, no admin screen; a bad file fails startup.
/// </summary>
public static class SearchSynonyms
{
    public const string RelativePath = "App_Data/seed/search-synonyms.json";
    public const int SchemaVersion = 1;

    private static readonly ConcurrentDictionary<string, IReadOnlyList<IReadOnlyList<string>>> Cache = new(StringComparer.Ordinal);

    private sealed class SeedFile
    {
        public int SchemaVersion { get; set; }
        public List<List<string>> Groups { get; set; } = new();
    }

    public static IReadOnlyList<IReadOnlyList<string>> Cached(string contentRoot) => Cache.GetOrAdd(contentRoot, Load);

    public static IReadOnlyList<IReadOnlyList<string>> Load(string contentRoot)
    {
        var path = Path.Combine(contentRoot, RelativePath);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Search synonyms not found at {path}. They must be published with the app.");

        SeedFile? file;
        try
        {
            file = JsonSerializer.Deserialize<SeedFile>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{RelativePath} is not valid JSON: {ex.Message}", ex);
        }

        var problems = new List<string>();
        if (file is null) problems.Add("The file is empty.");
        else
        {
            if (file.SchemaVersion != SchemaVersion) problems.Add($"schemaVersion is {file.SchemaVersion}; this build understands {SchemaVersion}.");
            if (file.Groups.Count == 0) problems.Add("groups is empty.");
            for (var i = 0; i < file.Groups.Count; i++)
            {
                if (file.Groups[i].Count < 2) problems.Add($"group {i + 1} needs at least two terms.");
                if (file.Groups[i].Any(string.IsNullOrWhiteSpace)) problems.Add($"group {i + 1} has an empty term.");
            }
        }
        if (problems.Count > 0)
            throw new InvalidOperationException($"{RelativePath} is invalid:{Environment.NewLine}- " + string.Join(Environment.NewLine + "- ", problems));

        return file!.Groups
            .Select(g => (IReadOnlyList<string>)g.Select(t => t.Trim().ToLowerInvariant()).Distinct().ToList())
            .ToList();
    }
}
