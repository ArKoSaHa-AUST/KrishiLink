using KrishiLink.BLL.Services;
using KrishiLink.BLL.Services.Ai;
using KrishiLink.Controllers;
using KrishiLink.DAL;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text.Encodings.Web;
using System.Text.Unicode;

var builder = WebApplication.CreateBuilder(args);
EnvironmentConfiguration.AddLocalEnvironmentFiles(builder.Configuration, builder.Environment.ContentRootPath, args);

// Register Unicode-safe HTML encoder so Bengali/Unicode text is rendered naturally without &#x... entity escaping
builder.Services.AddSingleton<HtmlEncoder>(HtmlEncoder.Create(UnicodeRanges.All));

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

// Off = one platform-wide workflow lock (the proven default). On = per-listing / per-user locks; revert with this setting.
WorkflowTransaction.ShardedLocks = builder.Configuration.GetValue<bool>("Database:ShardedWorkflowLocks");

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
.AddDefaultTokenProviders()
.AddClaimsPrincipalFactory<KrishiLinkClaimsPrincipalFactory>();

builder.Services.AddAuthorization(MvcSecurity.ConfigurePolicies);
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, VerifiedEmailResultHandler>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Headers.XRequestedWith == "XMLHttpRequest" ||
            (context.Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});
// Database sessions survive restarts and are shared across instances; memory is for local development only.
var sessionStore = builder.Configuration["Authentication:SessionStore"] ?? (builder.Environment.IsDevelopment() ? "Memory" : "Database");
if (sessionStore is not ("Memory" or "Database"))
    throw new InvalidOperationException("Authentication:SessionStore must be \"Memory\" or \"Database\".");
builder.Services.AddSupabaseAuthentication(builder.Configuration, databaseSessions: sessionStore == "Database");

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
    // SecurityHeaders sends the stricter X-Frame-Options: DENY on every response.
    options.SuppressXFrameOptionsHeader = true;
});

// Client IPs (rate limiting) and the https scheme come from X-Forwarded-* only when the request arrives through a
// proxy listed here. Nothing is trusted by default: an empty list makes the middleware a no-op instead of letting any
// client spoof its address. Configure ForwardedHeaders:KnownProxies / KnownNetworks for the real deployment.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
        options.KnownProxies.Add(IPAddress.Parse(proxy));
    foreach (var network in builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? Array.Empty<string>())
    {
        var parts = network.Split('/', 2);
        options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse(parts[0]), int.Parse(parts[1], CultureInfo.InvariantCulture)));
    }
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
builder.Services.AddScoped<ISeasonEconomicsService, SeasonEconomicsService>();
builder.Services.AddScoped<IPriceBenchmarkService, PriceBenchmarkService>();
builder.Services.AddScoped<IFarmerProfileService, FarmerProfileService>();
builder.Services.AddScoped<IGodownRevenueService, GodownRevenueService>();
builder.Services.AddScoped<IEquipmentRevenueService, EquipmentRevenueService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddSingleton<IRealtimeUpdateService, RealtimeUpdateService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ICropCalendarService, CropCalendarService>();
builder.Services.AddScoped<ICropAdvisorService, CropAdvisorService>();
builder.Services.AddScoped<ISuggestionStateService, SuggestionStateService>();
builder.Services.AddScoped<IPestAlertHistoryService, PestAlertHistoryService>();
builder.Services.AddScoped<IWeatherService, WeatherService>();
builder.Services.AddScoped<IPestAlertService, PestAlertService>();
builder.Services.AddScoped<IOwnerVerificationService, OwnerVerificationService>();
builder.Services.AddScoped<IWeatherSuggestionService, WeatherSuggestionService>();
builder.Services.AddSingleton<IQrCodeService, QrCodeService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IBadgeService, BadgeService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();
builder.Services.AddScoped<ILoyaltyService, LoyaltyService>();
builder.Services.AddScoped<ICommunityService, CommunityService>();

// Read-only views of the booking services. The AI assistant depends on these alone, so it has no write path.
builder.Services.AddScoped<IEquipmentQueries>(sp => sp.GetRequiredService<IEquipmentService>());
builder.Services.AddScoped<IGodownQueries>(sp => sp.GetRequiredService<IGodownService>());
builder.Services.AddScoped<IBookingQueries>(sp => sp.GetRequiredService<IBookingService>());
builder.Services.AddScoped<IWeatherSuggestionQueries>(sp => sp.GetRequiredService<IWeatherSuggestionService>());

// AI assistant: Groq first, Google Gemini as fallback (both OpenAI-compatible). Keys come from GROQ_API_KEY[_2|_3] and
// GEMINI_API_KEY[_2] in .env / .env.local or the host environment; they never reach the browser.
builder.Services.Configure<GroqOptions>(builder.Configuration.GetSection(GroqOptions.SectionName));
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));
builder.Services.AddHttpClient(FailoverChatClient.GroqName, (sp, http) =>
{
    var groq = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GroqOptions>>().Value;
    http.BaseAddress = new Uri(groq.BaseUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(Math.Max(5, groq.TimeoutSeconds));
});
builder.Services.AddHttpClient(FailoverChatClient.GeminiName, (sp, http) =>
{
    var gemini = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GeminiOptions>>().Value;
    http.BaseAddress = new Uri(gemini.BaseUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(Math.Max(5, gemini.TimeoutSeconds));
});
builder.Services.AddSingleton<ChatProviderStates>();
builder.Services.AddScoped<IChatModelClient, FailoverChatClient>();
builder.Services.AddScoped<AgentTools>();
builder.Services.AddScoped<IAgentService, AgentService>();

// QR codes, PDFs and e-mails embed absolute links built from this, so it must be the real public https origin.
builder.Services.AddOptions<AppOptions>()
    .Bind(builder.Configuration.GetSection(AppOptions.SectionName))
    .Validate(o => Uri.TryCreate(o.PublicBaseUrl, UriKind.Absolute, out var url)
            && (builder.Environment.IsDevelopment()
                || (url.Scheme == Uri.UriSchemeHttps && !(builder.Environment.IsProduction() && url.IsLoopback))),
        "App:PublicBaseUrl must be the site's absolute https:// address (and not localhost in Production).")
    .ValidateOnStart();

// Host names are allow-listed outside Development so a forged Host header is rejected before any link is built.
// Development accepts any host because phones on the LAN reach the site by IP address.
if (builder.Environment.IsDevelopment())
{
    builder.Services.PostConfigure<HostFilteringOptions>(o => o.AllowedHosts = new List<string> { "*" });
}
else if (builder.Configuration["AllowedHosts"] is not { Length: > 0 } allowedHosts
         || allowedHosts.Split(';', StringSplitOptions.TrimEntries).Contains("*"))
{
    throw new InvalidOperationException("Set AllowedHosts to the site's real host names (semicolon-separated). '*' is only allowed in Development.");
}
builder.Services.AddHttpClient();
// The key ring decrypts every stored NID number and the sign-in cookie: it must live on durable, backed-up storage
// shared by all instances (DataProtection:KeyPath), and be encrypted at rest with a certificate outside Development.
var keyPath = builder.Configuration["DataProtection:KeyPath"] ?? "App_Data/keys";
var keyDirectory = new DirectoryInfo(Path.GetFullPath(keyPath, builder.Environment.ContentRootPath));
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("KrishiLink")
    .PersistKeysToFileSystem(keyDirectory);
var keyCertificatePath = builder.Configuration["DataProtection:CertificatePath"];
if (!string.IsNullOrWhiteSpace(keyCertificatePath))
{
    dataProtection.ProtectKeysWithCertificate(new X509Certificate2(
        Path.GetFullPath(keyCertificatePath, builder.Environment.ContentRootPath),
        builder.Configuration["DataProtection:CertificatePassword"]));
}

builder.Services.AddScoped<IPayoutSettlementService, PayoutSettlementService>();

builder.Services.AddKrishiLinkRateLimiting();
builder.Services.Configure<DiagnosticsOptions>(builder.Configuration.GetSection(DiagnosticsOptions.SectionName));

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" })
    .AddCheck<SupabaseAuthHealthCheck>("supabase-auth", tags: new[] { "ready" });

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

builder.Services.AddScoped<IEmailDeliveryRecorder, EmailDeliveryRecorder>();
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
var mvc = builder.Services.AddControllersWithViews(options =>
{
    MvcSecurity.Configure(options);
    options.Filters.Add<ConcurrencyConflictFilter>();
    options.Filters.Add<JsonExceptionFilter>();
});
builder.Services.AddProblemDetails();
// .cshtml edits show on refresh while developing; other environments serve the precompiled views.
if (builder.Environment.IsDevelopment()) mvc.AddRazorRuntimeCompilation();

var app = builder.Build();

// Every instance runs this on boot, so a session-level advisory lock makes them take turns: the first does the work,
// the rest wait and then find nothing left to do. Requires a session/direct connection (never transaction pooling).
await using (var startupLock = await DbInitializer.AcquireStartupLockAsync(connectionString))
{
    if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
    {
        // Production applies the CI-generated idempotent script as a reviewed deployment step instead.
        if (!app.Environment.IsDevelopment())
            app.Logger.LogWarning("Database:ApplyMigrationsOnStartup is enabled outside Development; apply the reviewed migration script instead.");

        var migrationOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(DatabaseConfiguration.GetConnectionString(builder.Configuration, forMigrations: true),
                postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", DatabaseConfiguration.Schema))
            .Options;
        await using var migrationDb = new ApplicationDbContext(migrationOptions);
        await migrationDb.Database.MigrateAsync();
    }

    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<SupabaseStorageClient>().InitializeBucketsAsync();
    await DbInitializer.InitializeAsync(scope.ServiceProvider);
}

CheckDataProtectionKeyRing(app, keyDirectory, encrypted: !string.IsNullOrWhiteSpace(keyCertificatePath));

// First, so every later component (HTTPS redirection, rate limiting, logging) sees the real client and scheme.
app.UseForwardedHeaders();

// Correlation id + request timing (QLT-02): every later log line carries the id the response hands back.
app.UseRequestDiagnostics();

app.UseRequestLocalization();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Empty 4xx/5xx responses: fetch/XHR callers get problem+json they can parse, browsers get the friendly error page.
app.UseWhen(context => ConcurrencyConflictFilter.WantsJson(context.Request), json => json.UseStatusCodePages());
app.UseWhen(context => !ConcurrencyConflictFilter.WantsJson(context.Request), html => html.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}"));

// Redirecting to HTTPS in development would break LAN access from devices that do not trust the dev certificate.
// Health probes are exempt: orchestrators probe over plain HTTP and count a 3xx redirect as success.
if (!app.Environment.IsDevelopment())
{
    app.UseWhen(context => !IsHealthProbe(context.Request.Path), branch => branch.UseHttpsRedirection());
}

// Before static files so every response, including assets and error pages, carries the headers.
app.UseSecurityHeaders(enforceCsp: builder.Configuration.GetValue<bool>("Security:EnforceContentSecurityPolicy"));

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
// After authentication so signed-in users are limited per account rather than per shared IP.
app.UseRateLimiter();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapCspReports();

// Liveness answers from the process alone; readiness also needs the database and Supabase Auth.
app.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/readyz", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

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

static bool IsHealthProbe(PathString path) => path.StartsWithSegments("/healthz") || path.StartsWithSegments("/readyz");

static void CheckDataProtectionKeyRing(WebApplication app, DirectoryInfo keyDirectory, bool encrypted)
{
    keyDirectory.Create();
    if (!OperatingSystem.IsWindows())
    {
        try
        {
            File.SetUnixFileMode(keyDirectory.FullName, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            app.Logger.LogWarning("Could not restrict the Data Protection key directory {Path} to the app user.", keyDirectory.FullName);
        }
    }

    if (app.Environment.IsDevelopment()) return;
    if (!keyDirectory.EnumerateFiles("key-*.xml").Any())
    {
        app.Logger.LogCritical(
            "The Data Protection key ring at {Path} is EMPTY. A new ring will be created: every NID number encrypted with a " +
            "previous ring is now unreadable and all users are signed out. If this is not the very first start, mount the " +
            "persistent key volume (DataProtection:KeyPath) and restore it from backup.", keyDirectory.FullName);
    }
    if (!encrypted)
    {
        app.Logger.LogWarning(
            "Data Protection keys at {Path} are stored unencrypted. Set DataProtection:CertificatePath (and CertificatePassword) " +
            "so a copy of the key directory or a backup cannot decrypt stored NID numbers.", keyDirectory.FullName);
    }
}
