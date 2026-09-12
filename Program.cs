using KrishiLink.BLL.Services;
using KrishiLink.DAL;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// QuestPDF community licence (free for organisations under USD 1M annual revenue)
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Add DbContext with MsSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

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

// Data access: generic EF repositories + revenue reporting repositories
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IGodownRevenueRepository, GodownRevenueRepository>();
builder.Services.AddScoped<IEquipmentRevenueRepository, EquipmentRevenueRepository>();

// Business logic
builder.Services.Configure<RevenueOptions>(builder.Configuration.GetSection(RevenueOptions.SectionName));
builder.Services.Configure<UploadOptions>(builder.Configuration.GetSection(UploadOptions.SectionName));
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 32 * 1024 * 1024; // 32 MB
});
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IEquipmentService, EquipmentService>();
builder.Services.AddScoped<IGodownService, GodownService>();
builder.Services.AddScoped<IBookingService, BookingService>();
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
builder.Services.AddDataProtection();

builder.Services.AddScoped<IPayoutSettlementService, PayoutSettlementService>();

// Email + scheduled background services
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
if (builder.Configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>()?.IsConfigured == true)
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
else
    builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();

builder.Services.AddSingleton<EmailDispatchService>();
builder.Services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<EmailDispatchService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<EmailDispatchService>());
builder.Services.AddHostedService<MonthlyStatementScheduler>();
builder.Services.AddHostedService<PayoutSettlementScheduler>();
builder.Services.AddHostedService<WeatherSuggestionScheduler>();

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Apply migrations, ensure roles exist and (in Development) load demo data on first run
using (var scope = app.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(scope.ServiceProvider, seedDemoData: app.Environment.IsDevelopment());
}

app.UseRequestLocalization();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
