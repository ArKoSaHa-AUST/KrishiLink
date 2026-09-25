using KrishiLink.BLL.Helpers;
using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.DAL
{
    /// <summary>
    /// Seeds application roles and crop reference data in every environment after migrations
    /// have been applied separately. Accounts are provisioned through Supabase Auth, not here.
    /// </summary>
    public static class DbInitializer
    {
        // Stable, application-specific PostgreSQL transaction lock key ("KrishiLink" seed namespace).
        // All instances must use this same key before checking or inserting reference data.
        private const long ReferenceDataLockKey = 5931888534012390475;

        // Held for the whole boot-time migrate + storage + seed sequence, across instances.
        private const long StartupLockKey = 5931888534012390476;

        /// <summary>
        /// Opens a dedicated connection holding a session-level advisory lock until disposed, so concurrently starting
        /// instances run migrations, bucket creation and seeding one at a time.
        /// </summary>
        public static async Task<IAsyncDisposable> AcquireStartupLockAsync(string connectionString, CancellationToken ct = default)
        {
            var connection = new Npgsql.NpgsqlConnection(connectionString);
            try
            {
                await connection.OpenAsync(ct);
                await using var command = new Npgsql.NpgsqlCommand("SELECT pg_advisory_lock(@key)", connection);
                command.Parameters.AddWithValue("key", StartupLockKey);
                await command.ExecuteNonQueryAsync(ct);
                return new StartupLock(connection);
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }

        private sealed class StartupLock(Npgsql.NpgsqlConnection connection) : IAsyncDisposable
        {
            public async ValueTask DisposeAsync()
            {
                try
                {
                    await using var command = new Npgsql.NpgsqlCommand("SELECT pg_advisory_unlock(@key)", connection);
                    command.Parameters.AddWithValue("key", StartupLockKey);
                    await command.ExecuteNonQueryAsync();
                }
                finally
                {
                    // Closing the session releases the lock even if the explicit unlock failed.
                    await connection.DisposeAsync();
                }
            }
        }

        public static async Task InitializeAsync(IServiceProvider services)
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({ReferenceDataLockKey})");

            foreach (var role in AppRoles.All)
            {
                if (await roleManager.RoleExistsAsync(role))
                    continue;

                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded)
                {
                    var errors = string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
                    throw new InvalidOperationException($"Failed to seed role '{role}': {errors}");
                }
            }

            var contentRoot = services.GetRequiredService<IHostEnvironment>().ContentRootPath;
            await SeedCropCalendarAsync(db, contentRoot);
            // Not stored in the database, but validated here so a broken rules file stops the deployment at startup.
            PestRuleSeed.Cached(contentRoot);
            SearchSynonyms.Cached(contentRoot);
            ReferenceDataSeed.LoadGeography(contentRoot);
            ReferenceDataSeed.LoadCategories(contentRoot);
            await transaction.CommitAsync();
        }

        /// <summary>
        /// Upserts the crop calendar from App_Data/seed/crop-calendar.json, matching stored rows by name so their ids (and
        /// anything that references them) survive. Rows no longer in the file are kept: saved advisories may point at them.
        /// </summary>
        private static async Task SeedCropCalendarAsync(ApplicationDbContext db, string contentRoot)
        {
            var seed = CropCalendarSeed.Load(contentRoot);
            var stored = await db.CropCalendarEntries.ToDictionaryAsync(entry => entry.Name, StringComparer.Ordinal);
            foreach (var entry in seed)
            {
                if (stored.TryGetValue(entry.Name, out var existing))
                {
                    CropCalendarSeed.CopyInto(entry, existing);
                }
                else
                {
                    var added = new CropCalendarEntry();
                    CropCalendarSeed.CopyInto(entry, added);
                    db.CropCalendarEntries.Add(added);
                }
            }
            await db.SaveChangesAsync();
        }
    }
}
