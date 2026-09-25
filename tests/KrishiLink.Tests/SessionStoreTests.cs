using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Tests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.Tests;

/// <summary>SEC-07: sessions survive restarts, are shared across instances, and refresh exactly once under contention.</summary>
[Collection(PostgresCollection.Name)]
public class SessionStoreTests
{
    private readonly PostgresDatabase _database;
    private readonly IDataProtectionProvider _keys = new EphemeralDataProtectionProvider();

    public SessionStoreTests(PostgresDatabase database) => _database = database;

    public static TheoryData<string> Stores => new() { "memory", "database" };

    private ISupabaseSessionStore Create(string kind) => kind == "memory"
        ? new InMemorySupabaseSessionStore()
        : new DatabaseSupabaseSessionStore(_database.Options, _keys);

    private async Task<string> UserAsync()
    {
        await using var market = new Marketplace(_database);
        return await market.AddUserAsync(AppRoles.Farmer);
    }

    private static SupabaseSession Session(string userId, TimeSpan lifetime, TimeSpan tokenLifetime, string access = "access-1") => new()
    {
        UserId = userId,
        SecurityStamp = "stamp-" + userId,
        Tokens = new SupabaseAuthTokens { AccessToken = access, RefreshToken = "refresh-1", ExpiresIn = 3600 },
        TokenExpiresAt = DateTimeOffset.UtcNow.Add(tokenLifetime),
        ExpiresAt = DateTimeOffset.UtcNow.Add(lifetime)
    };

    [PostgresTheory]
    [MemberData(nameof(Stores))]
    public async Task A_session_round_trips(string kind)
    {
        var store = Create(kind);
        var userId = await UserAsync();
        var original = Session(userId, TimeSpan.FromHours(8), TimeSpan.FromMinutes(30));

        var id = await store.AddAsync(original);
        var loaded = await store.GetAsync(id);

        Assert.NotNull(loaded);
        Assert.Equal(userId, loaded!.UserId);
        Assert.Equal(original.SecurityStamp, loaded.SecurityStamp);
        Assert.Equal("access-1", loaded.Tokens.AccessToken);
        Assert.Equal("refresh-1", loaded.Tokens.RefreshToken);
        Assert.True((loaded.ExpiresAt - original.ExpiresAt).Duration() < TimeSpan.FromMilliseconds(1));
        Assert.Null(await store.GetAsync(id + "0"));
        Assert.Null(await store.GetAsync(null));
    }

    [PostgresTheory]
    [MemberData(nameof(Stores))]
    public async Task Expired_sessions_are_invisible_and_swept(string kind)
    {
        var store = Create(kind);
        var userId = await UserAsync();
        var expired = await store.AddAsync(Session(userId, TimeSpan.FromSeconds(-1), TimeSpan.Zero));
        var live = await store.AddAsync(Session(userId, TimeSpan.FromHours(1), TimeSpan.FromMinutes(30)));

        Assert.Null(await store.GetAsync(expired));
        Assert.True(await store.SweepExpiredAsync() >= 1);
        Assert.NotNull(await store.GetAsync(live));
    }

    [PostgresTheory]
    [MemberData(nameof(Stores))]
    public async Task Sign_out_and_sign_out_everywhere_remove_sessions(string kind)
    {
        var store = Create(kind);
        var userId = await UserAsync();
        var otherUser = await UserAsync();
        var first = await store.AddAsync(Session(userId, TimeSpan.FromHours(1), TimeSpan.FromMinutes(30)));
        var second = await store.AddAsync(Session(userId, TimeSpan.FromHours(1), TimeSpan.FromMinutes(30)));
        var other = await store.AddAsync(Session(otherUser, TimeSpan.FromHours(1), TimeSpan.FromMinutes(30)));

        await store.RemoveAsync(first);
        Assert.Null(await store.GetAsync(first));
        Assert.NotNull(await store.GetAsync(second));

        await store.RemoveUserAsync(userId);
        Assert.Null(await store.GetAsync(second));
        Assert.NotNull(await store.GetAsync(other));
    }

    [PostgresTheory]
    [MemberData(nameof(Stores))]
    public async Task Tokens_that_are_not_due_are_not_refreshed(string kind)
    {
        var store = Create(kind);
        var id = await store.AddAsync(Session(await UserAsync(), TimeSpan.FromHours(1), TimeSpan.FromMinutes(30)));
        var calls = 0;

        var session = await store.RefreshIfDueAsync(id, s => s.TokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1),
            _ => { calls++; return Task.FromResult(new SupabaseAuthTokens()); });

        Assert.Equal(0, calls);
        Assert.Equal("access-1", session!.Tokens.AccessToken);
    }

    [PostgresTheory]
    [MemberData(nameof(Stores))]
    public async Task Concurrent_refreshes_call_Supabase_once_and_all_see_the_new_tokens(string kind)
    {
        var first = Create(kind);
        // A second app instance: same database and key ring, separate store object.
        var second = kind == "memory" ? first : Create(kind);
        var id = await first.AddAsync(Session(await UserAsync(), TimeSpan.FromHours(1), TimeSpan.FromSeconds(-5)));
        var calls = 0;

        async Task<SupabaseAuthTokens> RefreshAsync(SupabaseSession s)
        {
            Interlocked.Increment(ref calls);
            Assert.Equal("refresh-1", s.Tokens.RefreshToken);
            await Task.Delay(300);
            return new SupabaseAuthTokens { AccessToken = "access-2", RefreshToken = "refresh-2", ExpiresIn = 3600 };
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => (i % 2 == 0 ? first : second)
            .RefreshIfDueAsync(id, s => s.TokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1), RefreshAsync)));

        Assert.Equal(1, calls);
        Assert.All(results, s => Assert.Equal("access-2", s!.Tokens.AccessToken));
        Assert.Equal("refresh-2", (await second.GetAsync(id))!.Tokens.RefreshToken);
    }

    [PostgresTheory]
    [MemberData(nameof(Stores))]
    public async Task Refreshing_a_removed_session_returns_nothing(string kind)
    {
        var store = Create(kind);
        var id = await store.AddAsync(Session(await UserAsync(), TimeSpan.FromHours(1), TimeSpan.FromSeconds(-5)));
        await store.RemoveAsync(id);

        Assert.Null(await store.RefreshIfDueAsync(id, _ => true, _ => Task.FromResult(new SupabaseAuthTokens())));
    }

    [PostgresFact]
    public async Task The_database_holds_neither_the_session_id_nor_readable_tokens()
    {
        var store = Create("database");
        var userId = await UserAsync();
        var id = await store.AddAsync(Session(userId, TimeSpan.FromHours(1), TimeSpan.FromMinutes(30), access: "eyJ-secret-access-token"));

        await using var db = _database.CreateContext();
        Assert.False(await db.SupabaseSessions.AnyAsync(r => r.Id == id));
        var row = await db.SupabaseSessions.SingleAsync(r => r.UserId == userId);
        Assert.DoesNotContain("eyJ-secret-access-token", row.ProtectedTokens);
        Assert.DoesNotContain("refresh-1", row.ProtectedTokens);
    }

    [PostgresFact]
    public async Task Deleting_a_user_deletes_their_sessions()
    {
        var store = Create("database");
        var userId = await UserAsync();
        var id = await store.AddAsync(Session(userId, TimeSpan.FromHours(1), TimeSpan.FromMinutes(30)));

        await using (var db = _database.CreateContext())
            await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync();

        Assert.Null(await store.GetAsync(id));
    }
}
