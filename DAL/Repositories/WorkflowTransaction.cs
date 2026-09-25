using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KrishiLink.DAL.Repositories
{
    /// <summary>
    /// Serializes the coupled booking, payment, loyalty, review and payout read/check/write workflows.
    /// SaveChanges alone is atomic, but PostgreSQL READ COMMITTED does not lock earlier eligibility reads.
    /// Nested services share the caller's transaction; only the outermost scope commits or rolls back.
    /// <para>
    /// By default every workflow takes one platform-wide lock (correct, but unrelated owners wait for each other).
    /// With <see cref="ShardedLocks"/> on, a workflow that names the resources it contends for takes the platform lock
    /// in shared mode plus an exclusive lock per resource, always in ascending (domain, key) order so two workflows can
    /// never wait on each other in a cycle. A workflow that names nothing still takes the platform lock exclusively and
    /// therefore excludes every sharded one, so unconverted code paths stay safe. Locks are database-scoped and
    /// transaction-owned, so they also hold across multiple app instances.
    /// </para>
    /// </summary>
    public sealed class WorkflowTransaction : IAsyncDisposable
    {
        /// <summary>Set once at startup from Database:ShardedWorkflowLocks; false restores the single global lock.</summary>
        public static bool ShardedLocks { get; set; }

        private readonly ApplicationDbContext _db;
        private readonly IDbContextTransaction? _transaction;
        private bool _finished;

        private WorkflowTransaction(ApplicationDbContext db, IDbContextTransaction? transaction)
        {
            _db = db;
            _transaction = transaction;
        }

        public static Task<WorkflowTransaction> BeginAsync(ApplicationDbContext db, CancellationToken ct = default) =>
            BeginAsync(db, Array.Empty<WorkflowLock>(), ct);

        public static async Task<WorkflowTransaction> BeginAsync(ApplicationDbContext db, IEnumerable<WorkflowLock> locks, CancellationToken ct = default)
        {
            var requested = locks.Distinct().Order().ToList();

            if (db.Database.CurrentTransaction != null)
            {
                // Nested: the caller's locks must already cover this work. If a shard is missing, take it rather than
                // proceed unprotected (PostgreSQL aborts one side if that ever closes a wait cycle).
                if (db.HeldWorkflowLocks is { Exclusive: false } held)
                {
                    foreach (var missing in requested.Where(l => !held.Locks.Contains(l)))
                    {
                        await AcquireAsync(db, missing, ct);
                        held.Locks.Add(missing);
                    }
                }
                return new WorkflowTransaction(db, null);
            }

            var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            try
            {
                var sharded = ShardedLocks && requested.Count > 0;
                if (sharded)
                {
                    await db.Database.ExecuteSqlAsync(
                        $"SELECT pg_advisory_xact_lock_shared({WorkflowLock.Platform.Domain}, {WorkflowLock.Platform.Key})", ct);
                    foreach (var shard in requested) await AcquireAsync(db, shard, ct);
                }
                else
                {
                    await AcquireAsync(db, WorkflowLock.Platform, ct);
                }
                db.HeldWorkflowLocks = new HeldWorkflowLocks(!sharded, requested.ToHashSet());

                // Identity/QR lookups may have populated this scoped context before the lock was obtained.
                foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State == EntityState.Unchanged).ToList())
                    await entry.ReloadAsync(ct);

                return new WorkflowTransaction(db, transaction);
            }
            catch
            {
                db.HeldWorkflowLocks = null;
                await transaction.DisposeAsync();
                throw;
            }
        }

        private static Task AcquireAsync(ApplicationDbContext db, WorkflowLock workflowLock, CancellationToken ct) =>
            db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({workflowLock.Domain}, {workflowLock.Key})", ct);

        public async Task CommitAsync(CancellationToken ct = default)
        {
            if (_transaction == null || _finished) return;
            await _transaction.CommitAsync(ct);
            _finished = true;
            _db.HeldWorkflowLocks = null;
            await _transaction.DisposeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_transaction == null || _finished) return;
            await _transaction.DisposeAsync();
            _db.HeldWorkflowLocks = null;
            _db.ChangeTracker.Clear();
            _finished = true;
        }
    }

    /// <summary>The locks the current outermost workflow holds on this context.</summary>
    public sealed record HeldWorkflowLocks(bool Exclusive, HashSet<WorkflowLock> Locks);

    /// <summary>
    /// One PostgreSQL two-key advisory lock: <see cref="Domain"/> says what kind of resource, <see cref="Key"/> which one.
    /// All domains live in the application's own namespace so they cannot collide with other lock users.
    /// </summary>
    public readonly record struct WorkflowLock(int Domain, int Key) : IComparable<WorkflowLock>
    {
        private const int Namespace = 1263682376;

        /// <summary>The platform-wide lock every workflow used before sharding (unchanged key).</summary>
        public static readonly WorkflowLock Platform = new(Namespace, 1);

        public static WorkflowLock Equipment(int equipmentId) => new(Namespace + 1, equipmentId);
        public static WorkflowLock Godown(int godownId) => new(Namespace + 2, godownId);

        /// <summary>A farmer's points/vouchers or an owner's payouts. Hash collisions only add waiting, never unsafety.</summary>
        public static WorkflowLock User(string userId) => new(Namespace + 3, StableHash(userId));

        public static WorkflowLock Listing(string bookingType, int listingId) =>
            string.Equals(bookingType, "Godown", StringComparison.OrdinalIgnoreCase) ? Godown(listingId) : Equipment(listingId);

        public int CompareTo(WorkflowLock other) =>
            Domain != other.Domain ? Domain.CompareTo(other.Domain) : Key.CompareTo(other.Key);

        // FNV-1a: stable across processes and machines, unlike string.GetHashCode().
        private static int StableHash(string value)
        {
            unchecked
            {
                var hash = (int)2166136261;
                foreach (var b in Encoding.UTF8.GetBytes(value))
                    hash = (hash ^ b) * 16777619;
                return hash;
            }
        }
    }
}
