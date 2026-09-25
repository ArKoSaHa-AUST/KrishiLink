using KrishiLink.BLL.Helpers;
using KrishiLink.DAL;

namespace KrishiLink.Tests;

/// <summary>QLT-04: districts and marketplace vocabulary are reviewed seed data, validated before the app starts.</summary>
public class ReferenceDataSeedTests
{
    [Fact]
    public void The_published_geography_has_8_divisions_and_64_districts()
    {
        var geo = ReferenceDataSeed.LoadGeography(CropAdvisorTests.RepoRoot());
        Assert.Equal(8, geo.Divisions.Count);
        Assert.Equal(64, geo.Districts.Count);
        Assert.Equal(64, BangladeshGeo.AllDistricts.Count);
        Assert.All(geo.Districts, d => Assert.True(GeoDistance.IsInBangladesh(d.Lat, d.Lng), d.Name));
    }

    [Theory]
    [InlineData("Bogra", "Bogura", "Rajshahi")]
    [InlineData("Chittagong", "Chattogram", "Chattogram")]
    [InlineData("Chapai Nawabganj", "Chapainawabganj", "Rajshahi")]
    [InlineData("Sylhet", "Sylhet", "Sylhet")]
    public void Older_spellings_resolve_to_one_district(string input, string canonical, string division)
    {
        Assert.Equal(canonical, BangladeshGeo.Canonical(input));
        Assert.Equal(division, BangladeshGeo.GetDivision(input));
        Assert.Equal(GeoLocationHelper.DistrictCoordinates[canonical], GeoLocationHelper.DistrictCoordinates[input]);
    }

    [Fact]
    public void Marketplace_vocabulary_comes_from_the_seed()
    {
        var categories = ReferenceDataSeed.LoadCategories(CropAdvisorTests.RepoRoot());
        Assert.Contains("Combine Harvester", categories.EquipmentCategories);
        Assert.Contains("Cold Storage", categories.StorageTypes);
        Assert.Contains("Rice (Boro)", categories.Crops);
        Assert.Equal(categories.EquipmentCategories, KrishiLink.Models.ViewModels.OnboardingOptions.EquipmentCategories);
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 2, "divisions": [], "districts": [] }""", "schemaVersion")]
    [InlineData("""{ "schemaVersion": 1, "divisions": ["Dhaka"], "districts": [] }""", "divisions")]
    [InlineData("not json", "not valid JSON")]
    public void A_broken_districts_file_stops_startup_with_the_reason(string content, string reason)
    {
        var root = Path.Combine(Path.GetTempPath(), "krishilink-seed-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "App_Data", "seed"));
            File.WriteAllText(Path.Combine(root, ReferenceDataSeed.DistrictsPath), content);
            var ex = Assert.Throws<InvalidOperationException>(() => ReferenceDataSeed.LoadGeography(root));
            Assert.Contains(reason, ex.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_district_claimed_twice_is_rejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "krishilink-seed-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "App_Data", "seed"));
            var original = File.ReadAllText(Path.Combine(CropAdvisorTests.RepoRoot(), ReferenceDataSeed.DistrictsPath));
            File.WriteAllText(Path.Combine(root, ReferenceDataSeed.DistrictsPath), original.Replace("\"aliases\": [\"Jessore\"]", "\"aliases\": [\"Bogra\"]"));
            var ex = Assert.Throws<InvalidOperationException>(() => ReferenceDataSeed.LoadGeography(root));
            Assert.Contains("more than one district", ex.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
