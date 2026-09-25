using KrishiLink.BLL.Services;
using KrishiLink.DAL;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KrishiLink.Tests;

/// <summary>
/// REL-02: sharded workflow locks. Lock semantics are checked directly, then real parallel workflows run with sharding
/// on and must leave capacity, payouts and the ledger exactly as correct as the global lock does.
/// </summary>
[Collection(PostgresCollection.Name)]
public class WorkflowLockingTests
{
    private static readonly TimeSpan Blocked = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan Released = TimeSpan.FromSeconds(10);

    private readonly PostgresDatabase _database;

    public WorkflowLockingTests(PostgresDatabase database) => _database = database;

    private static async Task<T> WithShardingAsync<T>(bool enabled, Func<Task<T>> body)
    {
        var previous = WorkflowTransaction.ShardedLocks;
        WorkflowTransaction.ShardedLocks = enabled;
        try { return await body(); }
        finally { WorkflowTransaction.ShardedLocks = previous; }
    }

    private static Task WithShardingAsync(bool enabled, Func<Task> body) =>
        WithShardingAsync(enabled, async () => { await body(); return true; });

    /// <summary>Starts a workflow on a fresh connection; the returned task completes once its locks are held.</summary>
    private async Task<(ApplicationDbContext Db, Task<WorkflowTransaction> Started)> StartAsync(params WorkflowLock[] locks)
    {
        var db = _database.CreateContext();
        await db.Database.OpenConnectionAsync();
        return (db, WorkflowTransaction.BeginAsync(db, locks));
    }

    private static async Task<bool> CompletesWithinAsync(Task task, TimeSpan timeout) =>
        await Task.WhenAny(task, Task.Delay(timeout)) == task;

    [PostgresFact]
    public Task Sharded_workflows_on_different_listings_run_in_parallel_and_on_the_same_listing_queue() =>
        WithShardingAsync(true, async () =>
        {
            var (holderDb, holder) = await StartAsync(WorkflowLock.Equipment(900_001));
            await using var _ = holderDb;
            var held = await holder;

            var (otherDb, otherListing) = await StartAsync(WorkflowLock.Equipment(900_002));
            await using var __ = otherDb;
            Assert.True(await CompletesWithinAsync(otherListing, Released), "A different listing must not wait.");
            await (await otherListing).CommitAsync();

            var (sameDb, sameListing) = await StartAsync(WorkflowLock.Equipment(900_001));
            await using var ___ = sameDb;
            Assert.False(await CompletesWithinAsync(sameListing, Blocked), "The same listing must wait for the holder.");

            await held.CommitAsync();
            Assert.True(await CompletesWithinAsync(sameListing, Released));
            await (await sameListing).CommitAsync();
        });

    [PostgresFact]
    public Task A_workflow_that_names_no_resources_excludes_every_sharded_workflow() =>
        WithShardingAsync(true, async () =>
        {
            var (shardedDb, sharded) = await StartAsync(WorkflowLock.Godown(900_003));
            await using var _ = shardedDb;
            var held = await sharded;

            var (exclusiveDb, exclusive) = await StartAsync();
            await using var __ = exclusiveDb;
            Assert.False(await CompletesWithinAsync(exclusive, Blocked), "An unconverted workflow must wait for sharded ones.");
            await held.CommitAsync();
            Assert.True(await CompletesWithinAsync(exclusive, Released));
            var exclusiveHeld = await exclusive;

            var (laterDb, later) = await StartAsync(WorkflowLock.Godown(900_004));
            await using var ___ = laterDb;
            Assert.False(await CompletesWithinAsync(later, Blocked), "Sharded workflows must wait for an unconverted one.");
            await exclusiveHeld.CommitAsync();
            Assert.True(await CompletesWithinAsync(later, Released));
            await (await later).CommitAsync();
        });

    [PostgresFact]
    public Task With_sharding_off_every_workflow_serializes_on_the_platform_lock() =>
        WithShardingAsync(false, async () =>
        {
            var (firstDb, first) = await StartAsync(WorkflowLock.Equipment(900_005));
            await using var _ = firstDb;
            var held = await first;

            var (secondDb, second) = await StartAsync(WorkflowLock.Equipment(900_006));
            await using var __ = secondDb;
            Assert.False(await CompletesWithinAsync(second, Blocked));
            await held.CommitAsync();
            Assert.True(await CompletesWithinAsync(second, Released));
            await (await second).CommitAsync();
        });

    [PostgresFact]
    public Task Parallel_accepts_never_oversubscribe_a_listing() =>
        WithShardingAsync(true, async () =>
        {
            await using var market = new Marketplace(_database);
            var owner = await market.AddUserAsync(AppRoles.EquipmentOwner);
            var equipmentId = await market.AddEquipmentAsync(owner, quantity: 2);
            var start = DateTime.Today.AddDays(20);

            var requests = new List<int>();
            for (var i = 0; i < 8; i++)
            {
                var farmer = await market.AddUserAsync(AppRoles.Farmer);
                requests.Add(await market.RequestRentalOrThrowAsync(farmer, equipmentId, start, start.AddDays(2)));
            }

            var results = await Task.WhenAll(requests.Select(id => market.RespondAsync(owner, id, "accept")));

            Assert.Equal(2, results.Count(r => r.Success));
            var statuses = await Task.WhenAll(requests.Select(market.GetBookingAsync));
            Assert.Equal(2, statuses.Count(b => b.Status == BookingStatus.Accepted));
            Assert.Equal(0, await market.FreeUnitsAsync(equipmentId, start, start.AddDays(2)));
        });

    [PostgresFact]
    public Task A_payout_request_racing_an_undo_never_pays_out_an_undone_booking() =>
        WithShardingAsync(true, async () =>
        {
            await using var market = new Marketplace(_database);
            var owner = await market.AddUserAsync(AppRoles.EquipmentOwner);
            var farmer = await market.AddUserAsync(AppRoles.Farmer);
            var equipmentId = await market.AddEquipmentAsync(owner, quantity: 50);

            for (var round = 0; round < 8; round++)
            {
                var start = DateTime.Today.AddDays(10 + round);
                var id = await market.RequestRentalOrThrowAsync(farmer, equipmentId, start, start);
                Assert.True((await market.RespondAsync(owner, id, "accept")).Success);
                Assert.Null(await market.PayAsync(farmer, id));
                Assert.True((await market.RespondAsync(owner, id, "complete")).Success);

                var payout = market.RequestPayoutAsync(owner);
                var undo = market.RespondAsync(owner, id, "undo");
                await Task.WhenAll(payout, undo);
                var undone = (await undo).Success;

                var booking = await market.GetBookingAsync(id);
                if (undone)
                    Assert.Null(booking.PayoutId);
                else
                    Assert.NotNull(booking.PayoutId);
                await market.SettlePayoutsAsync(owner);
                await market.AssertLedgerIsSoundAsync($"round {round}");

                // Leave no completed-but-unpaid booking behind for the next round.
                if (booking.Status == BookingStatus.Paid)
                {
                    Assert.True((await market.RespondAsync(owner, id, "complete")).Success);
                    Assert.Null(await market.RequestPayoutAsync(owner));
                    await market.SettlePayoutsAsync(owner);
                }
            }
        });

    [PostgresFact]
    public Task Parallel_money_flows_across_owners_keep_the_ledger_sound() =>
        WithShardingAsync(true, async () =>
        {
            await using var market = new Marketplace(_database);
            var sharedFarmer = await market.AddUserAsync(AppRoles.Farmer);

            async Task RunOwnerAsync(int ownerIndex)
            {
                var owner = await market.AddUserAsync(AppRoles.EquipmentOwner);
                var farmer = await market.AddUserAsync(AppRoles.Farmer);
                var equipmentId = await market.AddEquipmentAsync(owner, dailyRate: 1000m + ownerIndex * 111m, quantity: 100);
                for (var i = 0; i < 6; i++)
                {
                    var who = i % 2 == 0 ? farmer : sharedFarmer;
                    var start = DateTime.Today.AddDays(5 + i);
                    var id = await market.RequestRentalOrThrowAsync(who, equipmentId, start, start.AddDays(1));
                    Assert.True((await market.RespondAsync(owner, id, "accept")).Success);
                    Assert.Null(await market.PayAsync(who, id));
                    if (i % 3 == 2)
                    {
                        Assert.Equal(2000m + ownerIndex * 222m, (await market.CancelAsync(who, id)).Refunded);
                        continue;
                    }
                    Assert.True((await market.RespondAsync(owner, id, "complete")).Success);
                    if (i == 4) Assert.True((await market.RespondAsync(owner, id, "undo")).Success);
                }
                Assert.Null(await market.RequestPayoutAsync(owner, ownerIndex % 2 == 0 ? Marketplace.GoodWallet : Marketplace.RejectedWallet));
            }

            var settling = Task.Run(async () =>
            {
                for (var i = 0; i < 10; i++)
                {
                    await market.InScopeAsync(sp => sp.GetRequiredService<IPayoutSettlementService>().SettleDuePayoutsAsync(ignoreDelay: true));
                    await Task.Delay(20);
                }
            });
            await Task.WhenAll(Enumerable.Range(0, 5).Select(RunOwnerAsync).Append(settling));
            await market.InScopeAsync(sp => sp.GetRequiredService<IPayoutSettlementService>().SettleDuePayoutsAsync(ignoreDelay: true));

            await market.AssertLedgerIsSoundAsync("after parallel owners");
            await using var db = _database.CreateContext();
            Assert.False(await db.Transactions.AnyAsync(t => t.Status == PayoutStatus.Processing));
        });
}
