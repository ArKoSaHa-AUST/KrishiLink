using System.Collections;

namespace KrishiLink.DAL;

public static class EnvironmentConfiguration
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SUPABASE_URL"] = "Supabase:Url",
        ["SUPABASE_PUBLISHABLE_KEY"] = "Supabase:PublishableKey",
        ["SUPABASE_SECRET_KEY"] = "Supabase:SecretKey",
        ["SUPABASE_PUBLIC_BUCKET"] = "Supabase:PublicBucket",
        ["SUPABASE_PRIVATE_BUCKET"] = "Supabase:PrivateBucket",
        ["SUPABASE_ADMIN_EMAIL"] = "Supabase:AdminEmail",
        ["DATABASE_URL"] = "ConnectionStrings:DefaultConnection",
        ["DIRECT_URL"] = "ConnectionStrings:MigrationConnection"
    };

    public static void AddLocalEnvironmentFiles(
        IConfigurationBuilder builder, string contentRoot, string[] args)
    {
        foreach (var name in new[] { ".env", ".env.local" })
        {
            var path = Path.Combine(contentRoot, name);
            if (File.Exists(path))
                builder.AddInMemoryCollection(ReadFile(path));
        }

        // Deployment environment and command-line settings override local files.
        builder.AddEnvironmentVariables();
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            environment[((string)entry.Key).Replace("__", ":")] = entry.Value?.ToString();
        var environmentAliases = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (source, target) in Aliases)
            if (environment.TryGetValue(source, out var value) && !environment.ContainsKey(target))
                environmentAliases[target] = value;
        builder.AddInMemoryCollection(environmentAliases);
        builder.AddCommandLine(args, Aliases.ToDictionary(pair => "--" + pair.Key, pair => pair.Value));
    }

    private static Dictionary<string, string?> ReadFile(string path)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var lineNumber = 0;
        foreach (var source in File.ReadLines(path))
        {
            lineNumber++;
            var line = source.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            if (line.StartsWith("export ", StringComparison.Ordinal))
                line = line[7..].TrimStart();

            var separator = line.IndexOf('=');
            if (separator <= 0)
                throw InvalidLine(path, lineNumber);
            var key = line[..separator].Trim();
            if (key.Length == 0 || !(char.IsAsciiLetter(key[0]) || key[0] == '_') ||
                key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
                throw InvalidLine(path, lineNumber);

            var value = line[(separator + 1)..].Trim();
            if (value.StartsWith('"') || value.StartsWith('\''))
            {
                if (value.Length < 2 || value[^1] != value[0])
                    throw InvalidLine(path, lineNumber);
                value = value[1..^1];
            }
            else
            {
                var comment = value.IndexOf(" #", StringComparison.Ordinal);
                if (comment >= 0)
                    value = value[..comment].TrimEnd();
            }
            values[key.Replace("__", ":")] = value;
        }
        foreach (var (source, target) in Aliases)
            if (values.TryGetValue(source, out var value) && !values.ContainsKey(target))
                values[target] = value;
        return values;
    }

    private static InvalidOperationException InvalidLine(string path, int line) =>
        new($"Invalid environment setting at {Path.GetFileName(path)}:{line}. Expected KEY=value.");
}
