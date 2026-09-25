using KrishiLink.BLL.Helpers;
using KrishiLink.Controllers;

namespace KrishiLink.Tests;

public class GeoAndNormalizationTests
{
    [Fact]
    public void There_are_exactly_64_canonical_districts()
    {
        Assert.Equal(64, BangladeshGeo.AllDistricts.Count);
        Assert.Equal(64, BangladeshGeo.AllDistricts.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Every_district_maps_to_exactly_one_of_the_eight_divisions()
    {
        Assert.Equal(8, BangladeshGeo.Divisions.Count);

        var memberships = BangladeshGeo.AllDistricts.ToDictionary(
            d => d,
            d => BangladeshGeo.DistrictsByDivision.Where(p => p.Value.Contains(d)).Select(p => p.Key).ToList());

        foreach (var (district, divisions) in memberships)
        {
            var division = Assert.Single(divisions);
            Assert.Equal(division, BangladeshGeo.GetDivision(district));
            Assert.Contains(division, BangladeshGeo.Divisions);
        }

        Assert.Equal(64, BangladeshGeo.DistrictsByDivision.Values.Sum(d => d.Count));
    }

    [Theory]
    [InlineData("Dhaka", 13)]
    [InlineData("Chattogram", 11)]
    [InlineData("Rajshahi", 8)]
    [InlineData("Khulna", 10)]
    [InlineData("Barishal", 6)]
    [InlineData("Sylhet", 4)]
    [InlineData("Rangpur", 8)]
    [InlineData("Mymensingh", 4)]
    public void Division_district_counts_match_the_official_breakdown(string division, int count)
    {
        Assert.Equal(count, BangladeshGeo.DistrictsByDivision[division].Count);
    }

    [Theory]
    [InlineData("Bogra", "Bogura")]
    [InlineData("Chittagong", "Chattogram")]
    [InlineData("Comilla", "Cumilla")]
    [InlineData("Jessore", "Jashore")]
    [InlineData("Barisal", "Barishal")]
    [InlineData("Maulvibazar", "Moulvibazar")]
    [InlineData("Nawabganj", "Chapainawabganj")]
    public void Legacy_spellings_resolve_to_the_canonical_district_in_the_same_division(string legacy, string canonical)
    {
        Assert.Equal(canonical, BangladeshGeo.Canonical(legacy));
        Assert.Equal(BangladeshGeo.GetDivision(canonical), BangladeshGeo.GetDivision(legacy));
        Assert.DoesNotContain(legacy, BangladeshGeo.AllDistricts);
    }

    [Theory]
    [InlineData("+8801712345678")]
    [InlineData("8801712345678")]
    [InlineData("01712345678")]
    [InlineData("1712345678")]
    [InlineData("  +8801712345678  ")]
    public void Phone_numbers_normalize_to_one_canonical_local_form(string input)
    {
        Assert.Equal("01712345678", AccountController.NormalizePhone(input));
    }

    [Fact]
    public void Normalized_phone_is_idempotent()
    {
        var once = AccountController.NormalizePhone("+8801912345678");
        Assert.Equal(once, AccountController.NormalizePhone(once));
    }
}
