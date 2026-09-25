using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using KrishiLink.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace KrishiLink.Tests;

/// <summary>State-machine rules that depend on stored data (payment, payout, reviews, modification).</summary>
[Collection(PostgresCollection.Name)]
public class BookingLifecycleTests
{
    private readonly PostgresDatabase _database;

    public BookingLifecycleTests(PostgresDatabase database) => _database = database;

    private async Task<(Marketplace Market, string Owner, string Farmer, int EquipmentId)> SetUpAsync(int quantity = 1)
    {
        var market = new Marketplace(_database);
        var owner = await market.AddUserAsync(AppRoles.EquipmentOwner);
        var farmer = await market.AddUserAsync(AppRoles.Farmer);
        var equipmentId = await market.AddEquipmentAsync(owner, quantity: quantity);
        return (market, owner, farmer, equipmentId);
    }

    [PostgresFact]
    public async Task Complete_without_payment_is_refused()
    {
        var (market, owner, farmer, equipmentId) = await SetUpAsync();
        await using var _ = market;
        var id = await market.RequestRentalOrThrowAsync(farmer, equipmentId, DateTime.Today.AddDays(3), DateTime.Today.AddDays(4));
        Assert.True((await market.RespondAsync(owner, id, "accept")).Success);

        var result = await market.RespondAsync(owner, id, "complete");

        Assert.False(result.Success);
        Assert.Equal("This booking can't be completed until the farmer has paid.", result.Error);
        Assert.Equal(BookingStatus.Accepted, (await market.GetBookingAsync(id)).Status);
    }

    [PostgresFact]
    public async Task Undo_after_payout_is_refused()
    {
        var (market, owner, farmer, equipmentId) = await SetUpAsync();
        await using var _ = market;
        var id = await market.RequestRentalOrThrowAsync(farmer, equipmentId, DateTime.Today.AddDays(3), DateTime.Today.AddDays(4));
        Assert.True((await market.RespondAsync(owner, id, "accept")).Success);
        Assert.Null(await market.PayAsync(farmer, id));
        Assert.True((await market.RespondAsync(owner, id, "complete")).Success);
        Assert.Null(await market.RequestPayoutAsync(owner));
        Assert.Equal(1, await market.SettlePayoutsAsync(owner));

        var result = await market.RespondAsync(owner, id, "undo");

        Assert.False(result.Success);
        Assert.Equal("This booking has already been paid out and can no longer be changed.", result.Error);
        await market.AssertLedgerIsSoundAsync(nameof(BookingLifecycleTests));
    }

    [PostgresFact]
    public async Task Failed_payout_releases_the_bookings_so_they_can_be_undone_or_paid_out_again()
    {
        var (market, owner, farmer, equipmentId) = await SetUpAsync();
        await using var _ = market;
        var id = await market.RequestRentalOrThrowAsync(farmer, equipmentId, DateTime.Today.AddDays(3), DateTime.Today.AddDays(4));
        Assert.True((await market.RespondAsync(owner, id, "accept")).Success);
        Assert.Null(await market.PayAsync(farmer, id));
        Assert.True((await market.RespondAsync(owner, id, "complete")).Success);
        Assert.Null(await market.RequestPayoutAsync(owner, Marketplace.RejectedWallet));
        Assert.Equal(1, await market.SettlePayoutsAsync(owner));

        Assert.Null((await market.GetBookingAsync(id)).PayoutId);
        Assert.Null(await market.RequestPayoutAsync(owner));
        await market.AssertLedgerIsSoundAsync(nameof(BookingLifecycleTests));
    }

    [PostgresFact]
    public async Task Modify_while_paid_is_refused()
    {
        var (market, owner, farmer, equipmentId) = await SetUpAsync();
        await using var _ = market;
        var id = await market.RequestRentalOrThrowAsync(farmer, equipmentId, DateTime.Today.AddDays(3), DateTime.Today.AddDays(4));
        Assert.True((await market.RespondAsync(owner, id, "accept")).Success);
        Assert.Null(await market.PayAsync(farmer, id));

        var (error, needsReapproval) = await market.ModifyAsync(farmer, id, DateTime.Today.AddDays(5), DateTime.Today.AddDays(6));

        Assert.Equal("This booking has been paid and its payment is locked. Please cancel and request a refund instead of modifying.", error);
        Assert.False(needsReapproval);
        var booking = await market.GetBookingAsync(id);
        Assert.Equal(BookingStatus.Paid, booking.Status);
        Assert.Equal(DateTime.Today.AddDays(3), booking.StartDate);
    }

    [PostgresFact]
    public async Task Modifying_an_accepted_unpaid_booking_returns_it_to_pending_and_clears_the_price_snapshot()
    {
        var (market, owner, farmer, equipmentId) = await SetUpAsync();
        await using var _ = market;
        var id = await market.RequestRentalOrThrowAsync(farmer, equipmentId, DateTime.Today.AddDays(3), DateTime.Today.AddDays(4));
        Assert.True((await market.RespondAsync(owner, id, "accept")).Success);
        Assert.NotNull((await market.GetBookingAsync(id)).AgreedGross);

        var (error, needsReapproval) = await market.ModifyAsync(farmer, id, DateTime.Today.AddDays(5), DateTime.Today.AddDays(7));

        Assert.Null(error);
        Assert.True(needsReapproval);
        var booking = await market.GetBookingAsync(id);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Null(booking.AgreedGross);
        Assert.Null(booking.CommissionRate);
        Assert.Equal(3000m, booking.QuotedGross);
        Assert.Equal(1, booking.ModificationCount);
    }

    [PostgresFact]
    public async Task A_reviewed_booking_cannot_be_reopened()
    {
        var (market, owner, farmer, equipmentId) = await SetUpAsync();
        await using var _ = market;
        var id = await market.RequestRentalOrThrowAsync(farmer, equipmentId, DateTime.Today.AddDays(3), DateTime.Today.AddDays(4));
        Assert.True((await market.RespondAsync(owner, id, "accept")).Success);
        Assert.Null(await market.PayAsync(farmer, id));
        Assert.True((await market.RespondAsync(owner, id, "complete")).Success);
        Assert.True((await market.ReviewAsync(farmer, id)).Success);

        var result = await market.RespondAsync(owner, id, "undo");

        Assert.False(result.Success);
        Assert.Equal("This booking has been reviewed by the farmer and can no longer be reopened.", result.Error);
        Assert.Equal(BookingStatus.Completed, (await market.GetBookingAsync(id)).Status);
    }

    [PostgresFact]
    public async Task Cancelling_a_paid_booking_before_it_starts_refunds_the_full_amount()
    {
        var (market, owner, farmer, equipmentId) = await SetUpAsync();
        await using var _ = market;
        var id = await market.RequestRentalOrThrowAsync(farmer, equipmentId, DateTime.Today.AddDays(3), DateTime.Today.AddDays(5));
        Assert.True((await market.RespondAsync(owner, id, "accept")).Success);
        Assert.Null(await market.PayAsync(farmer, id));

        var (error, refunded) = await market.CancelAsync(farmer, id);

        Assert.Null(error);
        Assert.Equal(3000m, refunded);
        var booking = await market.GetBookingAsync(id);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(PaymentStatus.Refunded, booking.Payment!.Status);
        await market.AssertLedgerIsSoundAsync(nameof(BookingLifecycleTests));
    }

    [PostgresFact]
    public async Task A_booking_that_has_started_cannot_be_cancelled()
    {
        var (market, owner, farmer, equipmentId) = await SetUpAsync();
        await using var _ = market;
        var id = await market.RequestRentalOrThrowAsync(farmer, equipmentId, DateTime.Today, DateTime.Today.AddDays(2));
        Assert.True((await market.RespondAsync(owner, id, "accept")).Success);

        var (error, _) = await market.CancelAsync(farmer, id);

        Assert.Equal("This booking has already started and can no longer be cancelled. Please contact the owner.", error);
    }

    [PostgresFact]
    public async Task Accept_re_quotes_with_the_rules_in_force_at_acceptance()
    {
        var (market, owner, farmer, equipmentId) = await SetUpAsync();
        await using var _ = market;
        var start = DateTime.Today.AddDays(10);
        var id = await market.RequestRentalOrThrowAsync(farmer, equipmentId, start, start.AddDays(1));
        Assert.Equal(2000m, (await market.GetBookingAsync(id)).QuotedGross);

        await market.InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            db.EquipmentRateRules.Add(new EquipmentRateRule
            {
                EquipmentId = equipmentId,
                Kind = RateRuleKind.Season,
                Name = "Harvest peak",
                DailyRate = 1800m,
                StartDate = start,
                EndDate = start.AddDays(1),
                IsActive = true
            });
            await db.SaveChangesAsync();
        });

        Assert.True((await market.RespondAsync(owner, id, "accept")).Success);

        var booking = await market.GetBookingAsync(id);
        Assert.Equal(3600m, booking.AgreedGross);
        Assert.Equal(0.05m, booking.CommissionRate);
        Assert.EndsWith("(updated price)", booking.PricingNote);
    }
}
