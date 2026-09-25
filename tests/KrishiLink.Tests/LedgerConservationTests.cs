using KrishiLink.Models.Entities;
using KrishiLink.Tests.Infrastructure;
using Xunit.Abstractions;

namespace KrishiLink.Tests;

/// <summary>
/// Σ PaymentIn − Σ Refund = EscrowBalance + Σ CommissionEarned − Σ CommissionReversed + Σ PayoutOut,
/// checked after every single step of randomized pay → complete → undo → refund → payout sequences.
/// </summary>
[Collection(PostgresCollection.Name)]
public class LedgerConservationTests
{
    private readonly PostgresDatabase _database;
    private readonly ITestOutputHelper _output;

    public LedgerConservationTests(PostgresDatabase database, ITestOutputHelper output)
    {
        _database = database;
        _output = output;
    }

    [PostgresFact]
    public async Task Escrow_is_conserved_after_every_step_of_randomized_money_flows()
    {
        await using var market = new Marketplace(_database);
        var owner = await market.AddUserAsync(AppRoles.EquipmentOwner);
        var farmers = new[]
        {
            await market.AddUserAsync(AppRoles.Farmer),
            await market.AddUserAsync(AppRoles.Farmer),
            await market.AddUserAsync(AppRoles.Farmer)
        };
        var equipmentId = await market.AddEquipmentAsync(owner, dailyRate: 1234m, quantity: 1000);

        // Fixed seed: a failure is reproducible, and the printed trail says exactly which step broke.
        const int seed = 20260924;
        var random = new Random(seed);
        var bookings = new List<(int Id, string FarmerId)>();
        var performed = new Dictionary<string, int>();

        for (var step = 0; step < 160; step++)
        {
            var loaded = await market.GetBookingsAsync(bookings.Select(b => b.Id).ToList());
            var states = bookings.Select(b => (b.Id, b.FarmerId, Booking: loaded[b.Id])).ToList();

            (int Id, string FarmerId, EquipmentBooking Booking)? Pick(Func<EquipmentBooking, bool> filter)
            {
                var candidates = states.Where(s => filter(s.Booking)).ToList();
                return candidates.Count == 0 ? null : candidates[random.Next(candidates.Count)];
            }

            string performedAction;
            switch (random.Next(8))
            {
                case 1 when Pick(b => b.Status == BookingStatus.Accepted) is { } accepted:
                    Assert.Null(await market.PayAsync(accepted.FarmerId, accepted.Id));
                    performedAction = "pay";
                    break;

                case 2 when Pick(b => b.Status == BookingStatus.Paid) is { } paid:
                    Assert.True((await market.RespondAsync(owner, paid.Id, "complete")).Success);
                    performedAction = "complete";
                    break;

                case 3 when Pick(b => b.Status == BookingStatus.Completed && b.PayoutId is null) is { } completed:
                    Assert.True((await market.RespondAsync(owner, completed.Id, "undo")).Success);
                    performedAction = "undo";
                    break;

                case 4 when Pick(b => b.Status is BookingStatus.Accepted or BookingStatus.Paid or BookingStatus.Pending) is { } open:
                    var (cancelError, refunded) = await market.CancelAsync(open.FarmerId, open.Id);
                    Assert.Null(cancelError);
                    Assert.Equal(open.Booking.Status == BookingStatus.Paid, refunded is not null);
                    performedAction = refunded is null ? "cancel" : "refund";
                    break;

                case 5:
                    var wallet = random.Next(3) == 0 ? Marketplace.RejectedWallet : Marketplace.GoodWallet;
                    var payoutError = await market.RequestPayoutAsync(owner, wallet);
                    if (payoutError is not null)
                    {
                        Assert.Equal("There is no pending balance to pay out yet.", payoutError);
                        performedAction = "payout-none";
                        break;
                    }
                    Assert.Equal(1, await market.SettlePayoutsAsync(owner));
                    performedAction = wallet == Marketplace.GoodWallet ? "payout" : "payout-failed";
                    break;

                case 6 when Pick(b => b.Status == BookingStatus.Accepted) is { } declined:
                    Assert.NotNull(await market.PayAsync(declined.FarmerId, declined.Id, Marketplace.RejectedWallet));
                    performedAction = "payment-declined";
                    break;

                case 7 when Pick(b => b.Status != BookingStatus.Completed) is { } target:
                    // Illegal moves must be refused and must not move money.
                    var illegal = target.Booking.Status == BookingStatus.Paid ? "undo" : "complete";
                    Assert.False((await market.RespondAsync(owner, target.Id, illegal)).Success);
                    performedAction = "illegal-refused";
                    break;

                default:
                    var farmer = farmers[random.Next(farmers.Length)];
                    var start = DateTime.Today.AddDays(random.Next(5, 60));
                    var id = await market.RequestRentalOrThrowAsync(farmer, equipmentId, start, start.AddDays(random.Next(0, 4)));
                    Assert.True((await market.RespondAsync(owner, id, "accept")).Success);
                    bookings.Add((id, farmer));
                    performedAction = "accept";
                    break;
            }

            performed[performedAction] = performed.GetValueOrDefault(performedAction) + 1;
            var check = await market.CheckConservationAsync();
            _output.WriteLine($"step {step,3} {performedAction,-17} {check.Detail}");
            Assert.Equal(check.Lhs, check.Rhs);
            await market.AssertLedgerIsSoundAsync($"step {step} ({performedAction}, seed {seed})");
        }

        foreach (var movement in new[] { "pay", "complete", "undo", "refund", "payout", "payout-failed", "illegal-refused" })
            Assert.True(performed.GetValueOrDefault(movement) > 0, $"The random walk never exercised '{movement}'; change the seed or step count.");
    }
}
