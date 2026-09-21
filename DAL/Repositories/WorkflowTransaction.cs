using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KrishiLink.DAL.Repositories
{
    /// <summary>
    /// Serializes the coupled booking, payment, loyalty, review and payout read/check/write workflows.
    /// SaveChanges alone is atomic, but PostgreSQL READ COMMITTED does not lock earlier eligibility reads.
    /// Nested services share the caller's transaction; only the outermost scope commits or rolls back.
    /// The shared lock favors correctness over write throughput: unrelated owners also wait for each other.
    /// Reviews use this lock too, so review submission and completed-booking reopening cannot race.
    /// </summary>
    public sealed class WorkflowTransaction : IAsyncDisposable
    {
        private readonly ApplicationDbContext _db;
        private readonly IDbContextTransaction? _transaction;
        private bool _finished;

        private WorkflowTransaction(ApplicationDbContext db, IDbContextTransaction? transaction)
        {
            _db = db;
            _transaction = transaction;
        }

        public static async Task<WorkflowTransaction> BeginAsync(ApplicationDbContext db, CancellationToken ct = default)
        {
            if (db.Database.CurrentTransaction != null)
                return new WorkflowTransaction(db, null);

            var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            try
            {
                // A database-scoped, transaction-owned lock also works across multiple app instances.
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1263682376, 1)", ct);

                // Identity/QR lookups may have populated this scoped context before the lock was obtained.
                foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State == EntityState.Unchanged).ToList())
                    await entry.ReloadAsync(ct);

                return new WorkflowTransaction(db, transaction);
            }
            catch
            {
                await transaction.DisposeAsync();
                throw;
            }
        }

        public async Task CommitAsync(CancellationToken ct = default)
        {
            if (_transaction == null || _finished) return;
            await _transaction.CommitAsync(ct);
            _finished = true;
            await _transaction.DisposeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_transaction == null || _finished) return;
            await _transaction.DisposeAsync();
            _db.ChangeTracker.Clear();
            _finished = true;
        }
    }
}
