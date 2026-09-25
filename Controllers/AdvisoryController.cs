using System.Security.Claims;
using KrishiLink.BLL.Helpers;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;

namespace KrishiLink.Controllers
{
    /// <summary>
    /// The four Crop Advisory pages. They stay public, but a signed-in farmer sees their own district and crop by default,
    /// and an explicit choice on any page carries over to the others for the rest of the session.
    /// </summary>
    public class AdvisoryController : Controller
    {
        public const string ContextCookie = "krishi.advisory";
        private const string DefaultDistrict = "Bogura";
        private const string DefaultCrop = "Rice (Boro)";

        private readonly ICropCalendarService _cropCalendarService;
        private readonly IPestAlertService _pestAlertService;
        private readonly IWeatherSuggestionService _weatherSuggestionService;
        private readonly ICropAdvisorService _advisor;
        private readonly ISuggestionStateService _suggestionStates;
        private readonly IPestAlertHistoryService _pestHistory;
        private readonly UserManager<ApplicationUser> _users;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public AdvisoryController(
            ICropCalendarService cropCalendarService,
            IPestAlertService pestAlertService,
            IWeatherSuggestionService weatherSuggestionService,
            ICropAdvisorService advisor,
            ISuggestionStateService suggestionStates,
            IPestAlertHistoryService pestHistory,
            UserManager<ApplicationUser> users,
            IStringLocalizer<SharedResource> localizer)
        {
            _cropCalendarService = cropCalendarService;
            _pestAlertService = pestAlertService;
            _weatherSuggestionService = weatherSuggestionService;
            _advisor = advisor;
            _suggestionStates = suggestionStates;
            _pestHistory = pestHistory;
            _users = users;
            _localizer = localizer;
        }

        // ---------------------------------------------------------------- Smart Advisor

        /// <summary>GET: /Advisory — the Smart Advisor form, prefilled with the user's district and the current season.</summary>
        [AllowAnonymous]
        [OfflineCacheable]
        public async Task<IActionResult> Index(string? district = null, string? division = null, bool reset = false)
        {
            var context = await ResolveContextAsync(district, division, null, reset);
            // The farmer's own land unit (REA-03), prefilled with the same 50 decimals the form has always suggested.
            var unit = (User.Identity?.IsAuthenticated == true ? (await _users.GetUserAsync(User))?.PreferredLandUnit : null) ?? LandUnit.Decimal;
            var model = new CropAdvisoryViewModel
            {
                District = context.District,
                Season = CropAdvisorScorer.SeasonForMonth(BangladeshClock.Today.Month),
                LandUnit = unit,
                LandSize = Math.Round(UnitFormat.FromDecimals(50, unit), 2),
                Context = context,
                CanSave = User.IsInRole(AppRoles.Farmer)
            };
            return View(model);
        }

        /// <summary>POST: /Advisory — scores every seeded crop against the farm details.</summary>
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [SlowPath("advisory")]
        public async Task<IActionResult> Index(CropAdvisoryViewModel model)
        {
            model.CanSave = User.IsInRole(AppRoles.Farmer);
            if (!ModelState.IsValid)
            {
                model.Context = await ResolveContextAsync(null, null, null);
                return View(model);
            }

            // The district chosen here becomes the session's advisory context for the other tabs too.
            model.Context = await ResolveContextAsync(model.District, model.Division, null);
            model.Result = await _advisor.RecommendAsync(model.ToInput(), HttpContext.RequestAborted);
            model.District = model.Result.Input.District;
            model.WeatherAlert = await _pestAlertService.GetWeatherAlertNoteAsync(model.Result.Input.District);
            model.HasSubmitted = true;
            return View(model);
        }

        /// <summary>POST: /Advisory/Save — keeps one result, with the inputs that produced it, on the farmer's dashboard.</summary>
        [HttpPost]
        [Authorize(Roles = AppRoles.Farmer)]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> Save(CropAdvisoryViewModel model, int cropId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            if (!ModelState.IsValid || !await _advisor.SaveAsync(userId, model.ToInput(), cropId, HttpContext.RequestAborted))
            {
                TempData["ErrorMessage"] = _localizer["That crop is no longer among the results for these details. Please run the advisor again."].Value;
                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] = _localizer["Crop advice saved to your dashboard."].Value;
            return RedirectToAction("Dashboard", "Farmer");
        }

        // ---------------------------------------------------------------- Calendar, alerts, suggestions

        /// <summary>GET: /Advisory/Calendar — the DAE crop calendar, narrowed to the user's division unless they chose otherwise.</summary>
        [AllowAnonymous]
        [OfflineCacheable]
        public async Task<IActionResult> Calendar(
            string? search = null,
            string? category = null,
            string? season = null,
            string? division = null,
            int? month = null,
            string? stage = null,
            bool reset = false)
        {
            var context = await ResolveContextAsync(null, null, null, reset);
            // Only an absent filter is personalized; "All" chosen explicitly means all of Bangladesh.
            division ??= context.Source == AdvisoryContextSource.Default ? null : BangladeshGeo.GetDivision(context.District);

            var model = await _cropCalendarService.GetCalendarModelAsync(search, category, season, division, month, stage);
            ViewData["AdvisoryContext"] = context;
            return View(model);
        }

        // ---------------------------------------------------------------- Crop Planner (ADV-07)

        /// <summary>GET: /Advisory/Planner — this week on your farm, a sowing-date simulator and a crop comparison.</summary>
        [AllowAnonymous]
        [OfflineCacheable]
        [SlowPath("advisory")]
        public async Task<IActionResult> Planner(string? district = null, string? division = null, int? cropId = null, DateTime? sow = null, int[]? compare = null, bool reset = false)
        {
            var context = await ResolveContextAsync(district, division, null, reset);
            var crops = (await _cropCalendarService.GetAllCropsAsync()).OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
            var today = BangladeshClock.Today;
            var model = new CropPlannerViewModel { Context = context, Crops = crops, SowDate = today };

            // "This week": the farmer's newest saved advice, else the profile crop.
            CropCalendarEntry? weekCrop = null;
            if (User.IsInRole(AppRoles.Farmer) && User.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId
                && (await _advisor.GetSavedAsync(userId, HttpContext.RequestAborted)).FirstOrDefault()?.Crop is { } savedCrop)
            {
                weekCrop = crops.FirstOrDefault(c => c.Id == savedCrop.Id) ?? savedCrop;
                model.ThisWeekFromSavedAdvice = true;
            }
            weekCrop ??= context.HasCrop ? crops.FirstOrDefault(c => string.Equals(c.ProfileCropName, context.Crop, StringComparison.OrdinalIgnoreCase)) : null;
            if (weekCrop is not null) model.ThisWeek = CropPlanner.ThisWeek(weekCrop, today);

            // Simulator: an explicit crop, else the "this week" crop; dates limited to a sensible planning horizon.
            var simCrop = cropId is { } id ? crops.FirstOrDefault(c => c.Id == id) : weekCrop;
            model.SimCropId = simCrop?.Id;
            if (simCrop is not null)
            {
                var sowDate = sow?.Date ?? model.ThisWeek?.NextSowing ?? today;
                if (sowDate < today.AddYears(-1) || sowDate > today.AddYears(2)) sowDate = today;
                model.SowDate = sowDate;
                if (cropId.HasValue) model.Simulation = CropPlanner.Simulate(simCrop, sowDate, today);
            }

            // Comparison: two or three crops side by side, including live pest risk in the user's district.
            var ids = (compare ?? Array.Empty<int>()).Distinct().Take(3).ToList();
            model.CompareIds = ids;
            var chosen = ids.Select(i => crops.FirstOrDefault(c => c.Id == i)).OfType<CropCalendarEntry>().ToList();
            if (chosen.Count >= 2)
            {
                var weather = await _pestAlertService.GetRegionalWeatherAsync(context.District);
                var rules = await _pestAlertService.GetAllDiseaseRulesAsync();
                foreach (var crop in chosen)
                {
                    // The engine matches a crop by name against each rule's target crops: "Boro Rice (HYV & Hybrid)" contains "Rice".
                    model.Comparison.Add(new CropComparisonRow
                    {
                        Crop = crop,
                        Duration = CropPlanner.ParseDuration(crop.DurationDays),
                        RulesCovering = rules.Count(r => r.TargetCrops.Any(t => crop.Name.Contains(t, StringComparison.OrdinalIgnoreCase))),
                        ActiveWarnings = (await _pestAlertService.EvaluateAlertsAsync(weather, crop.Name)).Count
                    });
                }
            }

            return View(model);
        }

        /// <summary>GET: /Advisory/PlannerIcs?cropId=&amp;sow= — the plan's key dates as an iCalendar file for the phone calendar.</summary>
        [AllowAnonymous]
        [HttpGet]
        [EnableRateLimiting(RateLimitPolicies.ReadJson)]
        public async Task<IActionResult> PlannerIcs(int cropId, DateTime? sow = null)
        {
            var crop = await _cropCalendarService.GetCropByIdAsync(cropId);
            if (crop is null) return NotFound();

            var today = BangladeshClock.Today;
            var sowDate = sow?.Date ?? CropPlanner.ThisWeek(crop, today).NextSowing ?? today;
            if (sowDate < today.AddYears(-1) || sowDate > today.AddYears(2)) sowDate = today;

            var plan = CropPlanner.Simulate(crop, sowDate, today);
            var bangla = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "bn";
            string StageName(CropNeedStage stage) => stage switch
            {
                CropNeedStage.LandPreparation => _localizer["Land preparation"].Value,
                CropNeedStage.Harvest => _localizer["Harvest"].Value,
                _ => _localizer["Storage after harvest"].Value
            };
            var ics = CropPlanner.ToIcs(plan,
                bangla && !string.IsNullOrWhiteSpace(crop.BanglaName) ? crop.BanglaName : crop.Name,
                need => $"{_localizer[need.Category].Value} ({StageName(need.Stage)})",
                _localizer["Sowing"].Value,
                _localizer["Harvest window"].Value,
                DateTime.UtcNow);
            return File(System.Text.Encoding.UTF8.GetBytes(ics), "text/calendar; charset=utf-8", $"krishilink-{crop.Key}-{sowDate:yyyyMMdd}.ics");
        }

        /// <summary>GET: /Advisory/Alerts — rule-based pest and disease warnings for the user's district and crop.</summary>
        [AllowAnonymous]
        [OfflineCacheable]
        [SlowPath("advisory")]
        public async Task<IActionResult> Alerts(
            string? district = null,
            string? division = null,
            string? crop = null,
            double? temp = null,
            double? humidity = null,
            string? condition = null,
            bool reset = false)
        {
            var context = await ResolveContextAsync(district, division, crop, reset);
            var safeCondition = SanitizeCondition(condition);
            double? safeTemp = temp.HasValue ? Math.Clamp(temp.Value, -10.0, 60.0) : null;
            double? safeHumidity = humidity.HasValue ? Math.Clamp(humidity.Value, 0.0, 100.0) : null;

            var model = await _pestAlertService.GetPestAlertsDashboardAsync(context.District, PestAlertCrops.ForProfileCrop(context.Crop), safeTemp, safeHumidity, safeCondition);
            // Real forecasts build the district's 14-day history; what-if simulations never do (ADV-06).
            if (!model.IsCustomSimulated)
                await _pestHistory.RecordAsync(context.District, model.Weather, model.ActiveAlerts, HttpContext.RequestAborted);
            ViewData["PestHistory"] = await _pestHistory.GetRecentAsync(context.District, User.FindFirstValue(ClaimTypes.NameIdentifier), cancellationToken: HttpContext.RequestAborted);
            ViewData["AdvisoryContext"] = context;
            return View(model);
        }

        /// <summary>POST: /Advisory/AlertFeedback — "was this accurate?" for today's alert; stored for tuning the rules.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> AlertFeedback(int historyId, bool accurate)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            if (!await _pestHistory.RecordFeedbackAsync(userId, historyId, accurate, HttpContext.RequestAborted))
                return NotFound();
            TempData["SuccessMessage"] = _localizer["Thanks, your feedback helps tune these warnings."].Value;
            return RedirectToAction(nameof(Alerts));
        }

        /// <summary>GET: /Advisory/Suggestions — forecast-driven crop, machinery and storage suggestions.</summary>
        [AllowAnonymous]
        [OfflineCacheable]
        [SlowPath("advisory")]
        public async Task<IActionResult> Suggestions(string? district = null, string? division = null, string? crop = null, int? month = null, bool reset = false)
        {
            var context = await ResolveContextAsync(district, division, crop, reset);
            var suggestionCrop = context.HasCrop ? context.Crop : DefaultCrop;
            var suggestion = await _weatherSuggestionService.GenerateSuggestionForDistrictAndCropAsync(context.District, suggestionCrop, month);
            if (User.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId)
                await _suggestionStates.ApplyAsync(userId, suggestion, HttpContext.RequestAborted);

            var model = new WeatherSuggestionAdvisoryViewModel
            {
                SelectedDistrict = context.District,
                SelectedCrop = suggestionCrop,
                Suggestion = suggestion,
                AllGeneratedItems = suggestion.Suggestions
            };
            ViewData["AdvisoryContext"] = context;
            return View(model);
        }

        /// <summary>POST: /Advisory/SuggestionState — mark a weather nudge done, snooze it for three days, or bring it back.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> SuggestionState(string key, SuggestionStateKind state, bool fromDashboard = false)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            if (!Enum.IsDefined(state) || !await _suggestionStates.SetAsync(userId, key, state, HttpContext.RequestAborted))
                return BadRequest();

            TempData["SuccessMessage"] = _localizer[state switch
            {
                SuggestionStateKind.Done => "Marked as done. It will not show again this month.",
                SuggestionStateKind.Snoozed => "Snoozed for three days.",
                _ => "The suggestion is back on your list."
            }].Value;
            return fromDashboard ? RedirectToAction("Dashboard", "Farmer") : RedirectToAction(nameof(Suggestions));
        }

        /// <summary>GET: /Advisory/WeatherSuggestionsJson — proactive suggestions for client-side filtering.</summary>
        [AllowAnonymous]
        [HttpGet]
        [EnableRateLimiting(RateLimitPolicies.ReadJson)]
        [SlowPath("advisory")]
        public async Task<IActionResult> WeatherSuggestionsJson(string? district, string? division, string? crop, int? month = null)
        {
            var context = await ResolveContextAsync(district, division, crop, persist: false);
            var suggestion = await _weatherSuggestionService.GenerateSuggestionForDistrictAndCropAsync(context.District, context.HasCrop ? context.Crop : DefaultCrop, month);
            return Json(new { success = true, suggestion });
        }

        /// <summary>GET: /Advisory/WeatherAlertsJson — live weather and triggered disease alerts for a district.</summary>
        [AllowAnonymous]
        [HttpGet]
        [EnableRateLimiting(RateLimitPolicies.ReadJson)]
        [SlowPath("advisory")]
        public async Task<IActionResult> WeatherAlertsJson(string? district, string? division)
        {
            var context = await ResolveContextAsync(district, division, null, persist: false);
            var weather = await _pestAlertService.GetRegionalWeatherAsync(context.District);
            var alerts = await _pestAlertService.EvaluateAlertsAsync(weather);
            return Json(new { success = true, weather, alerts });
        }

        /// <summary>
        /// GET: /Advisory/CalendarJson — the whole crop calendar in both languages, small enough for the service worker to
        /// keep so the offline page can still answer "what is sown or harvested this month?" (REA-01).
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        [EnableRateLimiting(RateLimitPolicies.ReadJson)]
        public async Task<IActionResult> CalendarJson()
        {
            var crops = await _cropCalendarService.GetAllCropsAsync();
            return Json(new
            {
                schemaVersion = 1,
                generatedAt = DateTime.UtcNow,
                crops = crops.OrderBy(c => c.Name, StringComparer.Ordinal).Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    nameBn = c.BanglaName,
                    category = c.Category,
                    season = c.Season,
                    sowing = c.SowingMonths,
                    growing = c.GrowingMonths,
                    harvesting = c.HarvestingMonths,
                    duration = c.DurationDays,
                    tip = c.KeyTips,
                    tipBn = c.KeyTipsBn,
                    source = c.Source
                })
            });
        }

        /// <summary>GET: /Advisory/CropDetail/{id} — one crop profile as JSON.</summary>
        [AllowAnonymous]
        [HttpGet]
        [EnableRateLimiting(RateLimitPolicies.ReadJson)]
        public async Task<IActionResult> CropDetail(int id)
        {
            var crop = await _cropCalendarService.GetCropByIdAsync(id);
            if (crop == null)
            {
                return NotFound(new { success = false, message = "Crop entry not found." });
            }

            return Json(new { success = true, data = crop });
        }

        // ---------------------------------------------------------------- Whose advice is this? (ADV-02)

        /// <summary>
        /// Explicit query string, then this session's earlier choice, then the signed-in profile, then the platform default.
        /// An explicit choice is remembered for the session so it survives moving between the four tabs.
        /// </summary>
        private async Task<AdvisoryContextViewModel> ResolveContextAsync(string? district, string? division, string? crop, bool reset = false, bool persist = true)
        {
            if (reset) Response.Cookies.Delete(ContextCookie);

            var chosenDistrict = ValidDistrict(district) ?? ValidDistrict(division);
            var chosenCrop = ValidCrop(crop);
            var fallback = await FallbackContextAsync(useCookie: !reset);

            if (chosenDistrict is null && chosenCrop is null) return fallback;

            var context = new AdvisoryContextViewModel
            {
                District = chosenDistrict ?? fallback.District,
                Crop = chosenCrop ?? fallback.Crop,
                Source = AdvisoryContextSource.Selection,
                IsSignedIn = fallback.IsSignedIn
            };
            if (persist)
            {
                Response.Cookies.Append(ContextCookie, $"{context.District}|{context.Crop}", new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps
                });
            }
            return context;
        }

        private async Task<AdvisoryContextViewModel> FallbackContextAsync(bool useCookie)
        {
            var signedIn = User.Identity?.IsAuthenticated == true;
            if (useCookie && Request.Cookies.TryGetValue(ContextCookie, out var saved) && saved?.Split('|') is [var d, var c]
                && ValidDistrict(d) is { } savedDistrict)
            {
                return new AdvisoryContextViewModel { District = savedDistrict, Crop = ValidCrop(c) ?? "All", Source = AdvisoryContextSource.Selection, IsSignedIn = signedIn };
            }

            if (signedIn && await _users.GetUserAsync(User) is { } user
                && ValidDistrict(user.District ?? OnboardingOptions.GuessDistrict(user.Location)) is { } profileDistrict)
            {
                // Owners record an equipment or storage type as their specialization, which is not a crop.
                var profileCrop = User.IsInRole(AppRoles.Farmer) && OnboardingOptions.Crops.Contains(user.Specialization ?? string.Empty)
                    ? user.Specialization!
                    : "All";
                return new AdvisoryContextViewModel { District = profileDistrict, Crop = profileCrop, Source = AdvisoryContextSource.Profile, IsSignedIn = true };
            }

            return new AdvisoryContextViewModel { District = DefaultDistrict, Crop = "All", Source = AdvisoryContextSource.Default, IsSignedIn = signedIn };
        }

        /// <summary>One of the 64 districts in today's spelling (a division name resolves to its namesake district), else null.</summary>
        private static string? ValidDistrict(string? input)
        {
            var canonical = BangladeshGeo.Canonical(input);
            return string.IsNullOrWhiteSpace(canonical)
                ? null
                : BangladeshGeo.AllDistricts.FirstOrDefault(d => d.Equals(canonical, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>A crop from the onboarding list or the pest-alert filter; anything else is ignored.</summary>
        private static string? ValidCrop(string? input)
        {
            var value = input?.Trim();
            if (string.IsNullOrEmpty(value)) return null;
            return OnboardingOptions.Crops.FirstOrDefault(c => c.Equals(value, StringComparison.OrdinalIgnoreCase))
                ?? PestAlertCrops.Filters.FirstOrDefault(c => c.Equals(value, StringComparison.OrdinalIgnoreCase));
        }

        private static readonly Dictionary<string, string> AllowedConditions = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Cloudy with Showers", "Cloudy with Showers" },
            { "Dense Fog & High Moisture", "Dense Fog & High Moisture" },
            { "Monsoon Rain & High Humidity", "Monsoon Rain & High Humidity" },
            { "Overcast with Light Drizzle", "Overcast with Light Drizzle" },
            { "Warm & Humid with Stagnant Air", "Warm & Humid with Stagnant Air" },
            { "Clear & Dry Skies", "Clear & Dry Skies" }
        };

        private static string? SanitizeCondition(string? input)
        {
            if (!string.IsNullOrWhiteSpace(input))
            {
                var trimmed = input.Trim();
                if (AllowedConditions.TryGetValue(trimmed, out var matchedCondition))
                {
                    return matchedCondition;
                }
            }
            return null;
        }
    }
}
