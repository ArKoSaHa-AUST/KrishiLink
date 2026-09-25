using KrishiLink.BLL.Helpers;
using KrishiLink.BLL.Services;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using KrishiLink.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KrishiLink.Tests;

/// <summary>DIS-02: "within N km" is a real great-circle distance, computed in SQL.</summary>
public class GeoDistanceTests
{
    [Fact]
    public void Dhaka_to_Bogura_is_about_160_km() =>
        Assert.InRange(GeoDistance.Km(23.8103, 90.4125, 24.8465, 89.3777), 155, 165);

    [Fact]
    public void The_bounding_box_encloses_the_circle()
    {
        var circle = new GeoCircle(24.8465, 89.3777, 30);
        var (minLat, maxLat, minLng, maxLng) = circle.BoundingBox();
        Assert.InRange(GeoDistance.Km(circle.Lat, circle.Lng, maxLat, circle.Lng), 29.9, 30.1);
        Assert.InRange(GeoDistance.Km(circle.Lat, circle.Lng, circle.Lat, maxLng), 29.9, 30.1);
        Assert.True(minLat < circle.Lat && minLng < circle.Lng);
    }

    [Fact]
    public void The_origin_is_the_users_position_when_plausible_else_the_district_centre()
    {
        Assert.Equal(new GeoCircle(24.123, 89.456, 25), GeoDistance.Circle(24.12345, 89.45611, "Dhaka", 25));
        var centre = GeoDistance.Circle(null, null, "Bogura", 25)!.Value;
        Assert.Equal((24.8465, 89.3777), (centre.Lat, centre.Lng));
        Assert.Equal(centre, GeoDistance.Circle(null, null, "Bogra", 25));             // either spelling
        Assert.Equal(centre, GeoDistance.Circle(51.5, -0.12, "Bogura", 25));           // London is not a plausible origin
        Assert.Null(GeoDistance.Circle(null, null, null, 25));
        Assert.Null(GeoDistance.Circle(24.1, 89.4, "Bogura", null));
        Assert.Equal(GeoDistance.MaxRadiusKm, GeoDistance.Circle(null, null, "Bogura", 5000)!.Value.RadiusKm);
    }
}

[Collection(PostgresCollection.Name)]
public class ProximitySearchTests
{
    private readonly PostgresDatabase _database;

    public ProximitySearchTests(PostgresDatabase database) => _database = database;

    [PostgresFact]
    public async Task Within_radius_filters_and_sorts_by_true_distance_in_the_database()
    {
        await using var market = new Marketplace(_database);
        var owner = await market.AddUserAsync(AppRoles.EquipmentOwner);
        var category = $"Geo-{Guid.NewGuid():N}"[..16];

        async Task<int> AddAtAsync(string district, double lat, double lng)
        {
            var id = await market.AddEquipmentAsync(owner);
            await market.InScopeAsync(async sp =>
            {
                var db = sp.GetRequiredService<ApplicationDbContext>();
                var e = await db.Equipment.SingleAsync(x => x.Id == id);
                (e.Category, e.District, e.Location, e.Latitude, e.Longitude) = (category, district, district, lat, lng);
                await db.SaveChangesAsync();
            });
            return id;
        }

        var boguraTown = await AddAtAsync("Bogura", 24.8465, 89.3777);   // the district centre
        var joypurhat = await AddAtAsync("Joypurhat", 25.0968, 89.0227);  // ~45 km away, another district
        var sherpur = await AddAtAsync("Bogura", 24.6600, 89.4200);       // ~21 km south, same district
        var dhaka = await AddAtAsync("Dhaka", 23.8103, 90.4125);          // ~160 km
        await market.AddEquipmentAsync(owner);                            // no coordinates at all

        Task<EquipmentBrowseViewModel> BrowseAsync(EquipmentSearchCriteria c) =>
            market.InScopeAsync(sp => sp.GetRequiredService<IEquipmentService>().BrowseAsync(c));

        var within30 = await BrowseAsync(new() { SelectedCategories = new() { category }, District = "Bogura", RadiusKm = 30, SortBy = "distance" });
        Assert.True(within30.IsNearSearch);
        Assert.Equal(new[] { boguraTown, sherpur }, within30.EquipmentList.Select(e => e.Id));
        Assert.InRange(within30.EquipmentList[1].DistanceKm, 19, 23);

        // 60 km crosses into Joypurhat: the radius replaces the district match.
        var within60 = await BrowseAsync(new() { SelectedCategories = new() { category }, District = "Bogura", RadiusKm = 60, SortBy = "distance" });
        Assert.Equal(new[] { boguraTown, sherpur, joypurhat }, within60.EquipmentList.Select(e => e.Id));

        // The user's own position wins over the district, and "nearest first" follows it.
        var nearDhaka = await BrowseAsync(new() { SelectedCategories = new() { category }, District = "Bogura", RadiusKm = 300, NearLat = 23.81, NearLng = 90.41, SortBy = "distance" });
        Assert.Equal(dhaka, nearDhaka.EquipmentList.First().Id);
        Assert.DoesNotContain(nearDhaka.EquipmentList, e => e.DistanceKm == 0 && e.Id != dhaka);

        // Without a radius nothing changes: the district match still applies.
        var plain = await BrowseAsync(new() { SelectedCategories = new() { category }, District = "Bogura" });
        Assert.False(plain.IsNearSearch);
        Assert.Equal(new[] { boguraTown, sherpur }.OrderBy(i => i), plain.EquipmentList.Select(e => e.Id).OrderBy(i => i));
    }

    [PostgresFact]
    public async Task A_page_size_request_is_capped()
    {
        await using var market = new Marketplace(_database);
        var model = await market.InScopeAsync(sp => sp.GetRequiredService<IEquipmentService>().BrowseAsync(new EquipmentSearchCriteria { PageSize = 100_000 }));
        Assert.Equal(ListingSearch.MaxPageSize, model.PageSize);
    }

    [PostgresFact]
    public async Task Godown_search_uses_the_same_radius()
    {
        await using var market = new Marketplace(_database);
        var owner = await market.AddUserAsync(AppRoles.GodownOwner);
        var type = $"Geo-{Guid.NewGuid():N}"[..16];
        var ids = await market.InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var near = new Godown { OwnerId = owner, Name = "Near store", StorageType = type, District = "Bogura", Location = "Bogura", Latitude = 24.85, Longitude = 89.38, CapacityInTons = 100, PricePerTonPerMonth = 400 };
            var far = new Godown { OwnerId = owner, Name = "Far store", StorageType = type, District = "Khulna", Location = "Khulna", Latitude = 22.8456, Longitude = 89.5403, CapacityInTons = 100, PricePerTonPerMonth = 400 };
            db.Godowns.AddRange(near, far);
            await db.SaveChangesAsync();
            return (near.Id, far.Id);
        });

        var model = await market.InScopeAsync(sp => sp.GetRequiredService<IGodownService>().BrowseAsync(
            new GodownSearchCriteria { SelectedStorageTypes = new() { type }, District = "Bogura", RadiusKm = 50, SortBy = "distance" }));
        Assert.Equal(new[] { ids.Item1 }, model.GodownList.Select(g => g.Id));
    }
}
