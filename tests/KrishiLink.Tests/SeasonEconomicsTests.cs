using KrishiLink.BLL.Helpers;
using KrishiLink.BLL.Services;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using KrishiLink.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuestPDF.Fluent;

namespace KrishiLink.Tests;

/// <summary>REA-03: the units farmers speak in convert exactly and round-trip.</summary>
public class UnitFormatTests
{
    private static readonly AiAgentTests.PassThroughLocalizer L = new();

    [Theory]
    [InlineData(LandUnit.Decimal, 1)]
    [InlineData(LandUnit.Katha, 1.65)]
    [InlineData(LandUnit.Bigha, 33)]
    [InlineData(LandUnit.Acre, 100)]
    public void One_unit_is_the_documented_number_of_decimals(LandUnit unit, double decimals) =>
        Assert.Equal(decimals, UnitFormat.ToDecimals(1, unit), 10);

    [Theory]
    [InlineData(0.5)]
    [InlineData(33)]
    [InlineData(47.25)]
    [InlineData(1234.5)]
    public void Land_round_trips_through_every_unit(double decimals)
    {
        foreach (var unit in UnitFormat.LandUnits)
            Assert.Equal(decimals, UnitFormat.ToDecimals(UnitFormat.FromDecimals(decimals, unit), unit), 9);
        // decimal -> acre -> bigha -> decimal
        var acres = UnitFormat.FromDecimals(decimals, LandUnit.Acre);
        var bigha = UnitFormat.FromDecimals(UnitFormat.ToDecimals(acres, LandUnit.Acre), LandUnit.Bigha);
        Assert.Equal(decimals, UnitFormat.ToDecimals(bigha, LandUnit.Bigha), 9);
    }

    [Fact]
    public void A_bigha_is_33_decimals_and_20_katha() =>
        Assert.Equal(UnitFormat.ToDecimals(1, LandUnit.Bigha), UnitFormat.ToDecimals(20, LandUnit.Katha), 9);

    [Theory]
    [InlineData(40, 1)]
    [InlineData(1000, 25)]
    [InlineData(2600, 65)]
    public void Kilograms_and_maunds_convert_both_ways(double kg, double maund)
    {
        Assert.Equal(maund, UnitFormat.KgToMaund(kg), 9);
        Assert.Equal(kg, UnitFormat.MaundToKg(UnitFormat.KgToMaund(kg)), 9);
    }

    [Fact]
    public void An_acre_is_0_4047_hectares() => Assert.Equal(0.4047, UnitFormat.DecimalsToHectares(100), 4);

    [Theory]
    [InlineData(null, LandUnit.Decimal)]
    [InlineData("bigha", LandUnit.Bigha)]
    [InlineData("Acre", LandUnit.Acre)]
    [InlineData("hectare", LandUnit.Decimal)]
    [InlineData("99", LandUnit.Decimal)]
    public void Unknown_preferences_fall_back_to_decimals(string? stored, LandUnit expected) => Assert.Equal(expected, UnitFormat.Parse(stored));

    [Fact]
    public void The_local_unit_comes_first_with_the_metric_value_in_brackets()
    {
        using var _ = new InvariantCultureScope();
        Assert.Equal("1.5 bigha (0.20 ha)", UnitFormat.Land(49.5, LandUnit.Bigha, L));
        Assert.Equal("65 maund (2.6 t)", UnitFormat.Weight(2600, L));
        Assert.Equal("12 maund (480 kg)", UnitFormat.Weight(480, L));
        Assert.Equal("65–75 maund (2.6–3.0 t)", UnitFormat.WeightRange(2600, 3000, L));
    }
}

/// <summary>ECO-02: a harvest plan warns — never blocks — when an item is booked outside the crop's stage window.</summary>
public class CropTimingTests
{
    private static CropCalendarEntry Crop(int id, string name, string? profile, int[] sow, int[] grow, int[] harvest) =>
        new() { Id = id, Name = name, ProfileCropName = profile, SowingMonths = sow.ToList(), GrowingMonths = grow.ToList(), HarvestingMonths = harvest.ToList() };

    private static readonly CropCalendarEntry Aman = Crop(1, "T. Aman Rice (Transplanted Aman)", "Rice (Aman)", new[] { 6, 7, 8 }, new[] { 8, 9, 10 }, new[] { 11, 12 });
    private static readonly CropCalendarEntry Lentil = Crop(2, "Lentil (Masur Dal)", "Pulses", new[] { 10, 11 }, new[] { 11, 12, 1 }, new[] { 2, 3 });
    private static readonly CropCalendarEntry Mungbean = Crop(3, "Mungbean (Mug Dal)", "Pulses", new[] { 2, 3, 8 }, new[] { 3, 4, 9 }, new[] { 4, 5, 10 });
    private static readonly IReadOnlyList<CropCalendarEntry> Calendar = new[] { Aman, Lentil, Mungbean };

    private static CropTimingWarning? Check(string type, string? category, DateTime start, DateTime end, params CropCalendarEntry[] crops) =>
        CropTiming.Check(7, "Item", type, category, start, end, crops, crops.Length == 1 ? crops[0].Name : "Pulses");

    [Fact]
    public void A_combine_harvester_in_August_warns_for_Aman_rice_which_is_harvested_in_November_and_December()
    {
        var warning = Check(HarvestPlanItemType.Equipment, "Combine Harvester", new DateTime(2026, 8, 10), new DateTime(2026, 8, 12), Aman);
        Assert.NotNull(warning);
        Assert.Equal(PlanItemStage.Harvest, warning!.Stage);
        Assert.Equal(8, warning.BookedMonth);
        Assert.Contains(11, warning.ExpectedMonths);
        Assert.Contains(12, warning.ExpectedMonths);
    }

    [Fact]
    public void A_combine_harvester_in_the_harvest_window_is_fine() =>
        Assert.Null(Check(HarvestPlanItemType.Equipment, "Combine Harvester", new DateTime(2026, 11, 20), new DateTime(2026, 11, 22), Aman));

    [Fact]
    public void Tillage_the_month_before_sowing_is_fine_but_mid_harvest_is_not()
    {
        Assert.Null(Check(HarvestPlanItemType.Equipment, "Power Tiller", new DateTime(2026, 5, 25), new DateTime(2026, 5, 27), Aman));
        Assert.NotNull(Check(HarvestPlanItemType.Equipment, "Power Tiller", new DateTime(2026, 12, 1), new DateTime(2026, 12, 3), Aman));
    }

    [Fact]
    public void A_booking_that_spans_into_the_window_is_not_flagged() =>
        Assert.Null(Check(HarvestPlanItemType.Equipment, "Combine Harvester", new DateTime(2026, 10, 28), new DateTime(2026, 11, 2), Aman));

    [Fact]
    public void Storage_is_judged_by_its_first_day_so_a_long_store_after_harvest_is_fine()
    {
        Assert.Null(Check(HarvestPlanItemType.Godown, "Grain Warehouse", new DateTime(2026, 12, 15), new DateTime(2027, 6, 1), Aman));
        Assert.NotNull(Check(HarvestPlanItemType.Godown, "Grain Warehouse", new DateTime(2026, 7, 1), new DateTime(2026, 12, 31), Aman));
    }

    [Fact]
    public void Unclassified_equipment_and_unknown_crops_are_never_second_guessed()
    {
        Assert.Null(Check(HarvestPlanItemType.Equipment, "Other", new DateTime(2026, 8, 1), new DateTime(2026, 8, 2), Aman));
        Assert.Null(Check(HarvestPlanItemType.Equipment, "Combine Harvester", new DateTime(2026, 8, 1), new DateTime(2026, 8, 2)));
    }

    [Fact]
    public void A_crop_group_warns_only_when_no_crop_in_it_fits()
    {
        // Pulses = lentil (harvest Feb-Mar) + mungbean (Apr-May, Oct): October threshing fits mungbean.
        Assert.Null(Check(HarvestPlanItemType.Equipment, "Thresher", new DateTime(2026, 10, 5), new DateTime(2026, 10, 6), Lentil, Mungbean));
        Assert.NotNull(Check(HarvestPlanItemType.Equipment, "Thresher", new DateTime(2026, 8, 5), new DateTime(2026, 8, 6), Lentil, Mungbean));
    }

    [Fact]
    public void A_plan_crop_resolves_by_link_then_calendar_name_then_profile_group()
    {
        Assert.Equal(new[] { Lentil }, CropTiming.Resolve("Rice (Aman)", Lentil.Id, Calendar));
        Assert.Equal(new[] { Aman }, CropTiming.Resolve("t. aman rice (transplanted aman)", null, Calendar));
        Assert.Equal(new[] { Lentil, Mungbean }, CropTiming.Resolve("Pulses", null, Calendar));
        Assert.Empty(CropTiming.Resolve("Tea", null, Calendar));
        Assert.Empty(CropTiming.Resolve(null, null, Calendar));
    }
}

/// <summary>ECO-01: the season sheet's numbers are derived only from bookings, the farmer's inputs and the seeded yields.</summary>
public class SeasonSheetMathTests
{
    private static readonly CropCalendarEntry Aman = new()
    {
        Id = 1,
        Name = "T. Aman Rice",
        TypicalYieldPerAcreMin = 1.6,
        TypicalYieldPerAcreMax = 2.0,
        Source = "BRRI",
        SowingMonths = new() { 7 },
        HarvestingMonths = new() { 11 }
    };

    private static SeasonSheet Sheet(double? land, decimal? price, decimal platform, params decimal[] own) => new()
    {
        Plan = new HarvestPlan { Id = 1, Name = "Aman 2026", LandSizeDecimal = land, ExpectedPricePerKg = price },
        CropEntries = new[] { Aman },
        PlatformCosts = new[] { new SeasonPlatformCost(1, HarvestPlanItemType.Equipment, "Tiller", "Power Tiller", DateTime.Today, DateTime.Today, platform, false, BookingStatus.Accepted) },
        OwnCosts = own.Select((a, i) => new SeasonCost { Id = i + 1, Amount = a, Category = SeasonCostCategories.Seed }).ToList()
    };

    [Fact]
    public void Costs_add_up_and_scale_to_the_acre()
    {
        var sheet = Sheet(land: 50, price: 30m, platform: 4000m, 2000m, 1500m);
        Assert.Equal(7500m, sheet.TotalCost);
        Assert.Equal(150m, sheet.CostPerDecimal);
        Assert.Equal(15000m, sheet.CostPerAcre);
    }

    [Fact]
    public void Expected_return_uses_the_seeded_yield_the_land_and_the_farmers_price()
    {
        var sheet = Sheet(land: 50, price: 30m, platform: 4000m, 2000m, 1500m);
        Assert.Equal(800, sheet.YieldMinKg!.Value, 6);   // 1.6 t/acre × 0.5 acre
        Assert.Equal(1000, sheet.YieldMaxKg!.Value, 6);
        Assert.Equal(24000m, sheet.GrossMin);
        Assert.Equal(30000m, sheet.GrossMax);
        Assert.Equal(16500m, sheet.NetMin);
        Assert.Equal(22500m, sheet.NetMax);
        Assert.Equal(250, sheet.BreakEvenKg!.Value, 6);   // 7,500 ÷ 30
        Assert.Equal(500, sheet.BreakEvenKgPerAcre!.Value, 6);
        Assert.False(sheet.LossLikely);
    }

    [Fact]
    public void A_price_too_low_for_even_a_good_harvest_is_called_out() =>
        Assert.True(Sheet(land: 50, price: 5m, platform: 9000m).LossLikely);

    [Fact]
    public void Without_the_farmers_price_or_land_no_return_is_invented()
    {
        var noPrice = Sheet(land: 50, price: null, platform: 4000m);
        Assert.Null(noPrice.GrossMin);
        Assert.Null(noPrice.NetMax);
        Assert.Null(noPrice.BreakEvenKg);

        var noLand = Sheet(land: null, price: 30m, platform: 4000m);
        Assert.Null(noLand.YieldMinKg);
        Assert.Null(noLand.CostPerAcre);
        Assert.Null(noLand.GrossMax);
    }

    [Fact]
    public void A_crop_group_gives_no_yield_until_one_crop_is_chosen()
    {
        var sheet = new SeasonSheet
        {
            Plan = new HarvestPlan { Name = "Pulses", LandSizeDecimal = 50, ExpectedPricePerKg = 80m },
            CropEntries = new[] { Aman, new CropCalendarEntry { Id = 2, Name = "Lentil", TypicalYieldPerAcreMin = 0.5, TypicalYieldPerAcreMax = 0.7 } }
        };
        Assert.Null(sheet.Crop);
        Assert.Null(sheet.GrossMin);
    }

    [Fact]
    public void The_season_pdf_renders_including_bangla_names()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var sheet = Sheet(land: 50, price: 30m, platform: 4000m, 2000m);
        sheet.Plan.Name = "আমন ধান ২০২৬";
        var pdf = new SeasonSummaryDocument(sheet, "করিম মিয়া", DateTime.UtcNow).GeneratePdf();
        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }
}

/// <summary>ECO-01 against PostgreSQL: ownership, bookings as the source of platform costs, and advice → plan.</summary>
[Collection(PostgresCollection.Name)]
public class SeasonEconomicsServiceTests
{
    private readonly PostgresDatabase _database;

    public SeasonEconomicsServiceTests(PostgresDatabase database) => _database = database;

    private Marketplace Market() => new(_database, services =>
    {
        services.AddSingleton<IHostEnvironment>(new Microsoft.Extensions.Hosting.Internal.HostingEnvironment { ContentRootPath = CropAdvisorTests.RepoRoot() });
        services.AddScoped<ICropCalendarService, CropCalendarService>();
        services.AddScoped<IHarvestPlanService, HarvestPlanService>();
        services.AddScoped<ISeasonEconomicsService, SeasonEconomicsService>();
    });

    private static Task<int> AddCropAsync(Marketplace market) => market.InScopeAsync(async sp =>
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var crop = new CropCalendarEntry
        {
            Name = $"Season test crop {Guid.NewGuid():N}"[..40],
            Key = $"season-{Guid.NewGuid():N}"[..30],
            Season = "Rabi",
            TypicalYieldPerAcreMin = 1,
            TypicalYieldPerAcreMax = 2,
            SowingMonths = new() { 11 },
            HarvestingMonths = new() { 3 }
        };
        db.CropCalendarEntries.Add(crop);
        await db.SaveChangesAsync();
        return crop.Id;
    });

    private static Task<int> AddPlanAsync(Marketplace market, string farmerId) => market.InScopeAsync(async sp =>
    {
        var (error, planId) = await sp.GetRequiredService<IHarvestPlanService>().CreatePlanAsync(farmerId, "Season", "Wheat");
        Assert.Null(error);
        return planId!.Value;
    });

    [PostgresFact]
    public async Task Only_the_owning_farmer_can_see_or_change_a_season()
    {
        await using var market = Market();
        var farmer = await market.AddUserAsync(AppRoles.Farmer);
        var other = await market.AddUserAsync(AppRoles.Farmer);
        var planId = await AddPlanAsync(market, farmer);

        await market.InScopeAsync(async sp =>
        {
            var economics = sp.GetRequiredService<ISeasonEconomicsService>();
            Assert.Null(await economics.GetSheetAsync(other, planId));
            Assert.NotNull(await economics.AddCostAsync(other, planId, SeasonCostCategories.Seed, null, 500m, null));
            Assert.NotNull(await economics.UpdateInputsAsync(other, planId, null, 50, 30m));
            Assert.Null(await economics.AddCostAsync(farmer, planId, SeasonCostCategories.Seed, "BRRI dhan seed", 500m, null));
            var costId = (await economics.GetSheetAsync(farmer, planId))!.OwnCosts.Single().Id;
            Assert.False(await economics.DeleteCostAsync(other, planId, costId));
            Assert.True(await economics.DeleteCostAsync(farmer, planId, costId));
        });
    }

    [PostgresFact]
    public async Task Invalid_costs_and_inputs_are_refused()
    {
        await using var market = Market();
        var farmer = await market.AddUserAsync(AppRoles.Farmer);
        var planId = await AddPlanAsync(market, farmer);

        await market.InScopeAsync(async sp =>
        {
            var economics = sp.GetRequiredService<ISeasonEconomicsService>();
            Assert.NotNull(await economics.AddCostAsync(farmer, planId, "Gold", null, 100m, null));
            Assert.NotNull(await economics.AddCostAsync(farmer, planId, SeasonCostCategories.Labour, null, 0m, null));
            Assert.NotNull(await economics.AddCostAsync(farmer, planId, SeasonCostCategories.Labour, null, -5m, null));
            Assert.NotNull(await economics.UpdateInputsAsync(farmer, planId, null, -1, null));
            Assert.NotNull(await economics.UpdateInputsAsync(farmer, planId, null, null, 0m));
            Assert.NotNull(await economics.UpdateInputsAsync(farmer, planId, int.MaxValue, null, null));
            Assert.Empty((await economics.GetSheetAsync(farmer, planId))!.OwnCosts);
        });
    }

    [PostgresFact]
    public async Task Platform_costs_come_from_the_plans_bookings_and_leave_out_rejected_ones()
    {
        await using var market = Market();
        var owner = await market.AddUserAsync(AppRoles.EquipmentOwner);
        var farmer = await market.AddUserAsync(AppRoles.Farmer);
        var tiller = await market.AddEquipmentAsync(owner, dailyRate: 1000m, quantity: 1);
        var harvester = await market.AddEquipmentAsync(owner, dailyRate: 3000m, quantity: 1);
        var planId = await AddPlanAsync(market, farmer);
        var start = DateTime.Today.AddDays(20);

        var accepted = await market.RequestRentalOrThrowAsync(farmer, tiller, start, start.AddDays(1));
        Assert.True((await market.RespondAsync(owner, accepted, "accept")).Success);
        var rejected = await market.RequestRentalOrThrowAsync(farmer, harvester, start, start);
        Assert.True((await market.RespondAsync(owner, rejected, "reject")).Success);

        await market.InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            db.HarvestPlanItems.AddRange(
                new HarvestPlanItem { HarvestPlanId = planId, ItemType = HarvestPlanItemType.Equipment, ListingId = tiller, StartDate = start, EndDate = start.AddDays(1), BookingId = accepted },
                new HarvestPlanItem { HarvestPlanId = planId, ItemType = HarvestPlanItemType.Equipment, ListingId = harvester, StartDate = start, EndDate = start, BookingId = rejected },
                new HarvestPlanItem { HarvestPlanId = planId, ItemType = HarvestPlanItemType.Equipment, ListingId = harvester, StartDate = start.AddDays(5), EndDate = start.AddDays(5) });
            await db.SaveChangesAsync();
        });

        var sheet = await market.InScopeAsync(sp => sp.GetRequiredService<ISeasonEconomicsService>().GetSheetAsync(farmer, planId));
        Assert.Equal(2, sheet!.PlatformCosts.Count);
        var agreed = sheet.PlatformCosts.Single(c => c.ItemType == HarvestPlanItemType.Equipment && !c.IsEstimate);
        Assert.Equal(2000m, agreed.Amount);
        var draft = sheet.PlatformCosts.Single(c => c.IsEstimate);
        Assert.Equal(3000m, draft.Amount);
        Assert.Equal(5000m, sheet.PlatformTotal);
    }

    [PostgresFact]
    public async Task Saved_advice_starts_a_season_with_its_crop_and_land_size()
    {
        await using var market = Market();
        var farmer = await market.AddUserAsync(AppRoles.Farmer);
        var cropId = await AddCropAsync(market);
        var adviceId = await market.InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var advice = new SavedCropAdvisory { UserId = farmer, CropCalendarEntryId = cropId, Season = "Rabi", SoilType = "Loam", District = "Bogura", LandSizeDecimal = 66 };
            db.SavedCropAdvisories.Add(advice);
            await db.SaveChangesAsync();
            return advice.Id;
        });

        var other = await market.AddUserAsync(AppRoles.Farmer);
        var (stolenError, _) = await market.InScopeAsync(sp => sp.GetRequiredService<ISeasonEconomicsService>().StartFromAdviceAsync(other, adviceId));
        Assert.NotNull(stolenError);

        var (error, planId) = await market.InScopeAsync(sp => sp.GetRequiredService<ISeasonEconomicsService>().StartFromAdviceAsync(farmer, adviceId));
        Assert.Null(error);
        await market.InScopeAsync(async sp =>
        {
            var economics = sp.GetRequiredService<ISeasonEconomicsService>();
            Assert.Null(await economics.UpdateInputsAsync(farmer, planId!.Value, cropId, 66, 40m));
            var sheet = await economics.GetSheetAsync(farmer, planId.Value);
            Assert.Equal(cropId, sheet!.Crop!.Id);
            Assert.Equal(660, sheet.YieldMinKg!.Value, 6);
            Assert.Equal(26400m, sheet.GrossMin);
        });
    }
}
