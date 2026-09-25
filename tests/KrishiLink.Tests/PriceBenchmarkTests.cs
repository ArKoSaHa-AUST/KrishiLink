using KrishiLink.BLL.Services;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using KrishiLink.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace KrishiLink.Tests;

/// <summary>DIS-03: price context comes from completed bookings only, and says nothing rather than guess from too few.</summary>
public class PriceBenchmarkTests
{
    [Fact]
    public void Percentiles_interpolate_like_percentile_cont()
    {
        var values = new[] { 1000m, 2000m, 3000m, 4000m, 5000m };
        Assert.Equal(3000m, PriceBenchmarkService.Percentile(values, 0.5));
        Assert.Equal(2000m, PriceBenchmarkService.Percentile(values, 0.25));
        Assert.Equal(4000m, PriceBenchmarkService.Percentile(values, 0.75));
        Assert.Equal(2500m, PriceBenchmarkService.Percentile(new[] { 2000m, 3000m }, 0.5));
    }

    [Fact]
    public void Fewer_than_five_bookings_give_no_benchmark() =>
        Assert.Null(PriceBenchmarkService.Compute(new[] { (3, 1000m), (3, 1100m), (4, 1200m), (5, 1300m) }, 3));

    [Fact]
    public void The_same_month_is_used_when_it_has_enough_bookings()
    {
        var prices = Enumerable.Range(0, 5).Select(i => (Month: 3, Price: 3000m + i * 100)).Concat(new[] { (7, 9000m), (8, 9500m) }).ToList();
        var benchmark = PriceBenchmarkService.Compute(prices, 3)!;
        Assert.Equal(BenchmarkScope.DistrictMonth, benchmark.Scope);
        Assert.Equal(3, benchmark.Month);
        Assert.Equal(3200m, benchmark.Median);
        Assert.Equal(5, benchmark.SampleSize);
    }

    [Fact]
    public void A_thin_month_falls_back_to_the_whole_year_in_the_district()
    {
        var prices = new[] { (1, 2000m), (2, 2500m), (6, 3000m), (9, 3500m), (11, 4000m), (3, 3100m) };
        var benchmark = PriceBenchmarkService.Compute(prices, 3)!;
        Assert.Equal(BenchmarkScope.District, benchmark.Scope);
        Assert.Null(benchmark.Month);
        Assert.Equal(6, benchmark.SampleSize);
    }

    [Theory]
    [InlineData(1500, PricePosition.Below)]
    [InlineData(3000, PricePosition.Typical)]
    [InlineData(2900, PricePosition.Typical)]
    [InlineData(5000, PricePosition.Above)]
    public void A_price_is_placed_against_the_middle_half(decimal price, PricePosition expected) =>
        Assert.Equal(expected, new PriceBenchmark(3500m, 2900m, 4100m, 8, BenchmarkScope.District, null).PositionOf(price));
}

[Collection(PostgresCollection.Name)]
public class PriceBenchmarkDatabaseTests
{
    private readonly PostgresDatabase _database;

    public PriceBenchmarkDatabaseTests(PostgresDatabase database) => _database = database;

    private static async Task CompleteRentalAsync(Marketplace market, string owner, string farmer, int equipmentId, DateTime start, DateTime end)
    {
        var booking = await market.RequestRentalOrThrowAsync(farmer, equipmentId, start, end);
        Assert.True((await market.RespondAsync(owner, booking, "accept")).Success);
        Assert.Null(await market.PayAsync(farmer, booking));
        Assert.True((await market.RespondAsync(owner, booking, "complete")).Success);
    }

    [PostgresFact]
    public async Task Only_completed_rentals_in_the_district_count_and_the_price_is_per_unit_day_as_paid()
    {
        await using var market = new Marketplace(_database);
        var owner = await market.AddUserAsync(AppRoles.EquipmentOwner);
        var farmer = await market.AddUserAsync(AppRoles.Farmer);
        var category = $"Bench-{Guid.NewGuid():N}"[..20];
        var district = "Rangpur";

        var rates = new[] { 2000m, 2500m, 3000m, 3500m, 4000m };
        var start = DateTime.Today.AddDays(3);
        var ids = new List<int>();
        foreach (var rate in rates)
        {
            var id = await market.AddEquipmentAsync(owner, dailyRate: rate, quantity: 1);
            ids.Add(id);
            await market.InScopeAsync(async sp =>
            {
                var db = sp.GetRequiredService<ApplicationDbContext>();
                var e = await db.Equipment.SingleAsync(x => x.Id == id);
                e.Category = category;
                e.District = district;
                await db.SaveChangesAsync();
            });
        }

        async Task<PriceBenchmark?> BenchmarkAsync() => await market.InScopeAsync(sp =>
            new PriceBenchmarkService(sp.GetRequiredService<ApplicationDbContext>(), new MemoryCache(new MemoryCacheOptions()))
                .ForEquipmentAsync(category, district, start));

        // Four completed rentals, plus a pending and a rejected one: still not enough.
        for (var i = 0; i < 4; i++) await CompleteRentalAsync(market, owner, farmer, ids[i], start, start.AddDays(1));
        await market.RequestRentalOrThrowAsync(farmer, ids[4], start.AddDays(10), start.AddDays(10));
        var rejected = await market.RequestRentalOrThrowAsync(farmer, ids[4], start.AddDays(12), start.AddDays(12));
        Assert.True((await market.RespondAsync(owner, rejected, "reject")).Success);
        Assert.Null(await BenchmarkAsync());

        await CompleteRentalAsync(market, owner, farmer, ids[4], start.AddDays(20), start.AddDays(22));
        var benchmark = await BenchmarkAsync();
        Assert.NotNull(benchmark);
        Assert.Equal(5, benchmark!.SampleSize);
        Assert.Equal(3000m, benchmark.Median);          // two-day and three-day rentals both reduce to the daily rate
        Assert.Equal(2500m, benchmark.P25);
        Assert.Equal(3500m, benchmark.P75);

        // Another district's rentals never leak in.
        Assert.Null(await market.InScopeAsync(sp =>
            new PriceBenchmarkService(sp.GetRequiredService<ApplicationDbContext>(), new MemoryCache(new MemoryCacheOptions()))
                .ForEquipmentAsync(category, "Sylhet", start)));
    }
}
