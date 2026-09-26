using KrishiLink.BLL.Services;
using KrishiLink.DAL;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KrishiLink.Tests.Infrastructure;

/// <summary>
/// The production business-layer registrations from Program.cs, pointed at the disposable test database.
/// Only the true external boundaries (Supabase Storage, the SMTP queue) are replaced.
/// </summary>
public static class TestServices
{
    public static ServiceProvider Build(string connectionString, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString,
                postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", DatabaseConfiguration.Schema)));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddClaimsPrincipalFactory<KrishiLinkClaimsPrincipalFactory>();

        services.Configure<RevenueOptions>(_ => { });
        services.Configure<PricingOptions>(_ => { });
        services.Configure<AppOptions>(o => o.PublicBaseUrl = "https://krishilink.test");
        services.Configure<PaymentsOptions>(o => o.SettlementDelay = TimeSpan.Zero);

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IGodownRevenueRepository, GodownRevenueRepository>();
        services.AddScoped<IEquipmentRevenueRepository, EquipmentRevenueRepository>();
        services.AddScoped<ILedgerRepository, LedgerRepository>();

        services.AddScoped<IFavoriteService, FavoriteService>();
        services.AddScoped<IEquipmentService, EquipmentService>();
        services.AddScoped<IGodownService, GodownService>();
        services.AddScoped<IStorageIntakeService, StorageIntakeService>();
        services.AddScoped<IReceiptDocumentService, ReceiptDocumentService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IFarmerProfileService, FarmerProfileService>();
        services.AddScoped<IGodownRevenueService, GodownRevenueService>();
        services.AddScoped<IEquipmentRevenueService, EquipmentRevenueService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddSingleton<IQrCodeService, QrCodeService>();
        services.AddScoped<IBadgeService, BadgeService>();
        services.AddScoped<ILeaderboardService, LeaderboardService>();
        services.AddScoped<ILoyaltyService, LoyaltyService>();
        services.AddScoped<IPayoutSettlementService, PayoutSettlementService>();
        services.AddScoped<IPaymentGateway, SimulatedPaymentGateway>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ILedgerService, LedgerService>();
        services.AddScoped<IEquipmentQueries>(sp => sp.GetRequiredService<IEquipmentService>());
        services.AddScoped<IGodownQueries>(sp => sp.GetRequiredService<IGodownService>());
        services.AddScoped<IBookingQueries>(sp => sp.GetRequiredService<IBookingService>());

        services.AddSingleton<IEmailQueue, DiscardingEmailQueue>();
        services.AddSingleton<IFileStorageService, UnavailableFileStorage>();
        services.AddSingleton<IRealtimeUpdateService, RealtimeUpdateService>();

        configure?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed class DiscardingEmailQueue : IEmailQueue
    {
        public ValueTask EnqueueAsync(EmailJob job, CancellationToken ct = default) => ValueTask.CompletedTask;
    }

    private sealed class UnavailableFileStorage : IFileStorageService
    {
        public List<string> ValidateFiles(IEnumerable<IFormFile>? files) => new();
        public Task<List<string>> SaveImagesAsync(IEnumerable<IFormFile>? files, string folder) => Task.FromResult(new List<string>());
        public Task<List<string>> SavePrivateFilesAsync(IEnumerable<IFormFile>? files, string folder) => Task.FromResult(new List<string>());
        public Task DeleteFilesAsync(IEnumerable<string>? urls, string folder) => Task.CompletedTask;
        public Task DeletePrivateFilesAsync(IEnumerable<string>? paths, string ownerId) => Task.CompletedTask;
        public Task<StoredPrivateFile?> ReadPrivateFileAsync(string? path, string ownerId, CancellationToken cancellationToken = default) => Task.FromResult<StoredPrivateFile?>(null);
        public Task InitializeBucketsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
