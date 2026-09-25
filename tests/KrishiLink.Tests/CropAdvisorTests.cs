using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using KrishiLink.BLL.Helpers;
using KrishiLink.BLL.Services;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;

namespace KrishiLink.Tests;

/// <summary>ADV-01: the Smart Advisor is a deterministic, explainable scorer over the reviewed seed — never a mock.</summary>
public class CropAdvisorTests
{
    private static readonly AiAgentTests.PassThroughLocalizer L = new();

    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KrishiLink.csproj"))) dir = dir.Parent;
        return dir!.FullName;
    }

    private static readonly Lazy<IReadOnlyList<CropCalendarEntry>> Seed = new(() =>
        CropCalendarSeed.Load(RepoRoot()).Select((c, i) => { c.Id = i + 1; return c; }).ToList());

    private static CropAdvisoryInput Input(string season = "Rabi", string soil = "Clay Loam", bool irrigation = true,
        string district = "Bogura", double? ph = 6.5, double? land = 50) =>
        new(district, season, soil, ph, land, irrigation);

    public static IEnumerable<object[]> AllCombinations()
    {
        foreach (var season in CropAdvisorScorer.SeasonValues)
            foreach (var (soil, _) in SoilClassifier.FormOptions)
                foreach (var irrigation in new[] { true, false })
                    foreach (var month in new[] { 1, 4, 7, 10 })
                        yield return new object[] { season, soil, irrigation, month };
    }

    // ---------------------------------------------------------------- The seed file

    [Fact]
    public void The_seed_file_loads_and_passes_its_own_validation()
    {
        Assert.True(Seed.Value.Count >= 20);
        Assert.All(Seed.Value, c => Assert.False(string.IsNullOrWhiteSpace(c.Source)));
    }

    [Fact]
    public void Every_seeded_crop_names_a_soil_the_scorer_understands()
    {
        var unparsed = Seed.Value.Where(c => SoilClassifier.Parse(c.SoilTypes).Count == 0).Select(c => c.Key).ToList();
        Assert.Empty(unparsed);
    }

    [Fact]
    public void Every_major_district_and_division_in_the_seed_is_real()
    {
        var districts = BangladeshGeo.AllDistricts.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknownDistricts = Seed.Value.SelectMany(c => CropAdvisorScorer.MajorDistricts(c).Select(d => $"{c.Key}:{d}"))
            .Where(x => !districts.Contains(x.Split(':')[1])).ToList();
        var unknownDivisions = Seed.Value.Where(c => c.Division != "All")
            .SelectMany(c => c.Division.Split(',', StringSplitOptions.TrimEntries).Select(d => $"{c.Key}:{d}"))
            .Where(x => !BangladeshGeo.Divisions.Contains(x.Split(':')[1])).ToList();

        Assert.Empty(unknownDistricts);
        Assert.Empty(unknownDivisions);
    }

    [Fact]
    public void Every_seeded_crop_has_Bangla_counterparts_for_its_advice()
    {
        var missing = Seed.Value.Where(c => string.IsNullOrWhiteSpace(c.KeyTipsBn) || string.IsNullOrWhiteSpace(c.WaterRequirementBn) || string.IsNullOrWhiteSpace(c.SoilTypesBn))
            .Select(c => c.Key).ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void A_broken_seed_file_fails_loudly()
    {
        var root = Path.Combine(Path.GetTempPath(), "krishilink-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "App_Data", "seed"));
        try
        {
            File.WriteAllText(CropCalendarSeed.PathFor(root), """{ "schemaVersion": 1, "crops": [ { "key": "Bad Key", "name": "X", "season": "Winter", "sowingMonths": [13] } ] }""");
            var ex = Assert.Throws<InvalidOperationException>(() => CropCalendarSeed.Load(root));
            Assert.Contains("key must be a lowercase slug", ex.Message);
            Assert.Contains("season 'Winter'", ex.Message);
            Assert.Contains("months must be 1-12", ex.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // ---------------------------------------------------------------- The scorer

    [Fact]
    public void The_scorer_is_deterministic()
    {
        var first = CropAdvisorScorer.Recommend(Seed.Value, Input(), month: 11, L);
        var second = CropAdvisorScorer.Recommend(Seed.Value.Reverse(), Input(), month: 11, L);

        Assert.Equal(first.Matches.Select(m => (m.Crop.Key, m.Score)), second.Matches.Select(m => (m.Crop.Key, m.Score)));
        Assert.Equal(
            first.Matches.SelectMany(m => m.Factors.Select(f => f.Reason)),
            second.Matches.SelectMany(m => m.Factors.Select(f => f.Reason)));
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void A_season_mismatch_never_reaches_the_top_three(string season, string soil, bool irrigation, int month)
    {
        var result = CropAdvisorScorer.Recommend(Seed.Value, Input(season, soil, irrigation), month, L);

        foreach (var match in result.Matches.Take(3).Concat(result.ClosestOptions))
        {
            var seasonFactor = match.Factors.Single(f => f.Factor == AdvisorFactor.Season);
            Assert.True(seasonFactor.Points > 0, $"{match.Crop.Key} ({match.Crop.Season}) was offered for {season}.");
        }
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Factor_points_always_add_up_to_the_displayed_score(string season, string soil, bool irrigation, int month)
    {
        var input = Input(season, soil, irrigation);
        foreach (var crop in Seed.Value)
        {
            var match = CropAdvisorScorer.Score(crop, CropAdvisorScorer.Normalize(input, month), month, L);

            Assert.Equal(match.Factors.Sum(f => f.Points), match.Score);
            Assert.InRange(match.Score, 0, 100);
            Assert.All(match.Factors, f => Assert.InRange(f.Points, 0, f.MaxPoints));
            Assert.Equal(100, match.Factors.Sum(f => f.MaxPoints));
            Assert.All(match.Factors, f => Assert.False(string.IsNullOrWhiteSpace(f.Reason)));
        }
    }

    [Fact]
    public void Without_irrigation_a_high_water_crop_scores_zero_on_water_and_warns()
    {
        var boro = Seed.Value.Single(c => c.Key == "boro-rice");
        Assert.Equal(CropWaterNeed.High, boro.WaterNeed);

        var match = CropAdvisorScorer.Score(boro, CropAdvisorScorer.Normalize(Input(irrigation: false), 12), 12, L);
        var irrigation = match.Factors.Single(f => f.Factor == AdvisorFactor.Irrigation);

        Assert.Equal(0, irrigation.Points);
        Assert.True(irrigation.IsWarning);
        Assert.Contains(irrigation, match.Warnings);

        var irrigated = CropAdvisorScorer.Score(boro, CropAdvisorScorer.Normalize(Input(irrigation: true), 12), 12, L);
        Assert.Empty(irrigated.Warnings);
    }

    [Fact]
    public void Inputs_that_match_nothing_return_an_honest_empty_result_and_never_pad()
    {
        var poorFits = new[]
        {
            new CropCalendarEntry { Id = 1, Key = "paddy", Name = "Paddy", Season = "Kharif-2", SowingMonths = { 7 }, HarvestingMonths = { 11 },
                SoilTypes = "Clay Loam", WaterNeed = CropWaterNeed.High, Division = "Sylhet", MajorDistricts = "Sylhet", MinPh = 5.5, MaxPh = 6.5, Source = "TEST" },
            new CropCalendarEntry { Id = 2, Key = "wheat", Name = "Wheat", Season = "Rabi", SowingMonths = { 11 }, HarvestingMonths = { 3 },
                SoilTypes = "Loam", WaterNeed = CropWaterNeed.Low, Division = "Rajshahi", MajorDistricts = "Rajshahi", MinPh = 6.0, MaxPh = 7.5, Source = "TEST" }
        };

        var result = CropAdvisorScorer.Recommend(poorFits, Input("Kharif-2", "Sandy", irrigation: false, district: "Bandarban", ph: 3.5), month: 2, L);

        Assert.False(result.HasStrongMatch);
        Assert.Empty(result.Matches);
        var closest = Assert.Single(result.ClosestOptions);       // only the season-compatible crop, never the Rabi one
        Assert.Equal("paddy", closest.Crop.Key);
        Assert.True(closest.Score < CropAdvisorScorer.StrongMatchFloor);
    }

    [Fact]
    public void Results_are_capped_and_ordered_best_first()
    {
        var result = CropAdvisorScorer.Recommend(Seed.Value, Input(), month: 11, L);

        Assert.True(result.HasStrongMatch);
        Assert.InRange(result.Matches.Count, 1, CropAdvisorScorer.MaxMatches);
        Assert.Equal(result.Matches.Select(m => m.Score).OrderByDescending(s => s), result.Matches.Select(m => m.Score));
        Assert.All(result.Matches, m => Assert.True(m.Score >= CropAdvisorScorer.StrongMatchFloor));
    }

    [Fact]
    public void A_Barishal_farmer_gets_the_division_credit_that_old_spellings_used_to_miss()
    {
        var watermelon = Seed.Value.Single(c => c.Key == "watermelon");
        var match = CropAdvisorScorer.Score(watermelon, CropAdvisorScorer.Normalize(Input(district: "Jhalokati"), 11), 11, L);

        var region = match.Factors.Single(f => f.Factor == AdvisorFactor.Region);
        Assert.Equal(6, region.Points);
        Assert.True(BangladeshGeo.IsDistrictSuitable("Jhalokati", watermelon.Division));
    }

    [Fact]
    public void Old_district_spellings_in_the_seed_still_count_as_major_districts()
    {
        var boro = Seed.Value.Single(c => c.Key == "boro-rice");   // lists "Bogra"
        var match = CropAdvisorScorer.Score(boro, CropAdvisorScorer.Normalize(Input(district: "Bogura"), 12), 12, L);
        Assert.Equal(10, match.Factors.Single(f => f.Factor == AdvisorFactor.Region).Points);
    }

    [Fact]
    public void Yield_is_scaled_to_the_farmers_land()
    {
        var potato = Seed.Value.Single(c => c.Key == "potato");
        var halfAcre = CropAdvisorScorer.Score(potato, CropAdvisorScorer.Normalize(Input(land: 50), 11), 11, L);
        var perAcre = CropAdvisorScorer.Score(potato, CropAdvisorScorer.Normalize(Input(land: null), 11), 11, L);

        Assert.Equal(potato.TypicalYieldPerAcreMin!.Value / 2, halfAcre.YieldMinTonnes!.Value, 6);
        Assert.Equal(potato.TypicalYieldPerAcreMax!.Value / 2, halfAcre.YieldMaxTonnes!.Value, 6);
        Assert.False(halfAcre.YieldIsPerAcre);
        Assert.True(perAcre.YieldIsPerAcre);
        Assert.Equal(potato.TypicalYieldPerAcreMin, perAcre.YieldMinTonnes);
    }

    [Theory]
    [InlineData(0.0, 3.5)]
    [InlineData(14.0, 9.5)]
    [InlineData(6.54, 6.5)]
    public void Soil_pH_is_clamped_to_a_real_range(double posted, double expected)
    {
        Assert.Equal(expected, CropAdvisorScorer.Normalize(Input(ph: posted), 1).SoilPh);
    }

    [Theory]
    [InlineData(-20.0, null)]
    [InlineData(0.0, null)]
    [InlineData(5e9, 100000.0)]
    public void Land_size_is_clamped(double posted, double? expected)
    {
        Assert.Equal(expected, CropAdvisorScorer.Normalize(Input(land: posted), 1).LandSizeDecimal);
    }

    [Theory]
    [InlineData("Atlantis", "")]
    [InlineData("Bogra", "Bogura")]
    [InlineData("sylhet", "Sylhet")]
    public void Districts_are_canonicalized_or_dropped(string posted, string expected)
    {
        Assert.Equal(expected, CropAdvisorScorer.Normalize(Input(district: posted), 1).District);
    }

    [Theory]
    [InlineData("Rabi (Winter)", 7, "Rabi")]
    [InlineData("Kharif-2 (Monsoon)", 1, "Kharif-2")]
    [InlineData("nonsense", 5, "Kharif-1")]
    public void Seasons_are_normalized_with_the_current_season_as_fallback(string posted, int month, string expected)
    {
        Assert.Equal(expected, CropAdvisorScorer.NormalizeSeason(posted, month));
    }

    [Theory]
    [InlineData("Sandy Loam, River Char sandbeds (বেলে ও চরের বেলে-দোআঁশ)", new[] { SoilFamily.SandyLoam, SoilFamily.Sandy })]
    [InlineData("Heavy Loam, Clay Loam with rich compost", new[] { SoilFamily.Loam, SoilFamily.ClayLoam })]
    [InlineData("Clay Loam, Alluvial Silt (এঁটেল-দোআঁশ)", new[] { SoilFamily.ClayLoam, SoilFamily.Silt })]
    [InlineData("Deep fertile Loam, Silt Loam", new[] { SoilFamily.Loam, SoilFamily.SiltLoam })]
    public void Soil_descriptions_map_to_families(string text, SoilFamily[] expected)
    {
        Assert.Equal(expected.ToHashSet(), SoilClassifier.Parse(text).ToHashSet());
    }

    [Theory]
    [InlineData(1, "Rabi", "Rabi Season (রবি মৌসুম - শীতকালীন)")]
    [InlineData(2, "Rabi", "Rabi Season (রবি মৌসুম - শীতকালীন)")]
    [InlineData(3, "Rabi", "Rabi Season (রবি মৌসুম - শীতকালীন)")]
    [InlineData(4, "Kharif-1", "Kharif-1 Season (খরিফ-১ - প্রাক-খরিফ / গ্রীষ্মকালীন)")]
    [InlineData(5, "Kharif-1", "Kharif-1 Season (খরিফ-১ - প্রাক-খরিফ / গ্রীষ্মকালীন)")]
    [InlineData(6, "Kharif-1", "Kharif-1 Season (খরিফ-১ - প্রাক-খরিফ / গ্রীষ্মকালীন)")]
    [InlineData(7, "Kharif-2", "Kharif-2 Season (খরিফ-২ - বর্ষাকালীন)")]
    [InlineData(8, "Kharif-2", "Kharif-2 Season (খরিফ-২ - বর্ষাকালীন)")]
    [InlineData(9, "Kharif-2", "Kharif-2 Season (খরিফ-২ - বর্ষাকালীন)")]
    [InlineData(10, "Kharif-2", "Kharif-2 Season (খরিফ-২ - বর্ষাকালীন)")]
    [InlineData(11, "Rabi", "Rabi Season (রবি মৌসুম - শীতকালীন)")]
    [InlineData(12, "Rabi", "Rabi Season (রবি মৌসুম - শীতকালীন)")]
    public void SeasonBoundaryTest_all_12_months_map_consistently(int month, string expectedSeason, string expectedSeasonHero)
    {
        Assert.Equal(expectedSeason, CropAdvisorScorer.SeasonForMonth(month));
        Assert.Equal(expectedSeason, CropAdvisorService.SeasonForMonth(month));
        Assert.Equal(expectedSeasonHero, CropCalendarService.GetCurrentSeasonName(month));
    }

    [Fact]
    public void Multi_season_crops_are_tagged_YearRound_and_country_bean_is_Rabi()
    {
        Assert.Equal("YearRound", Seed.Value.Single(c => c.Key == "chili").Season);
        Assert.Equal("YearRound", Seed.Value.Single(c => c.Key == "mungbean").Season);
        Assert.Equal("YearRound", Seed.Value.Single(c => c.Key == "groundnut").Season);
        Assert.Equal("Rabi", Seed.Value.Single(c => c.Key == "country-bean").Season);
    }

    [Theory]
    [InlineData("Rabi")]
    [InlineData("Kharif-1")]
    [InlineData("Kharif-2")]
    public void YearRound_crops_pass_the_season_hard_gate_in_all_seasons(string season)
    {
        var yearRoundKeys = new[] { "chili", "mungbean", "groundnut", "brinjal", "sugarcane" };
        var input = Input(season: season, soil: "Loam", irrigation: true);

        foreach (var key in yearRoundKeys)
        {
            var crop = Seed.Value.Single(c => c.Key == key);
            var match = CropAdvisorScorer.Score(crop, CropAdvisorScorer.Normalize(input, 1), 1, L);
            var seasonFactor = match.Factors.Single(f => f.Factor == AdvisorFactor.Season);

            Assert.Equal(30, seasonFactor.Points);
            Assert.Equal(FactorState.Met, seasonFactor.State);
        }
    }
}

/// <summary>ADV-03 against a real database: saved advisories are recomputed server-side and capped at three per farmer.</summary>
[Collection(KrishiLink.Tests.Infrastructure.PostgresCollection.Name)]
public class SavedCropAdvisoryTests
{
    private readonly KrishiLink.Tests.Infrastructure.PostgresDatabase _database;

    public SavedCropAdvisoryTests(KrishiLink.Tests.Infrastructure.PostgresDatabase database) => _database = database;

    private KrishiLink.Tests.Infrastructure.Marketplace Market() => new(_database, services =>
    {
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(new SeedEnvironment());
        services.AddScoped<ICropCalendarService, CropCalendarService>();
        services.AddScoped<ICropAdvisorService, CropAdvisorService>();
    });

    private sealed class SeedEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "KrishiLink";
        public string ContentRootPath { get; set; } = CropAdvisorTests.RepoRoot();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private static async Task SeedCropsAsync(KrishiLink.Tests.Infrastructure.Marketplace market) => await market.InScopeAsync(async sp =>
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        if (await db.CropCalendarEntries.AnyAsync()) return;
        foreach (var entry in CropCalendarSeed.Load(CropAdvisorTests.RepoRoot()))
        {
            var row = new CropCalendarEntry();
            CropCalendarSeed.CopyInto(entry, row);
            db.CropCalendarEntries.Add(row);
        }
        await db.SaveChangesAsync();
    });

    [KrishiLink.Tests.Infrastructure.PostgresFact]
    public async Task Saving_recomputes_the_match_keeps_the_inputs_and_caps_at_three()
    {
        await using var market = Market();
        await SeedCropsAsync(market);
        var farmer = await market.AddUserAsync(AppRoles.Farmer);
        var input = new CropAdvisoryInput("Bogura", "Rabi", "Clay Loam", 6.5, 50, true);

        var result = await market.InScopeAsync(sp => sp.GetRequiredService<ICropAdvisorService>().RecommendAsync(input));
        Assert.True(result.Matches.Count >= 4);

        foreach (var match in result.Matches.Take(4))
            Assert.True(await market.InScopeAsync(sp => sp.GetRequiredService<ICropAdvisorService>().SaveAsync(farmer, input, match.Crop.Id)));

        var saved = await market.InScopeAsync(sp => sp.GetRequiredService<ICropAdvisorService>().GetSavedAsync(farmer));
        Assert.Equal(CropAdvisorService.MaxSavedPerFarmer, saved.Count);
        Assert.Equal(result.Matches.Skip(1).Take(3).Select(m => m.Crop.Id).Reverse(), saved.Select(s => s.CropCalendarEntryId));  // newest first, oldest dropped
        Assert.All(saved, s => Assert.Equal(("Bogura", "Rabi", "Clay Loam", 50.0), (s.District, s.Season, s.SoilType, s.LandSizeDecimal!.Value)));
        var newest = saved[0];
        Assert.Equal(result.Matches[3].Score, newest.MatchScore);
        Assert.Equal(newest.MatchScore, CropAdvisorService.ReadFactors(newest.FactorsJson).Sum(f => f.Points));
    }

    [KrishiLink.Tests.Infrastructure.PostgresFact]
    public async Task A_crop_that_is_not_in_the_results_cannot_be_saved()
    {
        await using var market = Market();
        await SeedCropsAsync(market);
        var farmer = await market.AddUserAsync(AppRoles.Farmer);
        var input = new CropAdvisoryInput("Bogura", "Rabi", "Clay Loam", 6.5, 50, true);

        var amanId = await market.InScopeAsync(sp => sp.GetRequiredService<ApplicationDbContext>().CropCalendarEntries.Where(c => c.Key == "t-aman-rice").Select(c => c.Id).SingleAsync());
        var saved = await market.InScopeAsync(sp => sp.GetRequiredService<ICropAdvisorService>().SaveAsync(farmer, input, amanId));

        Assert.False(saved);   // Kharif-2 crop, Rabi request: the season gate excludes it, so a forged post is refused
        Assert.Empty(await market.InScopeAsync(sp => sp.GetRequiredService<ICropAdvisorService>().GetSavedAsync(farmer)));
    }
}
