using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Tests.Infrastructure;

namespace KrishiLink.Tests;

/// <summary>Units-per-day capacity maths, blocked dates, self-exclusion and capacity-aware auto-rejection.</summary>
[Collection(PostgresCollection.Name)]
public class AvailabilityTests
{
    private readonly PostgresDatabase _database;

    public AvailabilityTests(PostgresDatabase database) => _database = database;

    private static DateTime Day(int offset) => DateTime.Today.AddDays(offset);

    [PostgresFact]
    public async Task Free_units_is_the_minimum_over_the_range_of_quantity_minus_units_booked_that_day()
    {
        await using var market = new Marketplace(_database);
        var owner = await market.AddUserAsync(AppRoles.EquipmentOwner);
        var farmer = await market.AddUserAsync(AppRoles.Farmer);
        var equipmentId = await market.AddEquipmentAsync(owner, quantity: 3);

        var first = await market.RequestRentalOrThrowAsync(farmer, equipmentId, Day(10), Day(12), units: 2);
        Assert.True((await market.RespondAsync(owner, first, "accept")).Success);
        var second = await market.RequestRentalOrThrowAsync(farmer, equipmentId, Day(12), Day(14), units: 1);
        Assert.True((await market.RespondAsync(owner, second, "accept")).Success);

        Assert.Equal(1, await market.FreeUnitsAsync(equipmentId, Day(10), Day(11)));
        Assert.Equal(0, await market.FreeUnitsAsync(equipmentId, Day(12), Day(12)));
        Assert.Equal(0, await market.FreeUnitsAsync(equipmentId, Day(9), Day(15)));
        Assert.Equal(2, await market.FreeUnitsAsync(equipmentId, Day(13), Day(14)));
        Assert.Equal(3, await market.FreeUnitsAsync(equipmentId, Day(15), Day(20)));
    }

    [PostgresFact]
    public async Task Pending_requests_do_not_consume_capacity()
    {
        await using var market = new Marketplace(_database);
        var owner = await market.AddUserAsync(AppRoles.EquipmentOwner);
        var farmer = await market.AddUserAsync(AppRoles.Farmer);
        var equipmentId = await market.AddEquipmentAsync(owner, quantity: 2);

        await market.RequestRentalOrThrowAsync(farmer, equipmentId, Day(10), Day(12), units: 2);

        Assert.Equal(2, await market.FreeUnitsAsync(equipmentId, Day(10), Day(12)));
        Assert.Null(await market.CheckAvailabilityAsync(equipmentId, Day(10), Day(12), units: 2));
    }

    [PostgresFact]
    public async Task A_booking_is_excluded_from_its_own_availability_check()
    {
        await using var market = new Marketplace(_database);
        var owner = await market.AddUserAsync(AppRoles.EquipmentOwner);
        var farmer = await market.AddUserAsync(AppRoles.Farmer);
        var equipmentId = await market.AddEquipmentAsync(owner, quantity: 1);
        var id = await market.RequestRentalOrThrowAsync(farmer, equipmentId, Day(10), Day(12));
        Assert.True((await market.RespondAsync(owner, id, "accept")).Success);

        Assert.Equal(0, await market.FreeUnitsAsync(equipmentId, Day(11), Day(13)));
        Assert.NotNull(await market.CheckAvailabilityAsync(equipmentId, Day(11), Day(13)));

        Assert.Equal(1, await market.FreeUnitsAsync(equipmentId, Day(11), Day(13), excludeBookingId: id));
        Assert.Null(await market.CheckAvailabilityAsync(equipmentId, Day(11), Day(13), excludeBookingId: id));
    }

    [PostgresFact]
    public async Task Blocked_dates_make_the_whole_range_unavailable()
    {
        await using var market = new Marketplace(_database);
        var owner = await market.AddUserAsync(AppRoles.EquipmentOwner);
        var farmer = await market.AddUserAsync(AppRoles.Farmer);
        var equipmentId = await market.AddEquipmentAsync(owner, quantity: 5);
        await market.BlockEquipmentDateAsync(equipmentId, Day(11));

        Assert.Equal(0, await market.FreeUnitsAsync(equipmentId, Day(10), Day(12)));
        Assert.Equal(5, await market.FreeUnitsAsync(equipmentId, Day(12), Day(14)));
        Assert.StartsWith("The owner has marked", await market.CheckAvailabilityAsync(equipmentId, Day(10), Day(12)));

        var (error, bookingId) = await market.RequestRentalAsync(farmer, equipmentId, Day(10), Day(12));
        Assert.NotNull(error);
        Assert.Null(bookingId);
    }

    [PostgresFact]
    public async Task Request_below_the_minimum_rental_days_is_refused()
    {
        await using var market = new Marketplace(_database);
        var owner = await market.AddUserAsync(AppRoles.EquipmentOwner);
        var farmer = await market.AddUserAsync(AppRoles.Farmer);
        var equipmentId = await market.AddEquipmentAsync(owner, minRentalDays: 3);

        var (error, _) = await market.RequestRentalAsync(farmer, equipmentId, Day(10), Day(11));
        Assert.Equal("This equipment must be rented for at least 3 days.", error);

        var (ok, id) = await market.RequestRentalAsync(farmer, equipmentId, Day(10), Day(12));
        Assert.Null(ok);
        Assert.NotNull(id);
    }

    [PostgresFact]
    public async Task Accepting_rejects_exactly_the_overlapping_pending_requests_that_no_longer_fit_in_request_order()
    {
        await using var market = new Marketplace(_database);
        var owner = await market.AddUserAsync(AppRoles.EquipmentOwner);
        var farmer = await market.AddUserAsync(AppRoles.Farmer);
        var equipmentId = await market.AddEquipmentAsync(owner, quantity: 2);

        var winner = await market.RequestRentalOrThrowAsync(farmer, equipmentId, Day(10), Day(13), units: 1);
        var stillFits = await market.RequestRentalOrThrowAsync(farmer, equipmentId, Day(10), Day(12), units: 1);
        var tooBig = await market.RequestRentalOrThrowAsync(farmer, equipmentId, Day(12), Day(12), units: 2);
        var noOverlap = await market.RequestRentalOrThrowAsync(farmer, equipmentId, Day(20), Day(21), units: 2);
        var alsoTooBig = await market.RequestRentalOrThrowAsync(farmer, equipmentId, Day(13), Day(13), units: 2);
        var hitsBlockedDay = await market.RequestRentalOrThrowAsync(farmer, equipmentId, Day(13), Day(14), units: 1);
        await market.BlockEquipmentDateAsync(equipmentId, Day(14));

        var result = await market.RespondAsync(owner, winner, "accept");

        Assert.True(result.Success);
        Assert.Equal(new[] { tooBig, alsoTooBig, hitsBlockedDay }, result.AutoRejectedIds);
        foreach (var id in result.AutoRejectedIds!)
        {
            var rejected = await market.GetBookingAsync(id);
            Assert.Equal(BookingStatus.Rejected, rejected.Status);
            Assert.Equal(BookingWorkflow.AutoRejectReason, rejected.RejectReason);
        }
        Assert.Equal(BookingStatus.Pending, (await market.GetBookingAsync(stillFits)).Status);
        Assert.Equal(BookingStatus.Pending, (await market.GetBookingAsync(noOverlap)).Status);
        Assert.Equal(BookingStatus.Accepted, (await market.GetBookingAsync(winner)).Status);
    }

    [PostgresFact]
    public async Task Accepting_into_a_full_day_is_refused()
    {
        await using var market = new Marketplace(_database);
        var owner = await market.AddUserAsync(AppRoles.EquipmentOwner);
        var farmer = await market.AddUserAsync(AppRoles.Farmer);
        var equipmentId = await market.AddEquipmentAsync(owner, quantity: 2);
        var a = await market.RequestRentalOrThrowAsync(farmer, equipmentId, Day(10), Day(11), units: 1);
        var b = await market.RequestRentalOrThrowAsync(farmer, equipmentId, Day(11), Day(12), units: 1);
        var c = await market.RequestRentalOrThrowAsync(farmer, equipmentId, Day(11), Day(11), units: 1);

        Assert.True((await market.RespondAsync(owner, a, "accept")).Success);
        // Accepting b auto-rejects c: day 11 is now full.
        var second = await market.RespondAsync(owner, b, "accept");
        Assert.True(second.Success);
        Assert.Equal(new[] { c }, second.AutoRejectedIds);

        var undo = await market.RespondAsync(owner, c, "undo");
        Assert.True(undo.Success);
        var third = await market.RespondAsync(owner, c, "accept");
        Assert.False(third.Success);
        Assert.StartsWith("Only 0 of 2 units are free on", third.Error);
    }
}
