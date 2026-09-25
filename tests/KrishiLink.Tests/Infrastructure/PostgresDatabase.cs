using KrishiLink.DAL;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KrishiLink.Tests.Infrastructure;

/// <summary>
/// A disposable PostgreSQL database for one test run: created empty, migrated with the real EF migrations,
/// dropped afterwards. The InMemory provider is deliberately not used — it has no transactions, advisory
/// locks or filtered unique indexes, which several invariants depend on.
/// </summary>
public sealed class PostgresDatabase : IAsyncLifetime
{
    /// <summary>Connection string to a server where the user may CREATE/DROP DATABASE (any database name).</summary>
    public const string ConnectionVariable = "KRISHILINK_TEST_POSTGRES";

    /// <summary>When "true", a missing server fails the run instead of skipping the database suites (set in CI).</summary>
    public const string RequiredVariable = "KRISHILINK_TEST_POSTGRES_REQUIRED";

    public static string? ServerConnection => Environment.GetEnvironmentVariable(ConnectionVariable) ?? "Host=127.0.0.1;Port=5432;Username=postgres;Password=postgres;Database=postgres";
    public static bool IsConfigured => !string.IsNullOrWhiteSpace(ServerConnection);
    public static bool IsRequired => true;

    private string? _databaseName;

    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>When "true", every database suite runs with Database:ShardedWorkflowLocks on (CI runs both modes).</summary>
    public const string ShardedLocksVariable = "KRISHILINK_TEST_SHARDED_LOCKS";

    public async Task InitializeAsync()
    {
        if (string.Equals(Environment.GetEnvironmentVariable(ShardedLocksVariable), "true", StringComparison.OrdinalIgnoreCase))
            KrishiLink.DAL.Repositories.WorkflowTransaction.ShardedLocks = true;

        if (!IsConfigured)
        {
            if (IsRequired) throw new InvalidOperationException($"{ConnectionVariable} must be set when {RequiredVariable}=true.");
            return;
        }

        var server = new NpgsqlConnectionStringBuilder(ServerConnection) { Pooling = false };
        _databaseName = $"krishilink_test_{Guid.NewGuid():N}";
        await ExecuteAsync(server.ConnectionString, $"CREATE DATABASE \"{_databaseName}\"");

        ConnectionString = new NpgsqlConnectionStringBuilder(ServerConnection)
        {
            Database = _databaseName,
            Timezone = "UTC",
            IncludeErrorDetail = true
        }.ConnectionString;

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_databaseName is null) return;
        NpgsqlConnection.ClearAllPools();
        var server = new NpgsqlConnectionStringBuilder(ServerConnection) { Pooling = false };
        await ExecuteAsync(server.ConnectionString, $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)");
    }

    public DbContextOptions<ApplicationDbContext> Options => new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql(ConnectionString, postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", DatabaseConfiguration.Schema))
        .Options;

    public ApplicationDbContext CreateContext() => new(Options);

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresDatabase>
{
    public const string Name = "PostgreSQL";
}

/// <summary>A fact that needs <see cref="PostgresDatabase"/>; skipped locally when no server is configured.</summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (!PostgresDatabase.IsConfigured && !PostgresDatabase.IsRequired)
            Skip = $"Set {PostgresDatabase.ConnectionVariable} to a PostgreSQL server connection string to run database tests.";
    }
}

/// <summary>A theory that needs <see cref="PostgresDatabase"/>; skipped locally when no server is configured.</summary>
public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (!PostgresDatabase.IsConfigured && !PostgresDatabase.IsRequired)
            Skip = $"Set {PostgresDatabase.ConnectionVariable} to a PostgreSQL server connection string to run database tests.";
    }
}
