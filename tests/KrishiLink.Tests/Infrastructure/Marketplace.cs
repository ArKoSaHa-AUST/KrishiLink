using KrishiLink.BLL.Services;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KrishiLink.Tests.Infrastructure;

/// <summary>Drives the platform through its real services, one DI scope per action — the way requests do.</summary>
public sealed class Marketplace : IAsyncDisposable
{
    public const string GoodWallet = "01712345678";
    public const string RejectedWallet = "01700000000";

    private readonly ServiceProvider _services;

    public Marketplace(PostgresDatabase database, Action<IServiceCollection>? configure = null)
    {
        _services = TestServices.Build(database.ConnectionString, configure);
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = _services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public Task InScopeAsync(Func<IServiceProvider, Task> action) =>
        InScopeAsync<bool>(async sp => { await action(sp); return true; });

    public Task<string> AddUserAsync(string role) => InScopeAsync(async sp =>
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var id = Guid.NewGuid().ToString();
        var email = $"{role.ToLowerInvariant()}-{id[..8]}@example.test";
        db.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            FullName = $"{role} {id[..4]}",
            UserRole = role,
            SecurityStamp = Guid.NewGuid().ToString()
        });
        await db.SaveChangesAsync();
        return id;
    });

    public Task<int> AddEquipmentAsync(string ownerId, decimal dailyRate = 1000m, int quantity = 1, int minRentalDays = 1) => InScopeAsync(async sp =>
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var equipment = new Equipment
        {
            OwnerId = ownerId,
            Name = "Power tiller",
            Category = "Tiller",
            Location = "Rangpur",
            District = "Rangpur",
            DailyRate = dailyRate,
            Quantity = quantity,
            MinRentalDays = minRentalDays,
            IsAvailable = true
        };
        db.Equipment.Add(equipment);
        await db.SaveChangesAsync();
        return equipment.Id;
    });

    public Task BlockEquipmentDateAsync(int equipmentId, DateTime date) => InScopeAsync(async sp =>
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        db.EquipmentBlockedDates.Add(new EquipmentBlockedDate { EquipmentId = equipmentId, Date = date.Date, Reason = "Maintenance" });
        await db.SaveChangesAsync();
    });

    public Task<(string? Error, int? BookingId)> RequestRentalAsync(string farmerId, int equipmentId, DateTime start, DateTime end, int units = 1) =>
        InScopeAsync(sp => sp.GetRequiredService<IEquipmentService>().RequestRentalWithResultAsync(farmerId, equipmentId, start, end, null, units));

    public async Task<int> RequestRentalOrThrowAsync(string farmerId, int equipmentId, DateTime start, DateTime end, int units = 1)
    {
        var (error, id) = await RequestRentalAsync(farmerId, equipmentId, start, end, units);
        Assert.Null(error);
        return id!.Value;
    }

    public Task<DecisionResult> RespondAsync(string ownerId, int bookingId, string decision) =>
        InScopeAsync(sp => sp.GetRequiredService<IEquipmentService>().RespondAsync(ownerId, bookingId, decision, null));

    /// <summary>Checkout + gateway callback. Returns the failure reason, or null when the payment succeeded.</summary>
    public async Task<string?> PayAsync(string farmerId, int bookingId, string wallet = GoodWallet)
    {
        var (error, redirect) = await InScopeAsync(sp =>
            sp.GetRequiredService<IPaymentService>().InitiateAsync(farmerId, "Equipment", bookingId, PaymentMethods.BKash, wallet));
        if (error is not null) return error;

        var gatewayReference = Uri.UnescapeDataString(redirect!.Split("ref=")[1]);
        var (failure, _) = await InScopeAsync(sp =>
            sp.GetRequiredService<IPaymentService>().CompleteAsync(gatewayReference, SimulatedPaymentGateway.OutcomeSuccess));
        return failure;
    }

    public Task<(string? Error, decimal? Refunded)> CancelAsync(string farmerId, int bookingId) =>
        InScopeAsync(sp => sp.GetRequiredService<IBookingService>().CancelAsync(farmerId, "Equipment", bookingId));

    public Task<(string? Error, bool NeedsReapproval)> ModifyAsync(string farmerId, int bookingId, DateTime start, DateTime end, int? units = null) =>
        InScopeAsync(sp => sp.GetRequiredService<IBookingService>().ModifyAsync(farmerId, "Equipment", bookingId, start, end, units, null));

    public Task<ReviewSubmissionResult> ReviewAsync(string farmerId, int bookingId) =>
        InScopeAsync(sp => sp.GetRequiredService<IReviewService>().SubmitReviewAsync(farmerId,
            new SubmitReviewViewModel { BookingType = "Equipment", BookingId = bookingId, Rating = 5, Comment = "Good" }));

    public Task<string?> RequestPayoutAsync(string ownerId, string wallet = GoodWallet) =>
        InScopeAsync(sp => sp.GetRequiredService<IEquipmentRevenueService>().RequestPayoutAsync(ownerId, "bKash", wallet));

    public Task<int> SettlePayoutsAsync(string ownerId) =>
        InScopeAsync(sp => sp.GetRequiredService<IPayoutSettlementService>().SettleDuePayoutsAsync(ignoreDelay: true, ownerId: ownerId));

    public Task<ConservationCheck> CheckConservationAsync() =>
        InScopeAsync(sp => Task.FromResult(sp.GetRequiredService<ILedgerService>().CheckConservation()));

    /// <summary>Conservation identity plus reconciliation of every ledger total against the domain tables.</summary>
    public async Task AssertLedgerIsSoundAsync(string context)
    {
        var check = await CheckConservationAsync();
        Assert.True(check.Ok, $"{context}: conservation identity failed — {check.Detail}");
        var problems = await InScopeAsync(LedgerAudit.FindDiscrepanciesAsync);
        Assert.True(problems.Count == 0, $"{context}: {string.Join("; ", problems)}");
    }

    public Task<EquipmentBooking> GetBookingAsync(int bookingId) => InScopeAsync(sp =>
        sp.GetRequiredService<ApplicationDbContext>().EquipmentBookings.AsNoTracking()
            .Include(b => b.Payment)
            .SingleAsync(b => b.Id == bookingId));

    public Task<Dictionary<int, EquipmentBooking>> GetBookingsAsync(IReadOnlyCollection<int> bookingIds) => InScopeAsync(sp =>
        sp.GetRequiredService<ApplicationDbContext>().EquipmentBookings.AsNoTracking()
            .Include(b => b.Payment)
            .Where(b => bookingIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id));

    public Task<int> FreeUnitsAsync(int equipmentId, DateTime start, DateTime end, int? excludeBookingId = null) =>
        InScopeAsync(sp => sp.GetRequiredService<IEquipmentService>().FreeUnitsAsync(equipmentId, start, end, excludeBookingId));

    public Task<string?> CheckAvailabilityAsync(int equipmentId, DateTime start, DateTime end, int units = 1, int? excludeBookingId = null) =>
        InScopeAsync(sp => sp.GetRequiredService<IEquipmentService>().CheckAvailabilityAsync(equipmentId, start, end, units, excludeBookingId));
}
