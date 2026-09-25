using System.Text.RegularExpressions;
using KrishiLink.BLL.Services;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using KrishiLink.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace KrishiLink.Tests;

/// <summary>REL-03: the payout/revenue path does no blocking database I/O, and nothing blocks on a task.</summary>
public class AsyncSurfaceTests
{
    [Theory]
    [InlineData(typeof(IOwnerRevenueService))]
    [InlineData(typeof(IOwnerRevenueRepository))]
    public void Revenue_data_access_is_task_returning(Type contract)
    {
        var synchronous = contract.GetMethods().Where(m => !typeof(Task).IsAssignableFrom(m.ReturnType)).Select(m => m.Name).ToList();
        Assert.Empty(synchronous);
    }

    [Fact]
    public void There_is_no_synchronous_payout_overload()
    {
        Assert.Null(typeof(IOwnerRevenueService).GetMethod("RequestPayout"));
        Assert.Null(typeof(OwnerRevenueService).GetMethod("RequestPayout"));
    }

    [Fact]
    public void Nothing_outside_Program_blocks_on_a_task()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "KrishiLink.csproj"))) root = root.Parent!;

        var offenders = Directory.EnumerateFiles(root.FullName, "*.cs", SearchOption.AllDirectories)
            .Where(p => !Regex.IsMatch(p, @"[\\/](bin|obj|tests|\.kilo|\.git)[\\/]") && Path.GetFileName(p) != "Program.cs")
            .SelectMany(p => File.ReadLines(p).Select((line, i) => (p, line, i)))
            .Where(x => Regex.IsMatch(x.line, @"\.GetAwaiter\(\)\.GetResult\(\)|\.Wait\(\)|\.WaitAll\(|Task\.WaitAny\(|(?<!context)\.Result\s*[;,)]"))
            .Select(x => $"{Path.GetRelativePath(root.FullName, x.p)}:{x.i + 1}")
            .ToList();
        Assert.Empty(offenders);
    }
}

[Collection(PostgresCollection.Name)]
public class OwnerRevenueAsyncTests
{
    private readonly PostgresDatabase _database;

    public OwnerRevenueAsyncTests(PostgresDatabase database) => _database = database;

    [PostgresFact]
    public async Task Report_payout_history_and_expenses_work_through_the_async_path()
    {
        await using var market = new Marketplace(_database);
        var owner = await market.AddUserAsync(AppRoles.EquipmentOwner);
        var farmer = await market.AddUserAsync(AppRoles.Farmer);
        var equipmentId = await market.AddEquipmentAsync(owner, dailyRate: 2000m);
        var id = await market.RequestRentalOrThrowAsync(farmer, equipmentId, DateTime.Today.AddDays(2), DateTime.Today.AddDays(3));
        Assert.True((await market.RespondAsync(owner, id, "accept")).Success);
        Assert.Null(await market.PayAsync(farmer, id));
        Assert.True((await market.RespondAsync(owner, id, "complete")).Success);

        await market.InScopeAsync(async sp =>
        {
            var revenue = sp.GetRequiredService<IEquipmentRevenueService>();

            var history = await revenue.GetPayoutHistoryAsync(owner);
            Assert.Equal(1, history.CompletedBookings);
            Assert.Equal(4000m - 200m, history.Settlement.Owed);

            Assert.Null(await revenue.SaveExpenseAsync(owner, null, id, null, 350m, ExpenseCategories.Fuel, "Diesel", DateTime.Today));
            var report = await revenue.GetReportAsync(owner, new RevenueFilter { From = DateTime.Today.AddMonths(-1), To = DateTime.Today.AddMonths(1) });
            var expense = Assert.Single(report.Transactions.Single(t => t.BookingId == id).ExpenseLines);
            Assert.Equal(350m, expense.Amount);

            Assert.True(await revenue.DeleteExpenseAsync(owner, expense.Id));
            Assert.False(await revenue.DeleteExpenseAsync(owner, expense.Id));
            Assert.NotNull(await revenue.GetInvoiceAsync(owner, id));
        });
    }
}
