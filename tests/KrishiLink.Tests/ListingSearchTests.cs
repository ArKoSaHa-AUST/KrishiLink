using KrishiLink.BLL.Helpers;
using KrishiLink.BLL.Services;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using KrishiLink.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KrishiLink.Tests;

/// <summary>DIS-01: the query builder only ever emits safe tsquery text, and expands Bangla words to their synonyms.</summary>
public class ListingSearchQueryTests
{
    private static readonly IReadOnlyList<IReadOnlyList<string>> Synonyms = SearchSynonyms.Load(CropAdvisorTests.RepoRoot());

    [Fact]
    public void A_bangla_word_is_searched_through_its_english_synonyms()
    {
        var query = ListingSearch.ToTsQuery("ধান", Synonyms);
        Assert.NotNull(query);
        Assert.Contains("paddy:*", query);
        Assert.Contains("rice:*", query);
    }

    [Theory]
    [InlineData("tractor'); DROP TABLE krishilink.\"Equipment\"; --")]
    [InlineData("a & b | !c <-> d:*")]
    [InlineData("\\ % _")]
    public void User_text_never_reaches_the_tsquery_as_syntax(string input)
    {
        var query = ListingSearch.ToTsQuery(input, Synonyms);
        Assert.True(query is null || System.Text.RegularExpressions.Regex.IsMatch(query, @"\A[a-z0-9:*&|()<> -]+\z"), query);
        Assert.DoesNotContain("'", query ?? string.Empty);
        Assert.DoesNotContain(";", query ?? string.Empty);
    }
}

[Collection(PostgresCollection.Name)]
public class ListingSearchTests
{
    private readonly PostgresDatabase _database;

    public ListingSearchTests(PostgresDatabase database) => _database = database;

    private static async Task<int> AddAsync(Marketplace market, string owner, string name, string category)
    {
        var id = await market.AddEquipmentAsync(owner);
        await market.InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var e = await db.Equipment.SingleAsync(x => x.Id == id);
            (e.Name, e.Category, e.Description) = (name, category, "Available for the season.");
            await db.SaveChangesAsync();
        });
        return id;
    }

    [PostgresFact]
    public async Task A_bangla_search_finds_the_english_titled_listing()
    {
        await using var market = new Marketplace(_database);
        var owner = await market.AddUserAsync(AppRoles.EquipmentOwner);
        var marker = $"Zq{Guid.NewGuid():N}"[..10];
        var paddy = await AddAsync(market, owner, $"{marker} Paddy Thresher", "Thresher");
        await AddAsync(market, owner, $"{marker} Mustard Sprayer", "Power Sprayer");

        var result = await market.InScopeAsync(sp => sp.GetRequiredService<IEquipmentService>().BrowseAsync(new EquipmentSearchCriteria { SearchTerm = "ধান" }));

        Assert.Contains(result.EquipmentList, e => e.Id == paddy);
        Assert.DoesNotContain(result.EquipmentList, e => e.Name.Contains("Mustard") && e.Name.StartsWith(marker));
    }

    [PostgresFact]
    public async Task A_one_letter_typo_still_ranks_the_right_listing_first()
    {
        await using var market = new Marketplace(_database);
        var owner = await market.AddUserAsync(AppRoles.EquipmentOwner);
        var category = $"Typo-{Guid.NewGuid():N}"[..14];
        var harvester = await AddAsync(market, owner, "Kubota Combine Harvester", category);
        await AddAsync(market, owner, "Walking Power Tiller", category);

        var result = await market.InScopeAsync(sp => sp.GetRequiredService<IEquipmentService>().BrowseAsync(
            new EquipmentSearchCriteria { SearchTerm = "harvestor", SelectedCategories = new() { category } }));

        Assert.True(result.IsFuzzyMatch);
        Assert.Equal(harvester, result.EquipmentList.First().Id);
    }
}
