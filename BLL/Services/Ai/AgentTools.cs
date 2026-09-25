using System.Diagnostics;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using KrishiLink.BLL.Helpers;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace KrishiLink.BLL.Services.Ai
{
    /// <summary>
    /// Who is asking, resolved on the server from the auth cookie (A1). Tool handlers take identity from here only;
    /// a userId/farmerId/ownerId the model puts into tool arguments is never read.
    /// </summary>
    public sealed record AgentCaller(
        string UserId,
        IReadOnlyCollection<string> Roles,
        bool EmailVerified,
        string Culture,
        string? District,
        string? Specialization)
    {
        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
        public bool IsBangla => Culture.StartsWith("bn", StringComparison.OrdinalIgnoreCase);
        public string PrimaryRole => Roles.FirstOrDefault(r => r == AppRoles.Admin) ?? Roles.FirstOrDefault() ?? "User";
        public string? Division => string.IsNullOrWhiteSpace(District) ? null : BangladeshGeo.GetDivision(District);
    }

    public enum AgentToolKind
    {
        /// <summary>Reads platform data; executed automatically.</summary>
        Read,

        /// <summary>Validates and prepares a pre-filled form; executes nothing.</summary>
        Proposal
    }

    public sealed record AgentTool(
        string Name,
        AgentToolKind Kind,
        string[]? Roles,
        string Description,
        JsonObject Parameters,
        string CitationLabel,
        string? CitationUrl,
        Func<AgentTools, JsonElement, AgentCaller, CancellationToken, Task<AgentToolResult>> Handler);

    /// <summary>The outcome of one tool call: the data the model sees, plus what the panel renders.</summary>
    public sealed class AgentToolResult
    {
        public bool Ok { get; init; } = true;
        public object Data { get; init; } = new();
        public List<AgentListingCard> Listings { get; init; } = new();
        public AgentProposal? Proposal { get; init; }
        public string? CitationUrl { get; init; }

        public static AgentToolResult Success(object data) => new() { Data = data };
        public static AgentToolResult Fail(string error, string message) => new() { Ok = false, Data = new ToolErrorDto(error, message) };
    }

    /// <summary>A tool argument the model got wrong; the message goes back so it can correct itself.</summary>
    public sealed class ToolArgumentException : Exception
    {
        public ToolArgumentException(string message) : base(message) { }
    }

    /// <summary>
    /// The assistant's tool catalogue and dispatcher. Every handler is a thin wrapper over an existing service, and
    /// the constructor takes only the read-only query interfaces, so no mutating service method is reachable from here
    /// at compile time. At run time the dispatcher resolves names only from <see cref="ReadOnlyTools"/> and
    /// <see cref="ProposalTools"/>; <see cref="MutatingTools"/> is deliberately empty.
    /// </summary>
    public sealed class AgentTools
    {
        public const int MaxSearchRows = 8;
        private const int MaxTextLength = 600;
        private const string DateFormat = "yyyy-MM-dd";

        private readonly IEquipmentQueries _equipment;
        private readonly IGodownQueries _godowns;
        private readonly IBookingQueries _bookings;
        private readonly ICropCalendarService _crops;
        private readonly IWeatherService _weather;
        private readonly IPestAlertService _pests;
        private readonly IWeatherSuggestionQueries _suggestions;
        private readonly GroqOptions _options;
        private readonly ILogger<AgentTools> _logger;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public AgentTools(
            IEquipmentQueries equipment,
            IGodownQueries godowns,
            IBookingQueries bookings,
            ICropCalendarService crops,
            IWeatherService weather,
            IPestAlertService pests,
            IWeatherSuggestionQueries suggestions,
            IOptions<GroqOptions> options,
            ILogger<AgentTools> logger,
            IStringLocalizer<SharedResource> localizer)
        {
            _equipment = equipment;
            _godowns = godowns;
            _bookings = bookings;
            _crops = crops;
            _weather = weather;
            _pests = pests;
            _suggestions = suggestions;
            _options = options.Value;
            _logger = logger;
            _localizer = localizer;
        }

        /// <summary>
        /// Envelope serializer (A2). The encoder lets Bangla through unescaped but always escapes &lt; &gt; &amp; and quotes,
        /// so text inside a listing can never spell the closing tag and break out of its envelope.
        /// </summary>
        internal static readonly JsonSerializerOptions EnvelopeJson = new(JsonSerializerDefaults.Web)
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static string Envelope(string toolName, object data) =>
            $"<tool_result tool=\"{toolName}\" trust=\"untrusted-data\">\n{JsonSerializer.Serialize(data, data.GetType(), EnvelopeJson)}\n</tool_result>";

        // ---------------------------------------------------------------- Catalogue

        private static readonly string[] FarmerOnly = { AppRoles.Farmer };
        private static readonly string[] ListingTypes = { AgentProposal.EquipmentType, AgentProposal.GodownType };

        // Descriptions are deliberately terse: every word is resent on every model call, and Groq meters tokens per minute.
        // Districts are free text validated on the server (unknown names are rejected with the nearest real ones), which
        // keeps the model from inventing one without resending all 64 names in every tool's schema.
        public static readonly IReadOnlyList<AgentTool> ReadOnlyTools = new AgentTool[]
        {
            new("search_equipment", AgentToolKind.Read, null,
                "Find farm machinery for rent. Max 8 real listings. Dates only if the user gave them.",
                Schema(
                    ("keyword", Text("Words in name/description"), false),
                    ("category", Text(null, OnboardingOptions.EquipmentCategories), false),
                    ("district", Text("Bangladeshi district"), false),
                    ("division", Text(null, BangladeshGeo.Divisions), false),
                    ("maxDailyRate", Number("BDT/day"), false),
                    ("startDate", Date(), false),
                    ("endDate", Date(), false),
                    ("minUnits", Integer(), false),
                    ("sort", Text(null, new[] { "newest", "price_asc", "price_desc", "rating_desc" }), false),
                    ("page", Integer(), false)),
                "Equipment listings", "/Equipment",
                (t, a, c, ct) => t.SearchEquipmentAsync(a)),

            new("search_godowns", AgentToolKind.Read, null,
                "Find crop storage (godowns, cold storage). Price is BDT per ton per month. Max 8 real listings.",
                Schema(
                    ("keyword", Text("Words in name/description"), false),
                    ("storageType", Text(null, OnboardingOptions.StorageTypes), false),
                    ("district", Text("Bangladeshi district"), false),
                    ("division", Text(null, BangladeshGeo.Divisions), false),
                    ("minCapacityTons", Number(null), false),
                    ("maxPricePerTonPerMonth", Number(null), false),
                    ("startDate", Date(), false),
                    ("endDate", Date(), false),
                    ("page", Integer(), false)),
                "Storage listings", "/Godown",
                (t, a, c, ct) => t.SearchGodownsAsync(a)),

            new("get_listing_details", AgentToolKind.Read, null,
                "One listing: rates, seasonal rules, min rental days, quantity/capacity, blocked dates (60 days).",
                Schema(("type", Text(null, ListingTypes), true), ("id", Integer(), true)),
                "Listing details", null,
                (t, a, c, ct) => t.GetListingDetailsAsync(a)),

            new("check_availability", AgentToolKind.Read, null,
                "Is a listing free for dates? units for equipment, tons for godowns.",
                Schema(("type", Text(null, ListingTypes), true), ("id", Integer(), true), ("startDate", Date(), true), ("endDate", Date(), true),
                    ("units", Integer(), false), ("tons", Number(null), false)),
                "Availability check", null,
                (t, a, c, ct) => t.CheckAvailabilityAsync(a)),

            new("get_price_quote", AgentToolKind.Read, null,
                "Exact BDT price for a listing and dates (platform pricing). units for equipment, tons for godowns. Never compute prices yourself.",
                Schema(("type", Text(null, ListingTypes), true), ("id", Integer(), true), ("startDate", Date(), true), ("endDate", Date(), true),
                    ("units", Integer(), false), ("tons", Number(null), false)),
                "Price quote", null,
                (t, a, c, ct) => t.GetPriceQuoteAsync(a)),

            new("get_crop_calendar", AgentToolKind.Read, null,
                "DAE crop calendar: crops to sow/harvest this month in a district; with crop, its phase, varieties, tips. Defaults: profile district, current month.",
                Schema(("crop", Text(null), false), ("district", Text("Bangladeshi district"), false), ("month", Integer(), false)),
                "DAE crop calendar", "/Advisory/Calendar",
                (t, a, c, ct) => t.GetCropCalendarAsync(a, c)),

            new("recommend_crops", AgentToolKind.Read, null,
                "Smart Advisor: scores every DAE crop for the farm (season, sowing time, soil, irrigation, district, pH) with the reason for each point. Ask about irrigation if unknown. Use its crops only; never add one.",
                Schema(
                    ("hasIrrigation", Boolean("Assured pump/canal irrigation"), true),
                    ("soilType", Text(null, SoilClassifier.FormOptions.Select(o => o.Value)), false),
                    ("season", Text(null, CropAdvisorScorer.SeasonValues), false),
                    ("district", Text("Bangladeshi district"), false),
                    ("soilPh", Number(null), false),
                    ("landSizeDecimal", Number("Decimals; 100 = 1 acre, 33 = 1 bigha"), false)),
                "Smart Advisor", "/Advisory",
                (t, a, c, ct) => t.RecommendCropsAsync(a, c)),

            new("get_weather_forecast", AgentToolKind.Read, null,
                "Open-Meteo weather and daily forecast. Say so if source is not Live. Default: profile district.",
                Schema(("district", Text("Bangladeshi district"), false)),
                "Open-Meteo forecast", "/Advisory/Suggestions",
                (t, a, c, ct) => t.GetWeatherForecastAsync(a, c, ct)),

            new("get_pest_alerts", AgentToolKind.Read, null,
                "Pest/disease outbreak warnings triggered by the forecast, with cause and remedies. Defaults: profile district and crop.",
                Schema(("district", Text("Bangladeshi district"), false), ("crop", Text(null), false)),
                "Pest & Disease Alerts", "/Advisory/Alerts",
                (t, a, c, ct) => t.GetPestAlertsAsync(a, c)),

            new("get_weather_suggestions", AgentToolKind.Read, null,
                "Advice linking forecast to crop stage and machinery/storage needs. Defaults: profile district and crop.",
                Schema(("district", Text("Bangladeshi district"), false), ("crop", Text(null), false)),
                "Weather Suggestions", "/Advisory/Suggestions",
                (t, a, c, ct) => t.GetWeatherSuggestionsAsync(a, c)),

            new("get_my_bookings", AgentToolKind.Read, FarmerOnly,
                "The user's own bookings, newest first.",
                Schema(("status", Text(null, new[] { "all", "pending", "accepted", "paid", "completed", "rejected", "cancelled" }), false), ("limit", Integer(), false)),
                "Your bookings", "/Bookings",
                (t, a, c, ct) => t.GetMyBookingsAsync(a, c)),

            new("get_my_profile_context", AgentToolKind.Read, null,
                "The user's role, district, crop/specialization, language.",
                Schema(),
                "Your profile", "/Account/Profile",
                (t, a, c, ct) => Task.FromResult(AgentToolResult.Success(ProfileContext(c)))),
        };

        public static readonly IReadOnlyList<AgentTool> ProposalTools = new AgentTool[]
        {
            new("propose_equipment_rental", AgentToolKind.Proposal, FarmerOnly,
                "Show the user a pre-filled rental request to confirm. Books nothing. Only when id, both dates and units came from the user.",
                Schema(("equipmentId", Integer(), true), ("startDate", Date(), true), ("endDate", Date(), true), ("units", Integer(), true),
                    ("note", Text("Short note for owner"), false)),
                "Rental request draft", null,
                (t, a, c, ct) => t.ProposeEquipmentRentalAsync(a, c)),

            new("propose_godown_storage", AgentToolKind.Proposal, FarmerOnly,
                "Show the user a pre-filled storage request to confirm. Books nothing. Only when id, dates, tons and crop came from the user.",
                Schema(("godownId", Integer(), true), ("startDate", Date(), true), ("endDate", Date(), true), ("tons", Number(null), true),
                    ("cropType", Text(null), true), ("note", Text("Short note for owner"), false)),
                "Storage request draft", null,
                (t, a, c, ct) => t.ProposeGodownStorageAsync(a, c)),
        };

        /// <summary>
        /// Tools that would change state. Empty in v1 by design: accepting, paying, cancelling, listing and payout paths stay
        /// manual. Adding anything here requires a written threat model first.
        /// </summary>
        public static readonly IReadOnlyList<AgentTool> MutatingTools = Array.Empty<AgentTool>();

        private static readonly Dictionary<string, AgentTool> Catalogue =
            ReadOnlyTools.Concat(ProposalTools).ToDictionary(t => t.Name, StringComparer.Ordinal);

        public static AgentTool? Find(string name) => Catalogue.TryGetValue(name, out var tool) ? tool : null;

        /// <summary>The definitions sent upstream — only the tools this caller may use, so the list itself leaks nothing (A4).</summary>
        public static List<ChatToolDefinition> DefinitionsFor(AgentCaller caller) =>
            Catalogue.Values
                .Where(t => IsAllowed(t, caller))
                .Select(t => new ChatToolDefinition
                {
                    Function = new ChatFunctionDefinition { Name = t.Name, Description = t.Description, Parameters = (JsonObject)t.Parameters.DeepClone() }
                })
                .ToList();

        private static bool IsAllowed(AgentTool tool, AgentCaller caller) => tool.Roles is null || tool.Roles.Any(caller.IsInRole);

        // ---------------------------------------------------------------- Dispatcher

        /// <summary>
        /// Runs one model-requested tool call. Unknown names, bad arguments, a wrong role and service failures all come back
        /// as structured error payloads for the model to relay — this never throws for a bad call.
        /// </summary>
        public async Task<AgentToolResult> ExecuteAsync(string name, string? argumentsJson, AgentCaller caller, CancellationToken cancellationToken = default)
        {
            var clock = Stopwatch.StartNew();
            var result = await DispatchAsync(name, argumentsJson, caller, cancellationToken);
            _logger.LogInformation("Agent tool {Tool} finished in {ElapsedMs} ms: {Outcome}.",
                name, clock.ElapsedMilliseconds, result.Ok ? "ok" : ((ToolErrorDto)result.Data).Error);
            return result;
        }

        private async Task<AgentToolResult> DispatchAsync(string name, string? argumentsJson, AgentCaller caller, CancellationToken cancellationToken)
        {
            var tool = Find(name);
            if (tool is null)
                return AgentToolResult.Fail("unknown_tool", $"There is no tool named '{name}'. Use only the tools provided.");
            if (!IsAllowed(tool, caller))
                return AgentToolResult.Fail("not_available", "This tool is not available for the user's account type.");

            JsonElement arguments;
            try
            {
                using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
                arguments = document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : throw new JsonException();
            }
            catch (JsonException)
            {
                return AgentToolResult.Fail("invalid_arguments", "Arguments must be a JSON object matching the tool's parameters.");
            }

            try
            {
                var result = await tool.Handler(this, arguments, caller, cancellationToken);
                return result.CitationUrl is null && tool.CitationUrl is not null
                    ? new AgentToolResult { Ok = result.Ok, Data = result.Data, Listings = result.Listings, Proposal = result.Proposal, CitationUrl = tool.CitationUrl }
                    : result;
            }
            catch (ToolArgumentException ex)
            {
                return AgentToolResult.Fail("invalid_arguments", ex.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Agent tool {Tool} failed.", name);
                return AgentToolResult.Fail("tool_failed", "This data source is temporarily unavailable. Tell the user and suggest the manual page.");
            }
        }

        // ---------------------------------------------------------------- Search

        private async Task<AgentToolResult> SearchEquipmentAsync(JsonElement a)
        {
            var (start, end) = OptionalRange(a);
            var model = await _equipment.BrowseAsync(new EquipmentSearchCriteria
            {
                SearchTerm = OptString(a, "keyword"),
                SelectedCategories = OptOneOf(a, "category", OnboardingOptions.EquipmentCategories) is { } category ? new List<string> { category } : null,
                District = OptDistrict(a, "district"),
                Division = OptOneOf(a, "division", BangladeshGeo.Divisions),
                SelectedMaxPrice = OptDecimal(a, "maxDailyRate"),
                StartDate = start,
                EndDate = end,
                Units = OptInt(a, "minUnits") ?? 1,
                SortBy = OptOneOf(a, "sort", new[] { "newest", "price_asc", "price_desc", "rating_desc" }) ?? "newest",
                Page = Math.Max(1, OptInt(a, "page") ?? 1),
                PageSize = MaxSearchRows
            });

            var rows = model.EquipmentList.Select(e => new EquipmentResultDto(
                e.Id, e.Name, e.Category, e.District, e.DailyRate, e.MinRentalDays, e.Quantity, e.IsAvailable,
                Math.Round(e.Rating, 1), Math.Round(e.OwnerRating, 1), e.OwnerIsVerified, e.HasRateRules, EquipmentUrl(e.Id))).ToList();

            return new AgentToolResult
            {
                Data = new EquipmentSearchDto(model.TotalCount, model.Page, rows,
                    rows.Count == 0 ? "No listings match. Say so plainly; do not suggest listings that are not in this result." : null),
                Listings = model.EquipmentList.Select(e => new AgentListingCard
                {
                    Type = AgentProposal.EquipmentType,
                    Id = e.Id,
                    Name = e.Name,
                    Subtitle = e.Category,
                    District = e.District,
                    RateText = $"{ListingFormat.Taka(e.DailyRate)}/day",
                    Rating = Math.Round(e.OwnerRating, 1),
                    IsVerifiedOwner = e.OwnerIsVerified,
                    ImageUrl = string.IsNullOrWhiteSpace(e.ImageUrl) ? null : e.ImageUrl,
                    DetailUrl = EquipmentUrl(e.Id)
                }).ToList()
            };
        }

        private async Task<AgentToolResult> SearchGodownsAsync(JsonElement a)
        {
            var (start, end) = OptionalRange(a);
            var model = await _godowns.BrowseAsync(new GodownSearchCriteria
            {
                SearchTerm = OptString(a, "keyword"),
                SelectedStorageTypes = OptOneOf(a, "storageType", OnboardingOptions.StorageTypes) is { } type ? new List<string> { type } : null,
                District = OptDistrict(a, "district"),
                Division = OptOneOf(a, "division", BangladeshGeo.Divisions),
                SelectedMinCapacity = OptDouble(a, "minCapacityTons"),
                SelectedMaxPrice = OptDecimal(a, "maxPricePerTonPerMonth"),
                AvailableStartDate = start,
                AvailableEndDate = end,
                Page = Math.Max(1, OptInt(a, "page") ?? 1),
                PageSize = MaxSearchRows
            });

            var rows = model.GodownList.Select(g => new GodownResultDto(
                g.Id, g.Name, g.StorageType, g.District, g.PricePerTonPerMonth, Math.Round(g.PricePerTonPerMonth / 30m, 2),
                Math.Round(g.AvailableCapacityTons, 1), Math.Round(g.TotalCapacityTons, 1),
                Math.Round(g.Rating, 1), Math.Round(g.OwnerRating, 1), g.OwnerIsVerified, GodownUrl(g.Id))).ToList();

            return new AgentToolResult
            {
                Data = new GodownSearchDto(model.TotalCount, model.Page, rows,
                    rows.Count == 0 ? "No listings match. Say so plainly; do not suggest listings that are not in this result." : null),
                Listings = model.GodownList.Select(g => new AgentListingCard
                {
                    Type = AgentProposal.GodownType,
                    Id = g.Id,
                    Name = g.Name,
                    Subtitle = g.StorageType,
                    District = g.District,
                    RateText = $"{ListingFormat.Taka(g.PricePerTonPerMonth)}/ton/month",
                    Rating = Math.Round(g.OwnerRating, 1),
                    IsVerifiedOwner = g.OwnerIsVerified,
                    ImageUrl = string.IsNullOrWhiteSpace(g.ImageUrl) ? null : g.ImageUrl,
                    DetailUrl = GodownUrl(g.Id)
                }).ToList()
            };
        }

        // ---------------------------------------------------------------- Listing details, availability, price

        private async Task<AgentToolResult> GetListingDetailsAsync(JsonElement a)
        {
            var type = ListingType(a);
            var id = ReqInt(a, "id");
            var horizon = BangladeshClock.Today.AddDays(60);

            if (type == AgentProposal.EquipmentType)
            {
                var e = await _equipment.GetDetailsAsync(id);
                if (e is null) return NotFound(type, id);
                return new AgentToolResult
                {
                    Data = new EquipmentDetailDto(
                        e.Id, e.Name, e.Category, Clip(e.Description), e.District, e.DailyRateAmount,
                        e.MinRentalDays, e.Quantity, e.Status,
                        e.RateRules.Select(r => new RateRuleDto(r.Name, r.Kind.ToString(), r.DateRangeText, r.DailyRate)).ToList(),
                        Dates(e.BookedDates.Where(d => d <= horizon)),
                        Dates(e.PartiallyBookedDates.Where(d => d <= horizon)),
                        Math.Round(e.AverageRating, 1), e.ReviewCount, Math.Round(e.OwnerRating, 1), e.OwnerIsVerified,
                        e.LastServicedText, EquipmentUrl(e.Id)),
                    CitationUrl = EquipmentUrl(e.Id)
                };
            }

            var g = await _godowns.GetDetailsAsync(id);
            if (g is null) return NotFound(type, id);
            return new AgentToolResult
            {
                Data = new GodownDetailDto(
                    g.Id, g.Name, g.StorageType, Clip(g.Description), g.District, g.PricePerTonPerMonthAmount, g.Status,
                    Math.Round(g.TotalCapacityTons, 1), Math.Round(g.AvailableCapacityTons, 1), g.Facilities,
                    Dates(g.UnavailableDates.Where(d => d <= horizon)),
                    Math.Round(g.AverageRating, 1), g.ReviewCount, Math.Round(g.OwnerRating, 1), g.OwnerIsVerified, GodownUrl(g.Id)),
                CitationUrl = GodownUrl(g.Id)
            };
        }

        private async Task<AgentToolResult> CheckAvailabilityAsync(JsonElement a)
        {
            var type = ListingType(a);
            var id = ReqInt(a, "id");
            var (start, end) = RequiredRange(a);

            if (type == AgentProposal.EquipmentType)
            {
                var units = OptInt(a, "units") ?? 1;
                var e = await _equipment.GetDetailsAsync(id);
                if (e is null) return NotFound(type, id);
                var reason = await _equipment.CheckAvailabilityAsync(id, start, end, units);
                var free = await _equipment.FreeUnitsAsync(id, start, end);
                var conflicts = e.BookedDates.Where(d => d >= start && d <= end);
                return new AgentToolResult
                {
                    Data = new AvailabilityDto(type, id, Day(start), Day(end), reason is null, free, "units", Dates(conflicts), reason),
                    CitationUrl = EquipmentUrl(id)
                };
            }

            var tons = OptDouble(a, "tons") ?? 1;
            var g = await _godowns.GetDetailsAsync(id);
            if (g is null) return NotFound(type, id);
            var clash = await _godowns.CheckAvailabilityAsync(id, tons, start, end);
            return new AgentToolResult
            {
                Data = new AvailabilityDto(type, id, Day(start), Day(end), clash is null, Math.Round(g.AvailableCapacityTons, 1), "tons",
                    Dates(g.UnavailableDates.Where(d => d >= start && d <= end)), clash),
                CitationUrl = GodownUrl(id)
            };
        }

        private async Task<AgentToolResult> GetPriceQuoteAsync(JsonElement a)
        {
            var type = ListingType(a);
            var id = ReqInt(a, "id");
            var (start, end) = RequiredRange(a);

            if (type == AgentProposal.EquipmentType)
            {
                var units = OptInt(a, "units") ?? 1;
                var quote = await _equipment.QuoteAsync(id, start, end, units);
                if (quote is null) return NotFound(type, id);
                if (!quote.Ok) return AgentToolResult.Fail("not_quotable", quote.Error ?? "This period cannot be quoted.");
                return new AgentToolResult
                {
                    Data = new PriceQuoteDto(type, id, Day(start), Day(end), quote.Days, quote.Units, "units", quote.Gross,
                        quote.Breakdown.Select(b => new PriceSegmentDto(b.Label, b.Rate, b.Days)).ToList(), quote.Description, "BDT"),
                    CitationUrl = EquipmentUrl(id)
                };
            }

            var tons = OptDouble(a, "tons") ?? throw new ToolArgumentException("Pass 'tons' to quote storage.");
            var g = await _godowns.GetDetailsAsync(id);
            if (g is null) return NotFound(type, id);
            var gross = BookingPricing.GodownGross(start, end, tons, g.PricePerTonPerMonthAmount);
            return new AgentToolResult
            {
                Data = new PriceQuoteDto(type, id, Day(start), Day(end), ListingFormat.InclusiveDays(start, end), tons, "tons", gross,
                    new List<PriceSegmentDto> { new("Per ton per month", g.PricePerTonPerMonthAmount, ListingFormat.InclusiveDays(start, end)) },
                    $"{tons:0.##} t × {ListingFormat.Taka(g.PricePerTonPerMonthAmount)}/ton/month × {ListingFormat.Months(start, end):0.##} month(s)", "BDT"),
                CitationUrl = GodownUrl(id)
            };
        }

        // ---------------------------------------------------------------- Advisory

        private async Task<AgentToolResult> GetCropCalendarAsync(JsonElement a, AgentCaller caller)
        {
            var month = OptInt(a, "month") ?? BangladeshClock.Today.Month;
            if (month is < 1 or > 12) throw new ToolArgumentException("'month' must be 1-12.");
            var district = OptDistrict(a, "district") ?? caller.District;
            var cropName = OptString(a, "crop");

            var crops = await _crops.GetAllCropsAsync();
            var suitable = crops.Where(c => string.IsNullOrWhiteSpace(district) || BangladeshGeo.IsDistrictSuitable(district, c.Division)).ToList();

            CropAdviceDto? advice = null;
            if (cropName is not null)
            {
                var rec = await _crops.GetRecommendationForCropAsync(cropName, district, month);
                if (rec is not null)
                {
                    // The service falls back to an in-season crop when the name matches nothing; flag that to the model.
                    var firstWord = rec.CropName.Split(new[] { ' ', '(' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    var matched = rec.CropName.Contains(cropName, StringComparison.OrdinalIgnoreCase)
                        || (firstWord is not null && cropName.Contains(firstWord, StringComparison.OrdinalIgnoreCase))
                        || string.Equals(rec.ProfileCropName, cropName, StringComparison.OrdinalIgnoreCase)
                        || rec.BanglaCropName.Contains(cropName, StringComparison.OrdinalIgnoreCase);
                    advice = new CropAdviceDto(rec.CropName, rec.BanglaCropName, matched, rec.CurrentPhase,
                        caller.IsBangla && !string.IsNullOrWhiteSpace(rec.AdvisorySummaryBangla) ? rec.AdvisorySummaryBangla : rec.AdvisorySummary,
                        Clip(rec.KeyTips), rec.PopularVarieties, rec.OptimalTemperature, rec.WaterRequirement, rec.IsSuitableDistrict);
                }
            }

            static CropWindowDto Window(CropCalendarEntry c) => new(c.Name, c.BanglaName, c.Season,
                Months(c.SowingMonths), Months(c.GrowingMonths), Months(c.HarvestingMonths), c.PopularVarieties, c.DurationDays);

            return AgentToolResult.Success(new CropCalendarDto(
                month, CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month), district,
                string.IsNullOrWhiteSpace(district) ? null : BangladeshGeo.GetDivision(district), advice,
                suitable.Where(c => c.SowingMonths.Contains(month)).Take(MaxSearchRows).Select(Window).ToList(),
                suitable.Where(c => c.HarvestingMonths.Contains(month)).Take(MaxSearchRows).Select(Window).ToList(),
                "DAE crop calendar (KrishiLink seed data)"));
        }

        /// <summary>
        /// The same transparent scorer the Smart Advisor page uses (ADV-01): the assistant reports its matches and their
        /// factor breakdown, and never substitutes its own recommendation (prompt/improvement.md §8).
        /// </summary>
        private async Task<AgentToolResult> RecommendCropsAsync(JsonElement a, AgentCaller caller)
        {
            var irrigation = OptBool(a, "hasIrrigation") ?? throw new ToolArgumentException("'hasIrrigation' is required. Ask the user whether they have a pump or canal.");
            var input = new CropAdvisoryInput(
                OptDistrict(a, "district") ?? BangladeshGeo.Canonical(caller.District) ?? string.Empty,
                OptOneOf(a, "season", CropAdvisorScorer.SeasonValues) ?? string.Empty,
                OptOneOf(a, "soilType", SoilClassifier.FormOptions.Select(o => o.Value)) ?? "Not sure",
                OptDouble(a, "soilPh"),
                OptDouble(a, "landSizeDecimal"),
                irrigation);

            var result = CropAdvisorScorer.Recommend(await _crops.GetAllCropsAsync(), input, BangladeshClock.Today.Month, _localizer);
            var shown = result.HasStrongMatch ? result.Matches : result.ClosestOptions;
            return AgentToolResult.Success(new CropRecommendationsDto(
                result.Input.District, result.Input.Season, result.HasStrongMatch,
                result.HasStrongMatch ? null : "No crop is a strong fit for these details. Say so plainly; these are only the closest options.",
                shown.Select(m => new CropMatchDto(
                    m.Crop.Name, m.Crop.BanglaName, m.Score,
                    m.Factors.Select(f => new ScoreFactorDto(f.Name, f.Points, f.MaxPoints, f.Reason)).ToList(),
                    m.YieldMinTonnes is { } lo && m.YieldMaxTonnes is { } hi
                        ? $"{lo.ToString("0.#", CultureInfo.InvariantCulture)}-{hi.ToString("0.#", CultureInfo.InvariantCulture)} t{(m.YieldIsPerAcre ? " per acre" : " on the given land")}"
                        : null,
                    m.Crop.Source)).ToList()));
        }

        private async Task<AgentToolResult> GetWeatherForecastAsync(JsonElement a, AgentCaller caller, CancellationToken cancellationToken)
        {
            var district = DistrictOrProfile(a, caller);
            var w = await _weather.GetForecastAsync(district, cancellationToken);
            return AgentToolResult.Success(new WeatherDto(
                district, w.Source.ToString(), w.Source == WeatherSource.Estimated, w.FreshnessLabel,
                w.Temperature, w.Humidity, w.RainProbability, w.WindSpeedKmh, caller.IsBangla ? w.BanglaCondition : w.Condition,
                w.FiveDayForecast.Select(d => new DailyWeatherDto(Day(d.Date), d.DayName, d.MaxTemp, d.MinTemp, d.Humidity, d.RainChance, d.Condition)).ToList()));
        }

        private async Task<AgentToolResult> GetPestAlertsAsync(JsonElement a, AgentCaller caller)
        {
            var district = DistrictOrProfile(a, caller);
            var crop = OptString(a, "crop") ?? (caller.IsInRole(AppRoles.Farmer) ? caller.Specialization : null);
            var weather = await _pests.GetRegionalWeatherAsync(district);
            var alerts = await _pests.EvaluateAlertsAsync(weather, crop);
            var bn = caller.IsBangla;
            return AgentToolResult.Success(new PestAlertsDto(district, crop, weather.Source.ToString(),
                alerts.Select(x => new PestAlertDto(
                    bn && !string.IsNullOrWhiteSpace(x.BanglaName) ? x.BanglaName : x.DiseaseName,
                    x.Category, x.Severity, Math.Round(x.RiskPercentage), x.TargetCrops,
                    bn && !string.IsNullOrWhiteSpace(x.BanglaTriggerExplanation) ? x.BanglaTriggerExplanation : x.TriggerExplanation,
                    Clip(bn && !string.IsNullOrWhiteSpace(x.BanglaSymptoms) ? x.BanglaSymptoms : x.Symptoms),
                    (bn && x.BanglaActionableRemedies.Count > 0 ? x.BanglaActionableRemedies : x.ActionableRemedies).Take(4).ToList())).ToList()));
        }

        private async Task<AgentToolResult> GetWeatherSuggestionsAsync(JsonElement a, AgentCaller caller)
        {
            var district = DistrictOrProfile(a, caller);
            var crop = OptString(a, "crop") ?? (caller.IsInRole(AppRoles.Farmer) ? caller.Specialization : null);
            var s = await _suggestions.GenerateSuggestionForDistrictAndCropAsync(district, crop, BangladeshClock.Today.Month);
            var bn = caller.IsBangla;
            return AgentToolResult.Success(new WeatherSuggestionsDto(
                s.District, bn ? s.BanglaCrop : s.FarmerCrop, bn ? s.BanglaCropStage : s.CropStage, s.OverallRiskLevel,
                bn ? s.BanglaBannerTitle : s.BannerTitle, bn ? s.BanglaBannerMessage : s.BannerMessage,
                s.Suggestions.Take(5).Select(x => new SuggestionDto(
                    bn ? x.BanglaTitle : x.Title, bn ? x.BanglaMessage : x.Message, x.IsCritical,
                    x.ActionUrl.StartsWith('/') ? x.ActionUrl : null)).ToList()));
        }

        // ---------------------------------------------------------------- The caller's own data (A1: ambient identity only)

        private async Task<AgentToolResult> GetMyBookingsAsync(JsonElement a, AgentCaller caller)
        {
            var status = OptOneOf(a, "status", new[] { "all", "pending", "accepted", "paid", "completed", "rejected", "cancelled" }) ?? "all";
            var limit = Math.Clamp(OptInt(a, "limit") ?? 5, 1, 10);
            var history = await _bookings.GetHistoryAsync(caller.UserId, "all", status, null, null, null);
            return AgentToolResult.Success(new MyBookingsDto(history.Bookings.Count,
                history.Bookings.Take(limit).Select(b => new MyBookingDto(
                    b.BookingCode, b.BookingType, b.ItemName, Day(b.StartDate), Day(b.EndDate), b.Status, b.TotalCost,
                    AppLinks.FarmerBookings(b.BookingType, b.Id))).ToList()));
        }

        internal static ProfileContextDto ProfileContext(AgentCaller caller) => new(
            caller.PrimaryRole, caller.District, caller.Division, caller.Specialization,
            caller.IsBangla ? "bn" : "en", Day(BangladeshClock.Today));

        // ---------------------------------------------------------------- Proposals (validate + prefill, never write)

        private async Task<AgentToolResult> ProposeEquipmentRentalAsync(JsonElement a, AgentCaller caller)
        {
            if (Refusal(caller) is { } refused) return refused;
            var id = ReqInt(a, "equipmentId");
            var (start, end) = RequiredRange(a);
            var units = ReqInt(a, "units");
            var note = Clip(OptString(a, "note"), 500);

            var e = await _equipment.GetDetailsAsync(id);
            if (e is null) return NotFound(AgentProposal.EquipmentType, id);
            if (start < BangladeshClock.Today) return AgentToolResult.Fail("invalid_dates", "The start date is in the past. Ask the user for new dates.");
            if (units < 1 || units > e.Quantity)
                return AgentToolResult.Fail("invalid_units", $"This listing has {e.Quantity} unit(s); units must be between 1 and {e.Quantity}.");
            var days = ListingFormat.InclusiveDays(start, end);
            if (days < e.MinRentalDays)
                return AgentToolResult.Fail("too_short", $"This equipment must be rented for at least {e.MinRentalDays} days; the request is {days}.");

            var conflict = await _equipment.CheckAvailabilityAsync(id, start, end, units);
            if (conflict is not null) return AgentToolResult.Fail("unavailable", conflict);
            var quote = await _equipment.QuoteAsync(id, start, end, units);
            if (quote is null || !quote.Ok) return AgentToolResult.Fail("not_quotable", quote?.Error ?? "This period cannot be quoted.");

            var proposal = NewProposal(AgentProposal.EquipmentType, e.Id, e.Name, EquipmentUrl(e.Id), start, end, units, quote.Gross, quote.Description, note);
            proposal.Days = quote.Days;
            proposal.Fields = new Dictionary<string, string>
            {
                ["Id"] = e.Id.ToString(CultureInfo.InvariantCulture),
                ["StartDate"] = Day(start),
                ["EndDate"] = Day(end),
                ["Units"] = units.ToString(CultureInfo.InvariantCulture),
                ["Note"] = note ?? string.Empty
            };
            return ProposalResult(proposal, "units");
        }

        private async Task<AgentToolResult> ProposeGodownStorageAsync(JsonElement a, AgentCaller caller)
        {
            if (Refusal(caller) is { } refused) return refused;
            var id = ReqInt(a, "godownId");
            var (start, end) = RequiredRange(a);
            var tons = OptDouble(a, "tons") ?? throw new ToolArgumentException("'tons' is required.");
            var crop = Clip(OptString(a, "cropType"), 60) ?? throw new ToolArgumentException("'cropType' is required. Ask the user which crop they will store.");
            var note = Clip(OptString(a, "note"), 400);

            var g = await _godowns.GetDetailsAsync(id);
            if (g is null) return NotFound(AgentProposal.GodownType, id);
            if (g.Status == "Inactive") return AgentToolResult.Fail("unavailable", "This storage facility is not accepting bookings right now.");
            if (start < BangladeshClock.Today) return AgentToolResult.Fail("invalid_dates", "The start date is in the past. Ask the user for new dates.");
            if (end <= start) return AgentToolResult.Fail("invalid_dates", "Storage must end after the start date.");
            if (tons <= 0 || tons > g.TotalCapacityTons)
                return AgentToolResult.Fail("invalid_tons", $"This godown holds {g.TotalCapacityTons:0.#} tons in total; tons must be above 0 and at most that.");

            var clash = await _godowns.CheckAvailabilityAsync(id, tons, start, end);
            if (clash is not null) return AgentToolResult.Fail("unavailable", clash);

            var gross = BookingPricing.GodownGross(start, end, tons, g.PricePerTonPerMonthAmount);
            var bookingNote = note is null ? $"Crop: {crop}" : $"Crop: {crop}. {note}";
            var proposal = NewProposal(AgentProposal.GodownType, g.Id, g.Name, GodownUrl(g.Id), start, end, tons, gross,
                $"{tons:0.##} t × {ListingFormat.Taka(g.PricePerTonPerMonthAmount)}/ton/month", note);
            proposal.CropType = crop;
            proposal.Days = ListingFormat.InclusiveDays(start, end);
            proposal.Fields = new Dictionary<string, string>
            {
                ["Id"] = g.Id.ToString(CultureInfo.InvariantCulture),
                ["StartDate"] = Day(start),
                ["EndDate"] = Day(end),
                ["RequestedCapacityTons"] = tons.ToString("0.##", CultureInfo.InvariantCulture),
                ["BookingNotes"] = bookingNote
            };
            return ProposalResult(proposal, "tons");
        }

        private static AgentToolResult? Refusal(AgentCaller caller)
        {
            if (!caller.IsInRole(AppRoles.Farmer))
                return AgentToolResult.Fail("not_available", "Only farmer accounts can request rentals or storage.");
            if (!caller.EmailVerified)
                return AgentToolResult.Fail("email_unverified", "The user must confirm their e-mail before booking. Point them to /Account/VerifyEmail.");
            return null;
        }

        private AgentProposal NewProposal(string type, int id, string name, string url, DateTime start, DateTime end, double quantity, decimal gross, string? pricingNote, string? note) => new()
        {
            Id = Guid.NewGuid(),
            Type = type,
            ListingId = id,
            ListingName = name,
            DetailUrl = url,
            StartDate = start,
            EndDate = end,
            Quantity = quantity,
            QuotedGross = gross,
            PricingNote = pricingNote,
            Note = note,
            FormAction = AgentProposal.FormActions[type],
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_options.ProposalLifetimeMinutes)
        };

        private static AgentToolResult ProposalResult(AgentProposal p, string unit) => new()
        {
            Data = new ProposalDto(p.Id.ToString(), p.Type, p.ListingId, p.ListingName, Day(p.StartDate), Day(p.EndDate), p.Days,
                p.Quantity, unit, p.QuotedGross, p.PricingNote, "BDT",
                "A confirm card with these exact details is now shown to the user. Nothing is booked yet: tell them to review it and press Confirm. " +
                "The owner still has to accept, and payment happens later."),
            Proposal = p,
            CitationUrl = p.DetailUrl
        };

        // ---------------------------------------------------------------- Argument helpers

        private static string? OptString(JsonElement a, string name) =>
            a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString()?.Trim() is { Length: > 0 } s ? s : null;

        private static int? OptInt(JsonElement a, string name)
        {
            if (!a.TryGetProperty(name, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var n) && n == Math.Floor(n) && Math.Abs(n) <= int.MaxValue) return (int)n;
            if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)) return s;
            if (v.ValueKind == JsonValueKind.Null) return null;
            throw new ToolArgumentException($"'{name}' must be a whole number.");
        }

        private static int ReqInt(JsonElement a, string name) => OptInt(a, name) ?? throw new ToolArgumentException($"'{name}' is required.");

        private static double? OptDouble(JsonElement a, string name)
        {
            if (!a.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var n) && double.IsFinite(n)) return n;
            if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) && double.IsFinite(s)) return s;
            throw new ToolArgumentException($"'{name}' must be a number.");
        }

        private static bool? OptBool(JsonElement a, string name)
        {
            if (!a.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
            if (v.ValueKind is JsonValueKind.True or JsonValueKind.False) return v.GetBoolean();
            if (v.ValueKind == JsonValueKind.String && bool.TryParse(v.GetString(), out var s)) return s;
            throw new ToolArgumentException($"'{name}' must be true or false.");
        }

        private static decimal? OptDecimal(JsonElement a, string name) => OptDouble(a, name) is { } d && d > 0 ? (decimal)d : null;

        private static DateTime? OptDate(JsonElement a, string name)
        {
            var text = OptString(a, name);
            if (text is null) return null;
            return DateTime.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d.Date
                : throw new ToolArgumentException($"'{name}' must be a date in {DateFormat} format.");
        }

        private static (DateTime? Start, DateTime? End) OptionalRange(JsonElement a)
        {
            var start = OptDate(a, "startDate");
            var end = OptDate(a, "endDate") ?? start;
            if (start is not null && end < start) throw new ToolArgumentException("'endDate' must be on or after 'startDate'.");
            return (start, end);
        }

        private static (DateTime Start, DateTime End) RequiredRange(JsonElement a)
        {
            var start = OptDate(a, "startDate") ?? throw new ToolArgumentException("'startDate' is required. Ask the user for it rather than guessing.");
            var end = OptDate(a, "endDate") ?? throw new ToolArgumentException("'endDate' is required. Ask the user for it rather than guessing.");
            if (end < start) throw new ToolArgumentException("'endDate' must be on or after 'startDate'.");
            if ((end - start).TotalDays > 366) throw new ToolArgumentException("Date ranges longer than a year are not supported.");
            return (start, end);
        }

        private static string? OptOneOf(JsonElement a, string name, IEnumerable<string> allowed)
        {
            var value = OptString(a, name);
            if (value is null) return null;
            return allowed.FirstOrDefault(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase))
                ?? throw new ToolArgumentException($"'{value}' is not a valid {name}. Use one of the listed values.");
        }

        /// <summary>A district from the fixed 64-name list; legacy spellings (Bogra, Comilla...) are mapped to today's names.</summary>
        private static string? OptDistrict(JsonElement a, string name)
        {
            var value = BangladeshGeo.Canonical(OptString(a, name));
            if (value is null) return null;
            var match = BangladeshGeo.AllDistricts.FirstOrDefault(d => string.Equals(d, value, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;

            var nearest = BangladeshGeo.AllDistricts
                .Select(d => (District: d, Distance: EditDistance(d.ToLowerInvariant(), value.ToLowerInvariant())))
                .Where(x => x.Distance <= 3)
                .OrderBy(x => x.Distance)
                .Take(3)
                .Select(x => x.District)
                .ToList();
            throw new ToolArgumentException(nearest.Count > 0
                ? $"'{value}' is not a Bangladeshi district. Did you mean: {string.Join(", ", nearest)}? Otherwise ask the user."
                : $"'{value}' is not a Bangladeshi district. Ask the user which district they mean.");
        }

        private static int EditDistance(string a, string b)
        {
            var previous = Enumerable.Range(0, b.Length + 1).ToArray();
            for (var i = 1; i <= a.Length; i++)
            {
                var current = new int[b.Length + 1];
                current[0] = i;
                for (var j = 1; j <= b.Length; j++)
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                previous = current;
            }
            return previous[b.Length];
        }

        private static string DistrictOrProfile(JsonElement a, AgentCaller caller) =>
            OptDistrict(a, "district") ?? BangladeshGeo.Canonical(caller.District)
            ?? throw new ToolArgumentException("No district given and the user's profile has none. Ask the user for their district.");

        private static string ListingType(JsonElement a) =>
            OptOneOf(a, "type", new[] { AgentProposal.EquipmentType, AgentProposal.GodownType }) ?? throw new ToolArgumentException("'type' is required.");

        private static AgentToolResult NotFound(string type, int id) =>
            AgentToolResult.Fail("not_found", $"There is no {type} listing with id {id}. Do not invent one; search again instead.");

        private static string EquipmentUrl(int id) => $"/Equipment/Details/{id}";
        private static string GodownUrl(int id) => $"/Godown/Details/{id}";
        private static string Day(DateTime date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);
        private static List<string> Dates(IEnumerable<DateTime> dates) => dates.OrderBy(d => d).Select(Day).Distinct().Take(60).ToList();

        private static string Months(IEnumerable<int> months) =>
            string.Join(", ", months.Where(m => m is >= 1 and <= 12).Select(m => CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(m)));

        private static string Clip(string? text) => Clip(text, MaxTextLength) ?? string.Empty;

        private static string? Clip(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var trimmed = text.Trim();
            return trimmed.Length <= max ? trimmed : trimmed[..max] + "…";
        }

        // ---------------------------------------------------------------- JSON schema helpers

        private static JsonObject Schema(params (string Name, JsonObject Schema, bool Required)[] properties)
        {
            var props = new JsonObject();
            foreach (var p in properties) props[p.Name] = p.Schema;
            var schema = new JsonObject { ["type"] = "object", ["properties"] = props };
            var required = properties.Where(p => p.Required).Select(p => (JsonNode?)JsonValue.Create(p.Name)).ToArray();
            if (required.Length > 0) schema["required"] = new JsonArray(required);
            return schema;
        }

        private static JsonObject Text(string? description, IEnumerable<string>? values = null)
        {
            var schema = new JsonObject { ["type"] = "string" };
            if (description is not null) schema["description"] = description;
            if (values is not null) schema["enum"] = new JsonArray(values.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray());
            return schema;
        }

        private static JsonObject Number(string? description)
        {
            var schema = new JsonObject { ["type"] = "number" };
            if (description is not null) schema["description"] = description;
            return schema;
        }

        private static JsonObject Integer() => new() { ["type"] = "integer" };

        private static JsonObject Boolean(string description) => new() { ["type"] = "boolean", ["description"] = description };

        private static JsonObject Date() => new() { ["type"] = "string", ["description"] = "YYYY-MM-DD" };
    }

    // ---------------------------------------------------------------- Tool result DTOs (A3: public listing data only — no names, contacts, NID or money accounts)

    public sealed record ToolErrorDto(string Error, string Message);

    public sealed record EquipmentSearchDto(int TotalCount, int Page, IReadOnlyList<EquipmentResultDto> Results, string? Note);
    public sealed record EquipmentResultDto(int Id, string Name, string Category, string? District, decimal DailyRate, int MinRentalDays,
        int Quantity, bool IsListed, double ListingRating, double OwnerRating, bool IsVerifiedOwner, bool HasSeasonalRates, string DetailUrl);

    public sealed record GodownSearchDto(int TotalCount, int Page, IReadOnlyList<GodownResultDto> Results, string? Note);
    public sealed record GodownResultDto(int Id, string Name, string StorageType, string? District, decimal PricePerTonPerMonth,
        decimal PricePerTonPerDay, double FreeCapacityTons, double TotalCapacityTons, double ListingRating, double OwnerRating,
        bool IsVerifiedOwner, string DetailUrl);

    public sealed record RateRuleDto(string Name, string Kind, string Applies, decimal DailyRate);
    public sealed record EquipmentDetailDto(int Id, string Name, string Category, string Description, string? District, decimal DailyRate,
        int MinRentalDays, int Quantity, string Status, IReadOnlyList<RateRuleDto> RateRules, IReadOnlyList<string> BlockedOrFullyBookedDates,
        IReadOnlyList<string> PartiallyBookedDates, double ListingRating, int ReviewCount, double OwnerRating, bool IsVerifiedOwner,
        string? LastServiced, string DetailUrl);
    public sealed record GodownDetailDto(int Id, string Name, string StorageType, string Description, string? District, decimal PricePerTonPerMonth,
        string Status, double TotalCapacityTons, double FreeCapacityTodayTons, IReadOnlyList<string> Facilities, IReadOnlyList<string> UnavailableDates,
        double ListingRating, int ReviewCount, double OwnerRating, bool IsVerifiedOwner, string DetailUrl);

    public sealed record AvailabilityDto(string Type, int Id, string StartDate, string EndDate, bool Available, double FreeQuantity,
        string QuantityUnit, IReadOnlyList<string> ConflictingDates, string? Reason);
    public sealed record PriceSegmentDto(string Label, decimal Rate, int Days);
    public sealed record PriceQuoteDto(string Type, int Id, string StartDate, string EndDate, int Days, double Quantity, string QuantityUnit,
        decimal Gross, IReadOnlyList<PriceSegmentDto> Breakdown, string? PricingNote, string Currency);

    public sealed record CropAdviceDto(string Crop, string BanglaName, bool MatchedRequestedCrop, string CurrentPhase, string Summary,
        string KeyTips, string Varieties, string OptimalTemperature, string WaterRequirement, bool SuitableForDistrict);
    public sealed record CropWindowDto(string Crop, string BanglaName, string Season, string SowingMonths, string GrowingMonths,
        string HarvestMonths, string Varieties, string Duration);
    public sealed record CropCalendarDto(int Month, string MonthName, string? District, string? Division, CropAdviceDto? RequestedCrop,
        IReadOnlyList<CropWindowDto> SowThisMonth, IReadOnlyList<CropWindowDto> HarvestThisMonth, string Source);

    public sealed record ScoreFactorDto(string Factor, int Points, int MaxPoints, string Reason);
    public sealed record CropMatchDto(string Crop, string BanglaName, int Score, IReadOnlyList<ScoreFactorDto> Factors, string? Yield, string Source);
    public sealed record CropRecommendationsDto(string District, string Season, bool HasStrongMatch, string? Note, IReadOnlyList<CropMatchDto> Crops);
    public sealed record DailyWeatherDto(string Date, string Day, double MaxC, double MinC, double HumidityPercent, double RainChancePercent, string Condition);
    public sealed record WeatherDto(string District, string Source, bool IsSeasonalEstimate, string Freshness, double TemperatureC,
        double HumidityPercent, double RainChancePercent, double WindKmh, string Condition, IReadOnlyList<DailyWeatherDto> Daily);

    public sealed record PestAlertDto(string Name, string Category, string Severity, double RiskPercent, IReadOnlyList<string> TargetCrops,
        string TriggeredBecause, string Symptoms, IReadOnlyList<string> Remedies);
    public sealed record PestAlertsDto(string District, string? Crop, string WeatherSource, IReadOnlyList<PestAlertDto> Alerts);

    public sealed record SuggestionDto(string Title, string Message, bool IsCritical, string? LinkUrl);
    public sealed record WeatherSuggestionsDto(string District, string Crop, string CropStage, string RiskLevel, string Headline,
        string Summary, IReadOnlyList<SuggestionDto> Suggestions);

    public sealed record MyBookingDto(string BookingCode, string Type, string ListingName, string StartDate, string EndDate, string Status,
        decimal Amount, string Url);
    public sealed record MyBookingsDto(int TotalCount, IReadOnlyList<MyBookingDto> Bookings);

    public sealed record ProfileContextDto(string Role, string? District, string? Division, string? PrimaryCropOrSpecialization,
        string Language, string TodayInDhaka);

    public sealed record ProposalDto(string ProposalId, string Type, int ListingId, string ListingName, string StartDate, string EndDate,
        int Days, double Quantity, string QuantityUnit, decimal QuotedGross, string? PricingNote, string Currency, string Instruction);
}
