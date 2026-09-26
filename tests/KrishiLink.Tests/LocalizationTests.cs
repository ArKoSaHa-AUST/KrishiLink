using System.Xml.Linq;

namespace KrishiLink.Tests;

public class LocalizationTests
{
    private static string ResourcePath(string culture)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KrishiLink.csproj")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "Resources", $"SharedResource.{culture}.resx");
    }

    private static Dictionary<string, string> Load(string culture) =>
        XDocument.Load(ResourcePath(culture)).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);

    [Fact]
    public void English_and_Bangla_resources_define_exactly_the_same_keys()
    {
        var en = Load("en");
        var bn = Load("bn");

        Assert.Empty(en.Keys.Except(bn.Keys));
        Assert.Empty(bn.Keys.Except(en.Keys));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("bn")]
    public void Keys_are_unique_ignoring_case(string culture)
    {
        // MSBuild compares resource names case-insensitively and silently drops the later duplicate.
        var duplicates = XDocument.Load(ResourcePath(culture)).Root!.Elements("data")
            .Select(d => (string)d.Attribute("name")!)
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        Assert.Empty(duplicates);
    }

    [Fact]
    public void Every_Bangla_value_is_translated()
    {
        var blank = Load("bn").Where(p => string.IsNullOrWhiteSpace(p.Value)).Select(p => p.Key).ToList();
        Assert.Empty(blank);
    }

    [Fact]
    public void Format_placeholders_match_between_languages()
    {
        var bn = Load("bn");
        var mismatched = Load("en")
            .Where(p => Placeholders(p.Value).SetEquals(Placeholders(bn[p.Key])) is false)
            .Select(p => p.Key)
            .ToList();
        Assert.Empty(mismatched);
    }

    [Fact]
    public void Views_do_not_use_undefined_resource_keys()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KrishiLink.csproj")))
            dir = dir.Parent;
        Assert.NotNull(dir);

        var en = Load("en");
        var viewsDir = Path.Combine(dir!.FullName, "Views");
        var viewFiles = Directory.GetFiles(viewsDir, "*.cshtml", SearchOption.AllDirectories);
        var pattern = new System.Text.RegularExpressions.Regex(@"L\[""([^""]+)""\]");

        var missing = new Dictionary<string, List<string>>();
        foreach (var file in viewFiles)
        {
            var text = File.ReadAllText(file);
            foreach (System.Text.RegularExpressions.Match match in pattern.Matches(text))
            {
                var key = match.Groups[1].Value;
                if (!en.ContainsKey(key))
                {
                    if (!missing.ContainsKey(key)) missing[key] = new List<string>();
                    var rel = Path.GetRelativePath(dir.FullName, file);
                    if (!missing[key].Contains(rel)) missing[key].Add(rel);
                }
            }
        }

        var json = System.Text.Json.JsonSerializer.Serialize(missing, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "missing_view_keys.json"), json);
        Assert.Empty(missing);
    }

    private static HashSet<string> Placeholders(string text) =>
        System.Text.RegularExpressions.Regex.Matches(text, @"\{(\d+)(?:[:,][^}]*)?\}").Select(m => m.Groups[1].Value).ToHashSet();
}
