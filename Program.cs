using KrishiLink.BLL.Services;
using KrishiLink.DAL;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Net.Sockets;

var builder = WebApplication.CreateBuilder(args);
EnvironmentConfiguration.AddLocalEnvironmentFiles(builder.Configuration, builder.Environment.ContentRootPath, args);

// QuestPDF community licence (free for organisations under USD 1M annual revenue)
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Binding 0.0.0.0 lets phones and tablets on the same Wi-Fi reach the site by LAN IP.
// HTTPS stays on localhost: a development certificate is not trusted by other devices.
// Development only, so a hosting platform's own port binding is never overridden in production.
var listenUrls = builder.Configuration["App:ListenUrls"];
if (builder.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(listenUrls))
{
    builder.WebHost.UseUrls(listenUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

var connectionString = DatabaseConfiguration.GetConnectionString(builder.Configuration);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString,
        postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", DatabaseConfiguration.Schema)));

// Add Identity role-based services
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});
builder.Services.AddSupabaseAuthentication(builder.Configuration);

// The sign-in cookie is Secure-only, which is right in production. A development LAN is served over
// plain HTTP so phones can reach it by IP, and a Secure cookie would never be sent back on those
// requests, making sign-in silently impossible. Relax it for Development only.
if (builder.Environment.IsDevelopment())
{
    builder.Services.ConfigureApplicationCookie(options =>
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest);
}

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

// Localization: shared .resx resources (English is the default/fallback, Bangla via SharedResource.bn.resx)
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { new CultureInfo("en"), new CultureInfo("bn") };
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    // Culture is chosen via cookie set by HomeController.SetLanguage (no JavaScript involved)
    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new CookieRequestCultureProvider()
    };
});

// Data access: generic EF repositories + revenue reporting repositories + money ledger
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IGodownRevenueRepository, GodownRevenueRepository>();
builder.Services.AddScoped<IEquipmentRevenueRepository, EquipmentRevenueRepository>();
builder.Services.AddScoped<ILedgerRepository, LedgerRepository>();

// Business logic
builder.Services.Configure<RevenueOptions>(builder.Configuration.GetSection(RevenueOptions.SectionName));
builder.Services.Configure<PricingOptions>(builder.Configuration.GetSection(PricingOptions.SectionName));
builder.Services.Configure<UploadOptions>(builder.Configuration.GetSection(UploadOptions.SectionName));
builder.Services.Configure<PaymentsOptions>(builder.Configuration.GetSection(PaymentsOptions.SectionName));
builder.Services.PostConfigure<PaymentsOptions>(o =>
    o.SettlementDelay ??= builder.Environment.IsDevelopment() ? TimeSpan.FromMinutes(2) : TimeSpan.FromMinutes(30));
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 32 * 1024 * 1024; // 32 MB
});
builder.Services.AddSupabaseStorage(builder.Configuration);
builder.Services.AddScoped<IFavoriteService, FavoriteService>();
builder.Services.AddScoped<ISavedSearchService, SavedSearchService>();
builder.Services.AddScoped<IEquipmentService, EquipmentService>();
builder.Services.AddScoped<IGodownService, GodownService>();
builder.Services.AddScoped<IStorageIntakeService, StorageIntakeService>();
builder.Services.AddScoped<IReceiptDocumentService, ReceiptDocumentService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IHarvestPlanService, HarvestPlanService>();
builder.Services.AddScoped<IFarmerProfileService, FarmerProfileService>();
builder.Services.AddScoped<IGodownRevenueService, GodownRevenueService>();
builder.Services.AddScoped<IEquipmentRevenueService, EquipmentRevenueService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ICropCalendarService, CropCalendarService>();
builder.Services.AddScoped<IWeatherService, WeatherService>();
builder.Services.AddScoped<IPestAlertService, PestAlertService>();
builder.Services.AddScoped<IOwnerVerificationService, OwnerVerificationService>();
builder.Services.AddScoped<IWeatherSuggestionService, WeatherSuggestionService>();
builder.Services.AddSingleton<IQrCodeService, QrCodeService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IBadgeService, BadgeService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();
builder.Services.AddScoped<ILoyaltyService, LoyaltyService>();

builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
builder.Services.AddHttpClient();
var keyPath = builder.Configuration["DataProtection:KeyPath"] ?? "App_Data/keys";
builder.Services.AddDataProtection()
    .SetApplicationName("KrishiLink")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(keyPath, builder.Environment.ContentRootPath)));

builder.Services.AddScoped<IPayoutSettlementService, PayoutSettlementService>();

// Money flow: simulated gateway behind IPaymentGateway; swap providers by adding a class and a case here
var paymentProvider = builder.Configuration[$"{PaymentsOptions.SectionName}:Provider"] ?? SimulatedPaymentGateway.ProviderName;
builder.Services.AddScoped<IPaymentGateway>(sp => paymentProvider switch
{
    SimulatedPaymentGateway.ProviderName => ActivatorUtilities.CreateInstance<SimulatedPaymentGateway>(sp),
    _ => throw new InvalidOperationException($"Unknown payment provider '{paymentProvider}'. Supported: {SimulatedPaymentGateway.ProviderName}.")
});
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ILedgerService, LedgerService>();

// Email + scheduled background services
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
if (builder.Configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>()?.IsConfigured == true)
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
else
    builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();

builder.Services.AddSingleton<EmailDispatchService>();
builder.Services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<EmailDispatchService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<EmailDispatchService>());
builder.Services.Configure<ReminderOptions>(builder.Configuration.GetSection(ReminderOptions.SectionName));
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.Configure<AlertOptions>(builder.Configuration.GetSection(AlertOptions.SectionName));

builder.Services.AddHostedService<MonthlyStatementScheduler>();
builder.Services.AddHostedService<PayoutSettlementScheduler>();
builder.Services.AddHostedService<WeatherSuggestionScheduler>();
builder.Services.AddHostedService<ReminderScheduler>();
builder.Services.AddHostedService<SavedSearchAlertScheduler>();

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Schema changes use a session/direct connection, never transaction pooling.
if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    var migrationOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql(DatabaseConfiguration.GetConnectionString(builder.Configuration, forMigrations: true),
            postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", DatabaseConfiguration.Schema))
        .Options;
    await using var migrationDb = new ApplicationDbContext(migrationOptions);
    await migrationDb.Database.MigrateAsync();
}

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<SupabaseStorageClient>().InitializeBucketsAsync();
    await DbInitializer.InitializeAsync(scope.ServiceProvider);
}

app.UseRequestLocalization();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Redirecting to HTTPS in development would break LAN access from devices that do not trust the dev certificate.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Print the addresses other devices can use, so QR codes scanned on a phone resolve to a reachable host.
app.Lifetime.ApplicationStarted.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var wildcards = app.Urls.Where(url => url.Contains("0.0.0.0") || url.Contains("[::]")).ToList();
    if (wildcards.Count == 0) return;

    var addresses = NetworkInterface.GetAllNetworkInterfaces()
        .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
        .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
        .Select(address => address.Address.ToString())
        .Distinct();

    foreach (var address in addresses)
    {
        foreach (var url in wildcards)
        {
            logger.LogInformation("KrishiLink is reachable on this network at {Url}", url.Replace("0.0.0.0", address).Replace("[::]", address));
        }
    }
});

app.Run();
