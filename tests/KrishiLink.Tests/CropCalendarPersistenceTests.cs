using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using KrishiLink.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KrishiLink.Tests;

/// <summary>The seeded agronomy must survive the round trip through PostgreSQL unchanged, or the advisor scores the wrong data.</summary>
[Collection(PostgresCollection.Name)]
public class CropCalendarPersistenceTests
{
    private readonly PostgresDatabase _database;

    public CropCalendarPersistenceTests(PostgresDatabase database) => _database = database;

    [PostgresTheory]
    [InlineData(CropWaterNeed.Low)]
    [InlineData(CropWaterNeed.Medium)]
    [InlineData(CropWaterNeed.High)]
    public async Task Every_water_need_is_stored_as_seeded(CropWaterNeed need)
    {
        await using var market = new Marketplace(_database);
        var name = $"Test crop {Guid.NewGuid():N}";
        await market.InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            db.CropCalendarEntries.Add(new CropCalendarEntry { Name = name, Key = name[10..], WaterNeed = need });
            await db.SaveChangesAsync();
        });

        var stored = await market.InScopeAsync(sp =>
            sp.GetRequiredService<ApplicationDbContext>().CropCalendarEntries.Where(c => c.Name == name).Select(c => c.WaterNeed).SingleAsync());
        Assert.Equal(need, stored);
    }
}
