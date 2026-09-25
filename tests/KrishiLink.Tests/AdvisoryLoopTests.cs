using System.Reflection;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;

namespace KrishiLink.Tests;

/// <summary>ADV-04 (advice → marketplace) and ADV-08 (urgency and state for weather nudges).</summary>
public class AdvisoryLoopTests
{
    private static WeatherSuggestionItem Nudge(string id, string type, bool critical = false, int priority = 1) =>
        new() { Id = id, NudgeType = type, IsCritical = critical, Priority = priority };

    // ---------------------------------------------------------------- ADV-08 urgency

    [Fact]
    public void Harvest_before_rain_outranks_a_general_tip()
    {
        var ranked = SuggestionUrgency.Rank(new[]
        {
            Nudge("tillage-seeding", "TillageIrrigation"),
            Nudge("harvester-urgent", "FastHarvesting", critical: true, priority: 2),
            Nudge("crop-guide", "CropGuide")
        }, cropStage: "Harvesting", rainAlert: true, rainWindowDays: 2, "Bogura", "Boro Rice", new DateTime(2026, 4, 20));

        Assert.Equal("harvester-urgent", ranked[0].Id);
        Assert.Equal(2, ranked[0].DaysUntilWeatherEvent);
        Assert.Null(ranked.Single(n => n.Id == "tillage-seeding").DaysUntilWeatherEvent);
    }

    [Fact]
    public void Rain_sooner_is_more_urgent_than_rain_later()
    {
        var item = Nudge("storage-urgent", "StorageProtection", critical: true);
        var tomorrow = SuggestionUrgency.Score(item, "Harvesting", rainAlert: true, rainWindowDays: 1);
        var inFourDays = SuggestionUrgency.Score(item, "Harvesting", rainAlert: true, rainWindowDays: 4);
        var noRain = SuggestionUrgency.Score(item, "Harvesting", rainAlert: false, rainWindowDays: 1);

        Assert.True(tomorrow > inFourDays);
        Assert.True(inFourDays > noRain);
    }

    [Fact]
    public void Ranking_is_deterministic_and_sets_month_scoped_state_keys()
    {
        var items = new[] { Nudge("a-tip", "CropGuide"), Nudge("b-tip", "CropGuide") };
        var first = SuggestionUrgency.Rank(items, "Growing", false, 1, "Sylhet", "Boro Rice", new DateTime(2026, 9, 25));
        var second = SuggestionUrgency.Rank(items.Reverse(), "Growing", false, 1, "Sylhet", "Boro Rice", new DateTime(2026, 9, 25));

        Assert.Equal(first.Select(i => i.Id), second.Select(i => i.Id));
        Assert.Equal("a-tip|Sylhet|Boro Rice|2026-09", first[0].StateKey);
    }

    [Theory]
    [InlineData("storage-urgent|Bogura|Boro Rice|2026-09", true)]
    [InlineData("storage-urgent|Bogura|Boro Rice", false)]
    [InlineData("<script>|Bogura|Boro Rice|2026-09", false)]
    [InlineData("", false)]
    public async Task Only_well_formed_state_keys_are_accepted(string key, bool accepted)
    {
        // A malformed key is refused before the database is touched, so no context is needed for the negative cases.
        if (accepted) return;
        var service = new SuggestionStateService(null!);
        Assert.False(await service.SetAsync("user-1", key, SuggestionStateKind.Done));
    }

    // ---------------------------------------------------------------- ADV-04 links

    [Fact]
    public void Marketplace_links_use_the_names_the_list_pages_bind()
    {
        var url = AppLinks.EquipmentSearch("Bogura", "Combine Harvester", new DateTime(2026, 4, 1), new DateTime(2026, 4, 7));
        Assert.Equal("/Equipment?District=Bogura&SelectedCategories=Combine%20Harvester&StartDate=2026-04-01&EndDate=2026-04-07", url);

        var criteria = typeof(EquipmentSearchCriteria).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToHashSet();
        Assert.Contains("District", criteria);
        Assert.Contains("SelectedCategories", criteria);
        Assert.Contains("StartDate", criteria);
        Assert.Contains("EndDate", criteria);

        var godown = AppLinks.GodownSearch("Munshiganj", "Cold Storage", new DateTime(2026, 3, 8), new DateTime(2026, 5, 6));
        Assert.Equal("/Godown?District=Munshiganj&SelectedStorageTypes=Cold%20Storage&AvailableStartDate=2026-03-08&AvailableEndDate=2026-05-06", godown);
        var godownCriteria = typeof(GodownSearchCriteria).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Superset(new HashSet<string> { "District", "SelectedStorageTypes", "AvailableStartDate", "AvailableEndDate" }, godownCriteria);

        Assert.Equal("/Equipment", AppLinks.EquipmentSearch());
    }

    // ---------------------------------------------------------------- ADV-05 pest rules as reviewed data

    private sealed class RepoEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "KrishiLink";
        public string ContentRootPath { get; set; } = CropAdvisorTests.RepoRoot();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    [Fact]
    public void Every_pest_rule_is_attributed_and_fully_bilingual()
    {
        var rules = KrishiLink.DAL.PestRuleSeed.Load(CropAdvisorTests.RepoRoot());

        Assert.True(rules.Count >= 9);
        Assert.All(rules, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Source), $"rule {r.Id} has no source");
            Assert.False(string.IsNullOrWhiteSpace(r.BanglaName));
            Assert.False(string.IsNullOrWhiteSpace(r.BanglaSymptoms));
            Assert.False(string.IsNullOrWhiteSpace(r.BanglaTriggerReason));
            Assert.False(string.IsNullOrWhiteSpace(r.PreventiveSprayBn));
            Assert.False(string.IsNullOrWhiteSpace(r.OrganicControlBn));
            Assert.Equal(r.ActionableRemedies.Count, r.BanglaActionableRemedies.Count);
        });
        Assert.DoesNotContain(rules, r => r.OrganicControl.Contains("100%", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_json_rules_still_fire_on_the_weather_they_describe()
    {
        var service = new PestAlertService(null!, new RepoEnvironment());
        var blastWeather = new RegionalWeatherForecast { District = "Bogura", Temperature = 25, MinTemp = 22, MaxTemp = 28, Humidity = 92, RainProbability = 70, Condition = "Cloudy with Showers" };
        var dryWeather = new RegionalWeatherForecast { District = "Bogura", Temperature = 38, MinTemp = 30, MaxTemp = 40, Humidity = 30, RainProbability = 0, Condition = "Clear & Dry Skies" };

        var alerts = await service.EvaluateAlertsAsync(blastWeather, "Rice");
        var blast = Assert.Single(alerts, a => a.RuleId == 1);
        Assert.Equal("BRRI", blast.Source);
        Assert.False(string.IsNullOrWhiteSpace(blast.PreventiveSprayBn));

        Assert.DoesNotContain(await service.EvaluateAlertsAsync(dryWeather, "Rice"), a => a.RuleId == 1);
    }

    [Fact]
    public void A_potato_crop_needs_a_tiller_before_sowing_and_cold_storage_after_harvest()
    {
        var potato = new CropCalendarEntry { Key = "potato", Category = "Tubers", SowingMonths = { 11, 12 }, HarvestingMonths = { 2, 3 } };
        var needs = CropNeeds.For(potato, new DateTime(2026, 9, 25));

        var tiller = Assert.Single(needs, n => n.Stage == CropNeedStage.LandPreparation);
        Assert.Equal(("Power Tiller", new DateTime(2026, 11, 1)), (tiller.Category, tiller.From));
        Assert.DoesNotContain(needs, n => n.Stage == CropNeedStage.Harvest);   // dug by hand: no machine invented
        var storage = Assert.Single(needs, n => n.Stage == CropNeedStage.PostHarvestStorage);
        Assert.True(storage.IsStorage);
        Assert.Equal("Cold Storage", storage.Category);
        Assert.Equal(new DateTime(2027, 2, 8), storage.From);
        Assert.StartsWith("/Godown?District=Munshiganj&SelectedStorageTypes=Cold%20Storage", storage.SearchUrl("Munshiganj"));
    }

    [Fact]
    public void Boro_rice_needs_a_combine_at_harvest_and_the_window_starts_today_inside_the_month()
    {
        var boro = new CropCalendarEntry { Key = "boro-rice", Category = "Cereals", SowingMonths = { 11, 12, 1 }, HarvestingMonths = { 4, 5 } };
        var needs = CropNeeds.For(boro, new DateTime(2026, 4, 14));

        var harvest = Assert.Single(needs, n => n.Stage == CropNeedStage.Harvest);
        Assert.Equal(("Combine Harvester", new DateTime(2026, 4, 14), new DateTime(2026, 4, 20)), (harvest.Category, harvest.From, harvest.To));
        Assert.Equal("Grain Warehouse", needs.Single(n => n.IsStorage).Category);
    }
}
