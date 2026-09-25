using KrishiLink.BLL.Services;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using KrishiLink.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace KrishiLink.Tests;

/// <summary>ADV-07: the planner's dates are derived from the seed and the Dhaka date, never invented.</summary>
public class CropPlannerTests
{
    private static readonly Lazy<IReadOnlyList<CropCalendarEntry>> Seed = new(() => CropCalendarSeed.Load(CropAdvisorTests.RepoRoot()));
    private static CropCalendarEntry Crop(string key) => Seed.Value.Single(c => c.Key == key);

    [Fact]
    public void Every_seeded_duration_parses_to_a_sane_range()
    {
        Assert.All(Seed.Value, c =>
        {
            var d = CropPlanner.ParseDuration(c.DurationDays);
            Assert.True(d is { } r && r.Min > 0 && r.Min <= r.Max && r.Max <= 400, $"{c.Key}: '{c.DurationDays}'");
        });
    }

    [Theory]
    [InlineData("140 – 160 Days", 140, 160)]
    [InlineData("300 – 360 Days (10-12 Months)", 300, 360)]
    [InlineData("60 Days", 60, 60)]
    public void Durations_parse(string text, int min, int max) => Assert.Equal((min, max), CropPlanner.ParseDuration(text));

    [Fact]
    public void This_week_follows_the_crop_calendar_stage()
    {
        var boro = Crop("boro-rice");   // sow Nov-Jan, grow Jan-Mar, harvest Apr-May

        var sowing = CropPlanner.ThisWeek(boro, new DateTime(2026, 11, 10));
        Assert.Equal(CropStage.Sowing, sowing.Stage);
        Assert.Contains(sowing.Needs, n => n.Category == "Power Tiller");

        var harvest = CropPlanner.ThisWeek(boro, new DateTime(2026, 4, 15));
        Assert.Equal(CropStage.Harvesting, harvest.Stage);
        Assert.Contains(harvest.Needs, n => n.Category == "Combine Harvester");
        Assert.DoesNotContain(harvest.Needs, n => n.Stage == CropNeedStage.LandPreparation);

        var off = CropPlanner.ThisWeek(boro, new DateTime(2026, 8, 20));
        Assert.Equal(CropStage.OffSeason, off.Stage);
        Assert.Equal(new DateTime(2026, 11, 1), off.NextSowing);
        Assert.Empty(off.Needs);
    }

    [Fact]
    public void Sowing_on_time_gives_the_seeded_harvest_window()
    {
        var boro = Crop("boro-rice");
        var sim = CropPlanner.Simulate(boro, new DateTime(2026, 12, 10), today: new DateTime(2026, 9, 25));

        Assert.True(sim.InsideWindow);
        Assert.Equal(new DateTime(2026, 12, 10).AddDays(140), sim.HarvestFrom);
        Assert.Equal(new DateTime(2026, 12, 10).AddDays(160), sim.HarvestTo);
        Assert.Contains(sim.Needs, n => n.Stage == CropNeedStage.LandPreparation && n.To == new DateTime(2026, 12, 9));
        Assert.Contains(sim.Needs, n => n.Stage == CropNeedStage.Harvest && n.From == sim.HarvestFrom);
    }

    [Fact]
    public void Sowing_late_is_reported_and_a_monsoon_harvest_is_flagged()
    {
        var boro = Crop("boro-rice");
        var sim = CropPlanner.Simulate(boro, new DateTime(2027, 2, 20), today: new DateTime(2026, 9, 25));   // window ends 31 Jan

        Assert.Equal(3, sim.WeeksOutsideWindow);
        Assert.True(sim.HarvestInMonsoon);                 // 140-160 days later lands in July
        Assert.Contains(7, sim.MonsoonHarvestMonths);
    }

    [Fact]
    public void A_few_days_early_is_never_shown_as_on_time()
    {
        var boro = Crop("boro-rice");
        var sim = CropPlanner.Simulate(boro, new DateTime(2026, 10, 29), today: new DateTime(2026, 9, 25));
        Assert.Equal(-1, sim.WeeksOutsideWindow);
    }

    [Fact]
    public void The_calendar_file_is_valid_icalendar()
    {
        var sim = CropPlanner.Simulate(Crop("potato"), new DateTime(2026, 11, 15), today: new DateTime(2026, 9, 25));
        var ics = CropPlanner.ToIcs(sim, "Potato, winter; table", n => n.Category, "Sowing", "Harvest window", new DateTime(2026, 9, 25, 8, 0, 0, DateTimeKind.Utc));

        Assert.StartsWith("BEGIN:VCALENDAR\r\nVERSION:2.0\r\n", ics);
        Assert.EndsWith("END:VCALENDAR\r\n", ics);
        Assert.DoesNotContain("\n", ics.Replace("\r\n", string.Empty));    // CRLF only
        Assert.Equal(ics.Split("BEGIN:VEVENT").Length - 1, ics.Split("END:VEVENT").Length - 1);
        Assert.Contains("DTSTART;VALUE=DATE:20261115", ics);
        Assert.Contains("SUMMARY:Potato\\, winter\\; table: Sowing", ics);   // text escaped per RFC 5545
        Assert.Contains("DTSTAMP:20260925T080000Z", ics);
    }
}

/// <summary>ADV-06 against a real database: history is idempotent per (district, rule, day) and feedback is one vote per user.</summary>
[Collection(PostgresCollection.Name)]
public class PestAlertHistoryTests
{
    private readonly PostgresDatabase _database;

    public PestAlertHistoryTests(PostgresDatabase database) => _database = database;

    private static PestAlertHistoryService Service(IServiceProvider sp) =>
        new(sp.GetRequiredService<ApplicationDbContext>(), null!, null!, new MemoryCache(new MemoryCacheOptions()), NullLogger<PestAlertHistoryService>.Instance);

    private static readonly RegionalWeatherForecast LiveWeather = new() { District = "Bogura", Temperature = 25, Humidity = 92, Source = WeatherSource.Live };
    private static readonly EvaluatedPestAlert Blast = new() { RuleId = 1, Severity = "Critical", RiskPercentage = 80 };

    [PostgresFact]
    public async Task Recording_twice_keeps_one_row_and_estimated_weather_is_never_recorded()
    {
        await using var market = new Marketplace(_database);
        var district = "Hist" + Guid.NewGuid().ToString("N")[..8];

        await market.InScopeAsync(sp => Service(sp).RecordAsync(district, LiveWeather, new[] { Blast, Blast }));
        await market.InScopeAsync(sp => Service(sp).RecordAsync(district, LiveWeather, new[] { Blast }));
        await market.InScopeAsync(sp => Service(sp).RecordAsync(district + "x", new RegionalWeatherForecast { Source = WeatherSource.Estimated }, new[] { Blast }));

        var rows = await market.InScopeAsync(sp => sp.GetRequiredService<ApplicationDbContext>().PestAlertHistories.Where(h => h.District.StartsWith(district)).ToListAsync());
        var row = Assert.Single(rows);
        Assert.Equal((1, "Critical"), (row.RuleId, row.Severity));
        Assert.Contains("\"humidity\":92", row.WeatherSnapshotJson);

        var recent = await market.InScopeAsync(sp => Service(sp).GetRecentAsync(district, null));
        Assert.Equal(PestAlertHistoryService.WindowDays, recent.Days.Count);
        Assert.Single(recent.ByRule[1]);
        Assert.Equal(row.Id, recent.TodayIds[1]);
    }

    [PostgresFact]
    public async Task Feedback_is_one_vote_per_user_and_can_be_changed()
    {
        await using var market = new Marketplace(_database);
        var farmer = await market.AddUserAsync(AppRoles.Farmer);
        var district = "Fb" + Guid.NewGuid().ToString("N")[..8];
        await market.InScopeAsync(sp => Service(sp).RecordAsync(district, LiveWeather, new[] { Blast }));
        var historyId = (await market.InScopeAsync(sp => Service(sp).GetRecentAsync(district, null))).TodayIds[1];

        Assert.True(await market.InScopeAsync(sp => Service(sp).RecordFeedbackAsync(farmer, historyId, accurate: true)));
        Assert.True(await market.InScopeAsync(sp => Service(sp).RecordFeedbackAsync(farmer, historyId, accurate: false)));
        Assert.False(await market.InScopeAsync(sp => Service(sp).RecordFeedbackAsync(farmer, historyId: int.MaxValue, accurate: true)));

        var votes = await market.InScopeAsync(sp => sp.GetRequiredService<ApplicationDbContext>().PestAlertFeedback.Where(f => f.PestAlertHistoryId == historyId).ToListAsync());
        var vote = Assert.Single(votes);
        Assert.False(vote.IsAccurate);
        Assert.Contains(historyId, (await market.InScopeAsync(sp => Service(sp).GetRecentAsync(district, farmer))).RatedIds);
    }
}
