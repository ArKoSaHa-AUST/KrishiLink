using KrishiLink.Controllers;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using KrishiLink.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;

namespace KrishiLink.Tests;

/// <summary>REL-04: stale writes are detected by the xmin token and surface as a retry message, not a 500.</summary>
[Collection(PostgresCollection.Name)]
public class ConcurrencyTests
{
    private readonly PostgresDatabase _database;

    public ConcurrencyTests(PostgresDatabase database) => _database = database;

    public static IEnumerable<object[]> TokenedEntities() => new[]
    {
        typeof(EquipmentBooking), typeof(GodownBooking), typeof(Equipment), typeof(Godown),
        typeof(Payment), typeof(Transaction), typeof(HarvestPlan)
    }.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(TokenedEntities))]
    public void Concurrently_mutated_entities_carry_the_xmin_token(Type entity)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=unused").Options);
        var token = db.Model.FindEntityType(entity)!.FindProperty(ApplicationDbContext.RowVersionProperty);
        Assert.NotNull(token);
        Assert.True(token!.IsConcurrencyToken);
        Assert.Equal("xmin", token.GetColumnName());
    }

    [PostgresFact]
    public async Task A_write_based_on_a_stale_read_is_rejected()
    {
        await using var market = new Marketplace(_database);
        var owner = await market.AddUserAsync(AppRoles.EquipmentOwner);
        var farmer = await market.AddUserAsync(AppRoles.Farmer);
        var equipmentId = await market.AddEquipmentAsync(owner);
        var bookingId = await market.RequestRentalOrThrowAsync(farmer, equipmentId, DateTime.Today.AddDays(4), DateTime.Today.AddDays(5));

        await using var first = _database.CreateContext();
        await using var second = _database.CreateContext();
        var a = await first.EquipmentBookings.SingleAsync(b => b.Id == bookingId);
        var b = await second.EquipmentBookings.SingleAsync(x => x.Id == bookingId);

        a.Status = BookingStatus.Accepted;
        await first.SaveChangesAsync();

        b.Status = BookingStatus.Rejected;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        Assert.Equal(BookingStatus.Accepted, (await market.GetBookingAsync(bookingId)).Status);
    }

    [Fact]
    public void Conflict_on_a_form_post_redirects_back_with_a_message()
    {
        using var _ = new InvariantCultureScope();
        var (filter, context, tempData) = FilterContext(ajax: false);

        filter.OnException(context);

        Assert.True(context.ExceptionHandled);
        Assert.Equal("/Bookings?tab=equipment", Assert.IsType<LocalRedirectResult>(context.Result).Url);
        Assert.Equal("This was just updated by someone else. Please review the latest details and try again.", tempData["ErrorMessage"]);
    }

    [Fact]
    public void Conflict_on_an_ajax_call_returns_409_problem_details()
    {
        var (filter, context, _) = FilterContext(ajax: true);

        filter.OnException(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.IsType<ProblemDetails>(result.Value);
    }

    [Fact]
    public void Other_exceptions_are_left_alone()
    {
        var (filter, context, _) = FilterContext(ajax: false, new InvalidOperationException());
        filter.OnException(context);
        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
    }

    private static (ConcurrencyConflictFilter, ExceptionContext, ITempDataDictionary) FilterContext(bool ajax, Exception? exception = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(o => o.ResourcesPath = "Resources");
        var provider = services.BuildServiceProvider();

        var http = new DefaultHttpContext { RequestServices = provider };
        http.Request.Host = new HostString("krishilink.test");
        http.Request.Headers.Referer = "https://krishilink.test/Bookings?tab=equipment";
        if (ajax) http.Request.Headers.XRequestedWith = "XMLHttpRequest";

        var tempData = new TempDataDictionary(http, new NullTempDataProvider());
        var factory = new FixedTempDataFactory(tempData);
        var filter = new ConcurrencyConflictFilter(provider.GetRequiredService<IStringLocalizer<SharedResource>>(), factory,
            NullLogger<ConcurrencyConflictFilter>.Instance);
        var context = new ExceptionContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), new List<IFilterMetadata>())
        {
            Exception = exception ?? new DbUpdateConcurrencyException("stale")
        };
        return (filter, context, tempData);
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class FixedTempDataFactory(ITempDataDictionary tempData) : ITempDataDictionaryFactory
    {
        public ITempDataDictionary GetTempData(HttpContext context) => tempData;
    }
}
