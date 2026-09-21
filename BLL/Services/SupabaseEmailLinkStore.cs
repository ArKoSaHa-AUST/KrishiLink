using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace KrishiLink.BLL.Services;

public sealed record SupabaseEmailLink(string TokenHash, string Type);

/// <summary>Only an opaque lookup key reaches the browser; email proof remains on the server.</summary>
public sealed class SupabaseEmailLinkStore(IMemoryCache cache)
{
    private readonly object _gate = new();
    private static string Key(string id) => $"supabase-email-link:{id}";

    public string Add(string tokenHash, string type)
    {
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        cache.Set(Key(id), new SupabaseEmailLink(tokenHash, type), TimeSpan.FromMinutes(15));
        return id;
    }

    public SupabaseEmailLink? Get(string? id) =>
        id == null ? null : cache.Get<SupabaseEmailLink>(Key(id));

    public SupabaseEmailLink? Take(string? id)
    {
        if (id == null) return null;
        lock (_gate)
        {
            var value = Get(id);
            cache.Remove(Key(id));
            return value;
        }
    }
}
