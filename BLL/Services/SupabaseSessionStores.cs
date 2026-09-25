using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.BLL.Services;

public sealed class SupabaseSession
{
    public required string UserId { get; init; }
    public required string SecurityStamp { get; init; }
    public required SupabaseAuthTokens Tokens { get; set; }
    public required DateTimeOffset TokenExpiresAt { get; set; }
    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>Where signed-in sessions live. Memory in Development; the database everywhere else (Authentication:SessionStore).</summary>
public interface ISupabaseSessionStore
{
    /// <summary>Stores the session and returns the new opaque id for the auth cookie.</summary>
    Task<string> AddAsync(SupabaseSession session);

    /// <summary>The unexpired session for this id, or null.</summary>
    Task<SupabaseSession?> GetAsync(string? id);

    Task RemoveAsync(string? id);
    Task RemoveUserAsync(string userId);

    /// <summary>
    /// Runs <paramref name="refresh"/> exclusively when <paramref name="isDue"/> says the tokens need it, then returns the
    /// current session (null once it no longer exists). Supabase refresh tokens are single-use, so concurrent requests —
    /// on any instance — must wait and reuse the new tokens rather than refresh twice.
    /// </summary>
    Task<SupabaseSession?> RefreshIfDueAsync(string id, Func<SupabaseSession, bool> isDue, Func<SupabaseSession, Task<SupabaseAuthTokens>> refresh);

    /// <summary>Deletes expired sessions; returns how many.</summary>
    Task<int> SweepExpiredAsync(CancellationToken ct = default);
}

/// <summary>Development store: process memory, so a restart signs everyone out and it cannot span instances.</summary>
public sealed class InMemorySupabaseSessionStore : ISupabaseSessionStore
{
    private sealed record Entry(SupabaseSession Session, SemaphoreSlim Gate);

    private readonly ConcurrentDictionary<string, Entry> _sessions = new();

    public Task<string> AddAsync(SupabaseSession session)
    {
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _sessions[id] = new Entry(session, new SemaphoreSlim(1, 1));
        return Task.FromResult(id);
    }

    public Task<SupabaseSession?> GetAsync(string? id) => Task.FromResult(Live(id)?.Session);

    public Task RemoveAsync(string? id)
    {
        if (id != null) _sessions.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task RemoveUserAsync(string userId)
    {
        foreach (var item in _sessions)
            if (item.Value.Session.UserId == userId) _sessions.TryRemove(item.Key, out _);
        return Task.CompletedTask;
    }

    public async Task<SupabaseSession?> RefreshIfDueAsync(string id, Func<SupabaseSession, bool> isDue, Func<SupabaseSession, Task<SupabaseAuthTokens>> refresh)
    {
        var entry = Live(id);
        if (entry == null) return null;
        await entry.Gate.WaitAsync();
        try
        {
            if (Live(id) != entry) return null;
            if (isDue(entry.Session))
            {
                entry.Session.Tokens = await refresh(entry.Session);
                entry.Session.TokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(entry.Session.Tokens.ExpiresIn);
            }
            return entry.Session;
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    public Task<int> SweepExpiredAsync(CancellationToken ct = default)
    {
        var removed = 0;
        foreach (var item in _sessions)
            if (item.Value.Session.ExpiresAt <= DateTimeOffset.UtcNow && _sessions.TryRemove(item.Key, out _)) removed++;
        return Task.FromResult(removed);
    }

    private Entry? Live(string? id) =>
        id != null && _sessions.TryGetValue(id, out var entry) && entry.Session.ExpiresAt > DateTimeOffset.UtcNow ? entry : null;
}

/// <summary>
/// Production store: PostgreSQL, shared by every instance and surviving restarts. Only a hash of the session id is
/// stored and the Supabase tokens are encrypted with Data Protection. Each call uses its own short-lived context so it
/// never saves, or joins the transaction of, the request's unit of work.
/// </summary>
public sealed class DatabaseSupabaseSessionStore(DbContextOptions<ApplicationDbContext> dbOptions, IDataProtectionProvider dataProtection)
    : ISupabaseSessionStore
{
    private const string LockSql = $"SELECT * FROM {DatabaseConfiguration.Schema}.\"SupabaseSessions\" WHERE \"Id\" = {{0}} FOR UPDATE";

    private readonly IDataProtector _protector = dataProtection.CreateProtector("KrishiLink.SupabaseSession.v1");

    public async Task<string> AddAsync(SupabaseSession session)
    {
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await using var db = new ApplicationDbContext(dbOptions);
        db.SupabaseSessions.Add(new SupabaseSessionRecord
        {
            Id = Key(id),
            UserId = session.UserId,
            SecurityStamp = session.SecurityStamp,
            ProtectedTokens = Protect(session.Tokens),
            TokenExpiresAt = session.TokenExpiresAt.UtcDateTime,
            ExpiresAt = session.ExpiresAt.UtcDateTime
        });
        await db.SaveChangesAsync();
        return id;
    }

    public async Task<SupabaseSession?> GetAsync(string? id)
    {
        if (id == null) return null;
        await using var db = new ApplicationDbContext(dbOptions);
        var key = Key(id);
        var now = DateTime.UtcNow;
        var record = await db.SupabaseSessions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == key && r.ExpiresAt > now);
        return record == null ? null : ToSession(record);
    }

    public async Task RemoveAsync(string? id)
    {
        if (id == null) return;
        await using var db = new ApplicationDbContext(dbOptions);
        var key = Key(id);
        await db.SupabaseSessions.Where(r => r.Id == key).ExecuteDeleteAsync();
    }

    public async Task RemoveUserAsync(string userId)
    {
        await using var db = new ApplicationDbContext(dbOptions);
        await db.SupabaseSessions.Where(r => r.UserId == userId).ExecuteDeleteAsync();
    }

    public async Task<SupabaseSession?> RefreshIfDueAsync(string id, Func<SupabaseSession, bool> isDue, Func<SupabaseSession, Task<SupabaseAuthTokens>> refresh)
    {
        var current = await GetAsync(id);
        if (current == null || !isDue(current)) return current;

        await using var db = new ApplicationDbContext(dbOptions);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var key = Key(id);
        // Row lock only for the refresh itself; a waiting request re-reads and finds the tokens already refreshed.
        var record = await db.SupabaseSessions.FromSqlRaw(LockSql, key).AsNoTracking().FirstOrDefaultAsync();
        if (record == null || record.ExpiresAt <= DateTime.UtcNow) return null;

        var session = ToSession(record);
        if (isDue(session))
        {
            session.Tokens = await refresh(session);
            session.TokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(session.Tokens.ExpiresIn);
            var tokens = Protect(session.Tokens);
            var tokenExpiresAt = session.TokenExpiresAt.UtcDateTime;
            await db.SupabaseSessions.Where(r => r.Id == key).ExecuteUpdateAsync(s => s
                .SetProperty(r => r.ProtectedTokens, tokens)
                .SetProperty(r => r.TokenExpiresAt, tokenExpiresAt));
        }
        await transaction.CommitAsync();
        return session;
    }

    public async Task<int> SweepExpiredAsync(CancellationToken ct = default)
    {
        await using var db = new ApplicationDbContext(dbOptions);
        var now = DateTime.UtcNow;
        return await db.SupabaseSessions.Where(r => r.ExpiresAt <= now).ExecuteDeleteAsync(ct);
    }

    private static string Key(string id) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)));

    private string Protect(SupabaseAuthTokens tokens) => _protector.Protect(JsonSerializer.Serialize(tokens));

    private SupabaseSession ToSession(SupabaseSessionRecord record) => new()
    {
        UserId = record.UserId,
        SecurityStamp = record.SecurityStamp,
        Tokens = JsonSerializer.Deserialize<SupabaseAuthTokens>(_protector.Unprotect(record.ProtectedTokens))!,
        TokenExpiresAt = new DateTimeOffset(DateTime.SpecifyKind(record.TokenExpiresAt, DateTimeKind.Utc)),
        ExpiresAt = new DateTimeOffset(DateTime.SpecifyKind(record.ExpiresAt, DateTimeKind.Utc))
    };
}
