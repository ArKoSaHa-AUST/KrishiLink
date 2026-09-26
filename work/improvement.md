# Master Prompt — KrishiLink Product Improvements

> **How to use this file.** Paste this whole document into a coding agent working inside the
> KrishiLink repository. This is the **third** prompt in the set and assumes the first two are done
> or in flight:
>
> | File | Scope |
> |---|---|
> | `prompt/loopholefix.md` | Security, correctness, reliability, test coverage. **Do this first.** |
> | `prompt/aiagent.md` | The Groq-powered in-app assistant. |
> | `prompt/improvement.md` | ← this file. Product depth and reach. |
>
> Findings here carry stable IDs (`ADV-01`, `DIS-02`, …). Reference them in commit messages.
> **Section 1 (Crop Advisory) is the priority and should be built first.**

---

## 0. Context and constraints

**Stack — hard constraint, identical to the other two prompts:**

.NET 8 / ASP.NET Core MVC · Razor views · EF Core 8 + Npgsql (Supabase PostgreSQL) · Supabase Auth
& Storage · QuestPDF · QRCoder · **Bootstrap 5.3 + Bootstrap Icons + vanilla JS + jQuery only** ·
`IStringLocalizer<SharedResource>` for EN/BN.

**No React/Vue/Svelte/Angular. No npm, no bundler, no TypeScript toolchain, no CSS framework other
than Bootstrap 5.3.** Everything below is deliberately designed to be buildable with server-rendered
Razor plus progressive enhancement in plain JavaScript.

**Explicit exclusions for this project:**

- ❌ **No admin-portal work.** Do not extend `Controllers/Admin/`, do not build moderation queues,
  content-management screens, or admin dashboards. Where a feature would normally need an admin
  screen, use a **seed file + migration** or a **Development-only** trigger instead, and say so.
- ❌ No new payment provider, no real SMS gateway, no paid third-party service beyond what is
  already configured.

**CI must stay green on every push.** These three checks are the bar:

```
CI Pipeline / Build, Test & Security Audit      ✅
CodeQL Security Analysis / CodeQL SAST Scan     ✅
CD Deploy Pipeline / Deploy to Production       ✅
```

Concretely that means: `dotnet build` at **0 warnings, 0 errors**; `dotnet format --verify-no-changes`
clean; `dotnet ef migrations has-pending-model-changes` clean after every schema change (regenerate
the migration, never hand-edit the snapshot); the secret scanner unaffected; no CodeQL-flagged
pattern introduced (taint into SQL/HTML/redirects, weak crypto, missing CSRF).

**Engineering posture.** Reuse before you write. `BangladeshGeo`, `AppLinks`, `BangladeshClock`,
`PostgresSearch`, `DateRanges`, `WorkflowTransaction`, `IWeatherService`, `ICropCalendarService`,
`IPestAlertService`, `IWeatherSuggestionService`, `INotificationService` already exist and are
good. Most of what follows is **connecting things that are already built but not wired together** —
that is the point. Prefer a 40-line scorer over a 400-line subsystem.

Razor runtime compilation is off: a `.cshtml` edit needs a rebuild **and an app restart**.

---

# 1. Crop Advisory — the priority

## 1.0 What the audit actually found

The Crop Advisory module has four parts. Three are genuinely well built. **One is a mock**, and the
whole module shares one structural gap.

| Part | Route | Service | Verdict |
|---|---|---|---|
| 1. Smart Advisor | `/Advisory` | *(none)* | 🔴 **Entirely hardcoded.** See `ADV-01`. |
| 2. Crop Calendar | `/Advisory/Calendar` | `CropCalendarService` (925 lines) | 🟢 Real. 23 DAE/BARI/BRRI entries with varieties, districts, soil, water, tips, Bangla names. |
| 3. Pest Alerts | `/Advisory/Alerts` | `PestAlertService` (867 lines) | 🟢 Real. ~28 agrometeorological rules with temp/humidity thresholds, consecutive-day evaluation, risk scoring. |
| 4. Weather Suggestions | `/Advisory/Suggestions` | `WeatherSuggestionService` (511 lines) | 🟢 Real. Forecast → growth stage → machinery/storage nudges. |

`WeatherService` (435 lines) underneath all of it is solid: live Open-Meteo → database cache →
seasonal baseline, three-tier fallback, 5-second timeout, `IMemoryCache`, 64-district GPS mapping.

**The structural gap:** `AdvisoryController` **never reads the signed-in user's profile.** There is
no `UserManager`, no `ClaimTypes` lookup anywhere in the file. Every one of the four pages defaults
to district `"Bogura"` and crop `"Rice (Boro)"` — for every user, in every district, forever — even
though `ApplicationUser.District` and `ApplicationUser.Specialization` are collected during
onboarding **for exactly this purpose**. A farmer in Sylhet growing tea is shown Bogura rice
advisories until they manually re-filter, on every page, every visit.

---

## `ADV-01` 🔴 The Smart Advisor returns hardcoded results and ignores every input

**Where:** `Controllers/AdvisoryController.cs` → `PopulateSampleRecommendations`

**What is wrong.** The form at `/Advisory` collects **Location, Season, Soil Type, Soil pH, Land
Size** and an irrigation toggle. `PopulateSampleRecommendations(model)` then discards all of it and
assigns three literal `CropRecommendationItem` objects:

- Boro Rice (BRRI Dhan-89) — **98%** match
- High-Yield Wheat (BARI Gom-33) — **91%** match
- Hybrid Maize (Sunshine-55) — **86%** match

…with hardcoded yield estimates, fertilizer doses and match reasons. Select *Kharif* season, *Sandy*
soil, pH 4.5, no irrigation, in Sylhet — you still get Boro Rice at 98%, with the reason text
*"Optimal match for your clay-loam soil, Rabi season, and assured irrigation access in Rajshahi
division."* The reason is not merely generic; on those inputs it is **factually false**.

To be precise about what *is* real: `model.WeatherAlert` is overwritten immediately afterwards by
`_pestAlertService.GetWeatherAlertNoteAsync(model.Location)`, which genuinely runs live weather
through the rule engine. So the weather strip is real. **The three crop cards are the mock.**

This is the flagship feature, the default landing page of the module, and the thing the README sells
as *"personalized, rule-based crop recommendations."*

**The fix is smaller than it looks, because the data already exists.** `CropCalendarEntry` already
carries every field the scorer needs:

```
Name · BanglaName · ScientificName · Category · Season · ProfileCropName
SowingMonths[] · GrowingMonths[] · HarvestingMonths[]
DurationDays · OptimalTemperature · SoilTypes · WaterRequirement
PopularVarieties · MajorDistricts · Division · KeyTips
```

23 seeded rows × 6 inputs is a scoring problem, not a machine-learning problem.

### Build `BLL/Services/CropAdvisorService.cs` — a transparent weighted scorer

```
ICropAdvisorService
    Task<IReadOnlyList<CropMatch>> RecommendAsync(CropAdvisoryInput input, CancellationToken ct)
```

Score every `CropCalendarEntry` out of 100 from six additive factors. Keep the weights in one
`static readonly` table so they are reviewable and tunable in one place:

| Factor | Weight | Rule |
|---|---:|---|
| **Season fit** | 30 | `entry.Season` matches the selected season → full. Adjacent season (Kharif-1 ↔ Kharif-2) → half. Mismatch → 0. This is the hard gate: a 0 here caps the total at 70 and the crop must not appear in the top 3. |
| **Sowing window** | 20 | `entry.SowingMonths` contains the current Asia/Dhaka month (`BangladeshClock`) → full. Within ±1 month → 12. Otherwise → 0. |
| **Soil fit** | 20 | Token-match the selected soil type against `entry.SoilTypes` (which already holds `"Clay Loam, Alluvial Silt (এঁটেল-দোআঁশ)"` — match on the English tokens, case-insensitive). Exact → full; compatible family (loam↔clay-loam↔silt) → 12; incompatible (sandy for a standing-water crop) → 0. |
| **Irrigation fit** | 15 | Parse `entry.WaterRequirement` into a 3-level enum (`High` / `Medium` / `Low`) once, at seed time, into a new `WaterNeed` column — do **not** re-parse free text per request. `HasIrrigation == false` + `High` → 0 and a visible warning. `false` + `Low` → full. |
| **Regional fit** | 10 | `entry.MajorDistricts` contains the user's district → full. `entry.Division` matches the district's division via `BangladeshGeo` → 6. `Division == "All"` → 5. |
| **pH fit** | 5 | Add a `MinPh`/`MaxPh` pair to the entity and the seed (most Bangladeshi field crops sit at 5.5–7.5; potato 5.0–6.5; jute 6.0–7.5). Inside range → full, within 0.5 → 3, else 0. |

**Non-negotiable output requirements:**

1. **Every score must be explainable.** `CropMatch` carries a `List<MatchFactor>` of
   `(FactorName, Points, MaxPoints, LocalizedReason)`. The card renders a real breakdown —
   *"Season: Rabi ✓ 30/30 · Soil: Clay Loam ✓ 20/20 · No irrigation: this crop needs continuous
   standing water ✗ 0/15"*. A percentage with no derivation is exactly the mock you are replacing.
2. **Return at most 3–5 matches and never pad.** If nothing scores above a floor (say 45), say so
   honestly: *"No strongly suited crop for this combination — the closest options are…"* plus a link
   to the full calendar. Inventing a fourth recommendation is the same sin in a new costume.
3. **Never invent agronomic content.** Fertilizer doses, yields and varieties come from the seeded
   entry or are omitted. Do **not** carry forward the hardcoded `"Urea: 110 kg/acre"` strings — they
   are unsourced. Move them into the seed with a `Source` column (`"DAE"`, `"BARI"`, `"BRRI"`) and
   render the attribution on the card.
4. **Yield and economics** — add `TypicalYieldPerAcreMin/Max` to the entity and seed. Scale by
   `LandSizeDecimal` (100 decimals = 1 acre) so the farmer sees *"≈ 2.6 – 3.0 tonnes on your 50
   decimals"* rather than an abstract per-hectare figure. This feeds `ECO-01`.

**Delete `PopulateSampleRecommendations` entirely.** Leaving it as a fallback guarantees it comes
back.

**Also fix while you are here:** `Index` (GET) calls `GetWeatherAlertNoteAsync` on
`model.Location`'s **default** value when `analyze=true` but no input was posted; and the POST
assigns `model.WeatherAlert` twice (once inside `PopulateSampleRecommendations`, then immediately
overwrites it). Both go away with the rewrite.

**Validation:** the input model has no `[Required]`, no `[Range]` on `SoilPh` (accepts 0 or 14) and
none on `LandSizeDecimal` (accepts negatives). Add them, plus server-side clamping in the service —
the controller already demonstrates this pattern in `SanitizeDistrict`/`SanitizeCrop`.

---

## `ADV-02` 🔴 Nothing is personalized, though the data is already collected

**Where:** `Controllers/AdvisoryController.cs` (all four actions)

Onboarding collects `District` and `Specialization` precisely so advisory can be personal. The
controller never looks at them. Worse, the personalization **already exists one layer down**:
`IWeatherSuggestionService.GetProactiveSuggestionForFarmerAsync(farmerId)` is implemented and used
by `FarmerController` for the dashboard — but `AdvisoryController.Suggestions` calls the generic
`GenerateSuggestionForDistrictAndCropAsync("Bogura", "Rice (Boro)")` instead. The dashboard is
personal; the dedicated advisory page is not.

**Fix:** inject `UserManager<ApplicationUser>` and add one resolver used by all four actions:

```csharp
// explicit query string  >  signed-in profile  >  platform default
private async Task<(string District, string Crop)> ResolveContextAsync(string? d, string? div, string? crop)
```

Keep the pages anonymous-accessible (they are public marketing surface), but when a user *is* signed
in, default to their district and primary crop and show a small chip — *"Showing advice for
**Sylhet** · **Tea** — change"* — so the personalization is visible and overridable. Persist an
explicit override in a cookie for the session so it survives navigation between the four tabs.

---

## `ADV-03` 🟠 "Save this recommendation" is half-built and has a dead database table

Three pieces of an unfinished feature are sitting in the repository:

- `CropAdvisoryViewModel.IsSaved` — set nowhere, read nowhere.
- `FarmerDashboardViewModel.SavedRecommendation` — rendered by `Views/Farmer/Dashboard.cshtml:531`.
- `Models/Entities/CropRecommendation.cs` — mapped to a `DbSet`, **has a real table in three
  migrations**, and is never read or written by any code path.

Note there are **two different types named `CropRecommendation`** — the dead entity and a different
class inside `FarmerDashboardViewModel.cs:69`. That ambiguity will bite whoever finishes this.

**Fix:** finish the loop, it is the highest value-per-line item in the module.

1. Rename the entity to `SavedCropAdvisory` (kills the name collision) and give it real columns:
   `Id · UserId (FK, indexed) · CropCalendarEntryId (FK) · Season · SoilType · SoilPh · LandSizeDecimal
   · HasIrrigation · District · MatchScore · FactorsJson · CreatedAt`. One migration.
2. `POST /Advisory/Save` (authenticated, antiforgery) persists the chosen match **with the inputs
   that produced it** — so the dashboard card can say *"based on your 50 decimals of clay loam in
   Sylhet, saved 12 March"* and can be recomputed later when the weather changes.
3. Wire `FarmerDashboardViewModel.SavedRecommendation` to it. The view already exists.
4. Cap at 3 saved advisories per farmer; newest wins.

---

## `ADV-04` 🟠 Advisory dead-ends instead of leading to the marketplace

The whole commercial premise is *advice → the farmer needs a machine or storage → they rent it here*.
That bridge is mostly missing:

- `Views/Advisory/*.cshtml` contain **zero** links to `/Equipment` or `/Godown`.
- `WeatherSuggestionService` does build action URLs — but as **hardcoded strings**:
  `$"/Equipment?location={...}&category={Uri.EscapeDataString("Power Tiller")}"` — bypassing
  `AppLinks`, the "centralized type-safe URL registry" the README describes as existing to eliminate
  dead links. A route change silently breaks these.

**Fix:**

1. Move every advisory action URL into `AppLinks` (`AppLinks.EquipmentSearch(district, category)`,
   `AppLinks.GodownSearch(district, storageType)`, `AppLinks.CalendarForCrop(crop)`). Grep for
   `"/Equipment?` and `"/Godown?` afterwards; zero hits outside `AppLinks`.
2. Add a **"What you'll need"** strip to each Smart Advisor result card, derived from the crop's
   growth stages: land prep → Power Tiller; harvest month → Combine Harvester; post-harvest → Godown.
   Each chip links to a pre-filtered, date-scoped search. This is a Razor partial plus a small
   crop→equipment-category map, not a subsystem.
3. Add the same strip to Pest Alerts: an active outbreak warning should offer *"Find a Power Sprayer
   in {district}"*.
4. On Crop Calendar, add **"Add to Harvest Plan"** on each crop row — `HarvestPlanService` and the
   multi-item cart already exist; this is the natural funnel from "I'll grow potato in November" to a
   booked tiller and a booked cold store.

---

## `ADV-05` 🟠 Advisory content is only partly bilingual

The seed is genuinely bilingual — `BanglaName`, and Bangla soil terms inside `SoilTypes`. But the
agronomic prose that matters (`KeyTips`, `WaterRequirement`, `PopularVarieties`, the pest rules'
symptom and treatment text) is English-only, and the ~28 `PestDiseaseRule` objects are English
literals in C#. A Bangla-speaking farmer gets a Bangla chrome around English advice — which for this
audience means the feature does not work.

**Fix:**

1. Add `KeyTipsBn`, `WaterRequirementBn`, `SoilTypesBn` to `CropCalendarEntry`; add `NameBn`,
   `SymptomsBn`, `TreatmentBn`, `PreventionBn` to `PestDiseaseRule`. Render by
   `CultureInfo.CurrentUICulture`, falling back to English when a Bangla string is missing (never
   render an empty block).
2. **Move the seed out of C#.** `DbInitializer.SeedCropCalendarAsync` is a ~550-line object literal;
   the pest rules are another ~500 in `PestAlertService`. Move both to
   `App_Data/seed/crop-calendar.json` and `App_Data/seed/pest-rules.json`, versioned with a
   `schemaVersion` field, loaded and upserted idempotently at startup. Agronomic content then gets
   reviewed and corrected by an agronomist editing JSON — with **no admin UI**, which is exactly the
   constraint. Validate the JSON against the expected shape at startup and fail loudly on a bad file.
3. Add a `Source` field (`DAE` / `BARI` / `BRRI` / `BMD`) per entry and render it. Advice a farmer
   will spend money on must be attributable.

---

## `ADV-06` 🟡 Pest alerts are stateless — no history, no acknowledgement, no feedback

`EvaluateAlertsAsync` recomputes from the current forecast on every request. Nothing is stored, so:

- A farmer cannot see *"Late Blight risk was high in your district all last week"*.
- There is no acknowledgement, so the same warning re-renders identically every visit and is quickly
  ignored.
- There is no way to learn whether a rule is firing usefully — the thresholds can never be tuned
  because no outcome is ever recorded.

**Fix (small, high leverage):**

1. `PestAlertHistory` entity: `District · RuleId · Severity · TriggeredOn · WeatherSnapshotJson`.
   Write one row per (district, rule, day) from the existing scheduler — `WeatherSuggestionScheduler`
   already runs on a timer; extend it rather than adding a sixth `IHostedService`. Unique index on
   `(District, RuleId, TriggeredOn)` makes it idempotent, matching the `DedupeKey` pattern already
   used for notifications.
2. Render a 14-day risk sparkline per rule on `/Advisory/Alerts` — an inline SVG in Razor, no chart
   library needed.
3. Add a one-click **"Was this accurate?" 👍/👎** on each alert, stored against the history row.
   Zero admin UI; it is a feedback column that an agronomist queries directly when tuning thresholds.
4. Push **critical** alerts through the existing `INotificationService` with a `DedupeKey` of
   `pest:{district}:{ruleId}:{date}` so the existing unique filtered index prevents spam. A rule
   engine that only fires when the farmer happens to open the page is worth a fraction of one that
   reaches them.

---

## `ADV-07` 🟡 The Crop Calendar is a reference table, not a planning tool

611 lines of view over genuinely good data, but it answers *"when is potato sown?"* and stops. It
cannot answer the questions a farmer actually has: *what do I do this week?*, *what will it cost
me?*, *what happens if I sow two weeks late?*

**Fix, in ascending order of effort:**

1. **"This week on your farm"** — for the signed-in user's saved crop (`ADV-03`) plus the current
   Asia/Dhaka date, derive the current growth stage from `SowingMonths`/`GrowingMonths`/
   `HarvestingMonths` and show the stage-appropriate activity. Pure computation over existing data.
2. **Sowing-date simulator** — a date input; shift the calendar and show the resulting harvest
   window, whether it collides with monsoon (`IWeatherService` seasonal baseline), and the
   equipment/storage dates implied. The single most useful decision-support feature you can add for
   the effort involved.
3. **Crop comparison** — pick 2–3 crops, get a side-by-side table (duration, water need, yield,
   expected gross, pest risk in the user's district). Server-rendered Razor table; no JS framework.
4. **ICS export** — emit a `text/calendar` file of the crop's key dates so it lands in the farmer's
   phone calendar. Roughly 30 lines, no dependency, and it works offline permanently.

---

## `ADV-08` 🟡 Weather Suggestions do not close the loop

`WeatherSuggestionService` is the most commercially aligned part of the module — it already maps
forecast + growth stage to a machinery or storage need. Gaps:

1. `AdvisoryController.Suggestions` ignores the per-farmer overload (`ADV-02`).
2. Suggestions are ephemeral: no dismiss, no snooze, no "done". The same nudge reappears forever.
3. No urgency ordering surfaced — a *"harvest before Thursday's rain"* nudge ranks the same as a
   general tip.
4. The 7-day Open-Meteo forecast is fetched but only shallowly used; there is no *"rain in 3 days →
   your crop is at harvest stage → book a harvester **now**"* chain, which is precisely the platform's
   reason to exist.

**Fix:** add a lightweight `SuggestionState` table (`UserId · SuggestionKey · State(New/Snoozed/Done)
· UpdatedAt`), sort by a computed urgency (days-until-weather-event × stage-criticality), and render
the top 3 prominently with the rest collapsed. Wire the highest-urgency nudge into the farmer
dashboard hero.

---

## `ADV-09` 🟢 Cache the advisory read path

`GetPestAlertsDashboardAsync` and `GenerateSuggestionForDistrictAndCropAsync` re-evaluate ~28 rules
against the forecast on **every** page view and every JSON poll. `WeatherService` caches the
forecast (3 h live / 30 min DB / 15 min fallback) but the rule evaluation itself is not cached.

**Fix:** `IMemoryCache` on `(district, crop, date, month)` with a ~30-minute TTL. The inputs only
change when the forecast refreshes, so this is nearly free and removes a per-request CPU cost.
Do this **after** `loopholefix.md → SEC-08` adds rate limiting, so the two are sized together.

---

# 2. Discovery and marketplace

## `DIS-01` 🟠 Search is `ILIKE '%term%'` — it cannot find anything in Bangla or with a typo

`BLL/Helpers/PostgresSearch.cs` is six lines: escape `\ % _`, wrap in `%…%`. That is the entire
search capability. Consequences: no ranking (results are ordered by whatever the outer query says),
no typo tolerance (`"harvestor"` → 0 results), **no Bangla matching at all** (a Bangla-UI user
searching `ধান` gets nothing, because listing titles are stored in English), and `%term%` cannot use
a B-tree index, so every search is a sequential scan that degrades linearly with listing count.

**Fix — all of it is native PostgreSQL, already available on Supabase:**

1. Enable `pg_trgm` (migration: `CREATE EXTENSION IF NOT EXISTS pg_trgm`). Add GIN trigram indexes
   on `Equipment.Name`, `Equipment.Description`, `Godown.Name`, `Godown.Description`. `ILIKE` becomes
   index-backed immediately — a one-migration win with no code change.
2. Add a `tsvector` column (generated, stored) over name + description + category + district, with a
   GIN index. Use the **`simple`** text-search configuration, not `english` — Bangla has no stemmer
   in PostgreSQL, and `simple` at least tokenizes it correctly.
3. Rank with `ts_rank_cd`, and fall back to trigram `similarity()` above a 0.3 threshold when
   full-text returns nothing. That gives "did you mean" behaviour for free.
4. **Bangla ↔ English synonyms.** Add a small curated map (`ধান`↔`rice`/`paddy`, `ট্রাক্টর`↔`tractor`,
   `গুদাম`↔`godown`/`warehouse`, `হিমাগার`↔`cold storage`) as a seed JSON file, expanded into the
   query before it hits the database. ~40 entries covers the realistic vocabulary of this
   marketplace. No admin UI — it is a seed file (`ADV-05` §2 pattern).

## `DIS-02` 🟡 No map view, despite latitude/longitude being indexed

`Equipment` and `Godown` both store `Latitude`/`Longitude` with a composite index
(`ApplicationDbContext.cs:77, 131`), and `GeoLocationHelper` holds verified centroids for all 64
districts. Nothing renders a map. "Show me what's near me" is the most natural query in a rental
marketplace and it is currently unanswerable.

Note what `GeoLocationHelper` is and is not: it is a **coordinate dictionary and navigation-URL
helper**. There is **no distance function anywhere in the codebase** — grep for `6371`, `Haversine`
or `Math.Acos` returns nothing. Proximity search has to be added, and it must be added **in SQL**, not
in C#, or you will materialize every listing per request and throw the index away.

**Fix:**

1. Enable the `cube` and `earthdistance` extensions (migration:
   `CREATE EXTENSION IF NOT EXISTS cube; CREATE EXTENSION IF NOT EXISTS earthdistance;`) and add a
   GiST index on `ll_to_earth("Latitude", "Longitude")` for both tables. Filter with
   `earth_box(...)` plus an exact `earth_distance(...)` check — index-backed, and it keeps paging
   and sorting inside the database. If extensions are unavailable on the plan, fall back to a
   bounding-box prefilter on the existing `(Latitude, Longitude)` index followed by a Haversine
   expression in the projection; do not skip the prefilter.
2. Render with Leaflet + OpenStreetMap tiles — one `<script>` + one `<link>` from a CDN, no npm, no
   framework, free, no API key. Add a map/list toggle on `/Equipment` and `/Godown` driven by the
   existing filter endpoints, plus a "within N km" control.
3. Adding a CDN origin and tile server means widening the CSP from `loopholefix.md → SEC-05`
   (`script-src`, `style-src`, and `img-src` for the tiles). Coordinate the two — do not add
   `'unsafe-inline'` to make Leaflet work.

## `DIS-03` 🟡 No price context for either side

Neither a farmer nor an owner can tell whether ৳3,500/day is fair. The data to answer it is already
in the database.

**Fix:** a `PriceBenchmarkService` computing median/p25/p75 per (category, district, month) over
**completed** bookings. Show farmers *"৳3,500/day — about average for tractors in Bogura (৳2,900–
৳4,100)"*; show owners the same on their pricing page. Require a minimum sample (say 5 bookings)
before displaying anything, and say "not enough data yet" otherwise — a benchmark from two data
points is worse than none.

---

# 3. Farmer-side economics

## `ECO-01` 🟠 Owners get full P&L; farmers get nothing

The platform has `OwnerRevenueService`, expense categories, fiscal-year P&L and monthly QuestPDF
statements — **for owners**. A farmer, who is the one actually spending money, has no view of what a
season costs or returns. This is the largest unserved need in the product outside advisory.

**Fix — build it on top of `ADV-01`'s yield data and the existing booking records:**

1. **Season Cost Sheet.** For a saved advisory (`ADV-03`) or a harvest plan, sum the booked rental
   and storage costs, let the farmer add non-platform costs (seed, fertilizer, labour) reusing the
   existing `BookingExpense` category pattern, and show cost per decimal and per acre.
2. **Expected return.** `TypicalYieldPerAcreMin/Max` (added in `ADV-01`) × land size × a market price
   the farmer enters → expected gross, net, and break-even yield. Be explicit that the price is the
   farmer's own input, not a market feed — do not imply a data source you do not have.
3. **Season summary PDF** via QuestPDF. `ReceiptDocumentService`, `PdfStyle` and
   `MonthlyStatementDocument` already establish the pattern; this is a fourth document, not new
   infrastructure.

## `ECO-02` 🟡 Harvest Plan does not know about the crop calendar

`HarvestPlanService` (multi-item cart, cloning, two-pass submission) is strong engineering, but plan
dates are entered by hand with no agronomic awareness. A farmer can plan a combine harvester for a
month when their crop is not harvestable.

**Fix:** when a plan is linked to a crop, warn on items whose dates fall outside the crop's stage
windows — *"Combine harvester booked for August, but your Aman rice harvests in November–December"*.
A warning, never a block: the farmer knows their field better than a 23-row seed table does.

---

# 4. Reach — this is a rural Bangladesh product

## `REA-01` 🟠 No offline support at all

Target users are on intermittent 2G/3G in rural districts. `wwwroot/` has **no `manifest.json` and no
service worker**. The crop calendar — static reference data that changes a few times a year — is
re-fetched over the network every single time.

**Fix — a PWA is pure vanilla JS and a JSON file, entirely inside the stack constraint:**

1. `wwwroot/manifest.json` + maskable icons → installable to the home screen. Large adoption gain
   for near-zero effort.
2. `wwwroot/sw.js`: **cache-first** for `/css`, `/js`, `/lib`, icons and the crop-calendar JSON;
   **network-first with cache fallback** for advisory pages; **never cache** anything authenticated,
   money-related, or under `/Account`, `/Bookings`, `/Verify`. Get that exclusion list right — a
   cached booking status is a support incident.
3. An offline fallback page that still renders the cached crop calendar and the last-seen forecast,
   clearly stamped *"showing data from {date}"*.
4. Register the worker only over HTTPS and only outside Development (the LAN dev setup is plain HTTP;
   see the `App:ListenUrls` note in `Program.cs`).

## `REA-02` 🟠 Images are served full-size and mostly not lazy-loaded

`SupabaseStorageClient.PublicObjectUrl` returns `object/public/...` — the **original** upload, up to
5 MB, with no resizing. Only **4 of 19** views with `<img>` use `loading="lazy"`. On a 2G connection
a listing grid is effectively unusable.

**Fix, in order of cost:**

1. Add `loading="lazy"` plus explicit `width`/`height` to every `<img>` (the dimensions also kill
   layout shift). Free, do it today.
2. If the Supabase plan includes image transformations, add a `PublicObjectUrl(key, width, quality)`
   overload emitting `render/image/public/{bucket}/{key}?width=400&quality=70` and use it for every
   grid thumbnail. **Check the plan first** — the render endpoint is not available on all tiers, and
   a 404 image is worse than a slow one.
3. If it is not available, generate a 400px WebP variant at upload time in `FileStorageService` and
   store it beside the original as `{key}_thumb.webp`. That needs an image library; `SixLabors.ImageSharp`
   is the standard choice and its licence model matches QuestPDF, which the project already accepts.
4. Accept WebP on upload — `FileStorageService.AllowedExtensions` already lists `.webp`, so this is
   just encouraging it in the UI copy.

## `REA-03` 🟡 Weights and measures do not match how farmers speak

The UI mixes decimals, acres, hectares, tonnes and kg. Rural Bangladesh uses **bigha**, **katha**,
**shotangsho/decimal** and **maund (40 kg)**. A farmer who thinks in bigha must convert in their head
before they can trust a number.

**Fix:** a `UnitFormat` helper plus a per-user preferred-unit setting on the profile (default:
decimal/shotangsho, which the codebase already leans on). Render the local unit with the metric value
in parentheses. Note the regional catch and handle it explicitly: a bigha is **33 decimals** in most
of Bangladesh, and the helper should document that rather than silently pick one.

---

# 5. Platform quality

| ID | Item | Why |
|---|---|---|
| `QLT-01` | **Dead code sweep.** `CropRecommendation` entity (a table in three migrations, zero reads/writes — resolved by `ADV-03`), `OwnerRevenueService.RequestPayout` sync wrapper (covered by `loopholefix.md → REL-03`), `CropAdvisoryViewModel.PreviousCrop` (in the model, absent from the form). Delete with a migration; do not leave orphan tables. |
| `QLT-02` | **Structured logging + timings.** No request timing, no per-service metrics. Add `ILogger` scopes with a correlation id and log slow paths (advisory evaluation, search, booking workflow) above a threshold. Prerequisite for knowing whether `ADV-09` and `DIS-01` actually helped. |
| `QLT-03` | **Email deliverability.** `loopholefix.md → SEC-01` covers configuring custom SMTP. Follow through: SPF/DKIM/DMARC on the sending domain, and an `EmailDeliveryLog` so a farmer asking "I never got my receipt" is answerable. |
| `QLT-04` | **Seed data as versioned JSON.** Generalize `ADV-05` §2: crop calendar, pest rules, districts, search synonyms, equipment categories all move to `App_Data/seed/*.json` with a `schemaVersion` and idempotent upsert. This is the project's substitute for an admin CMS, and it satisfies the no-admin constraint properly. |
| `QLT-05` | **Empty and error states.** Several JSON endpoints return bare `{}` and several grids render nothing when empty. Every list needs an empty state with a next action, and every AJAX failure needs a visible message (see `loopholefix.md → QUA-03`). |

---

# 6. Sequencing

Ship in this order. Each phase is independently releasable and leaves CI green.

| Phase | Contents | Rationale |
|---|---|---|
| **0 — Prerequisite** | `prompt/loopholefix.md` Phases 1–2 | Tests and the authorization baseline must exist first. Everything below adds surface area; adding it to an untested, opt-in-authorization codebase compounds the problem. |
| **1 — Make the advisory real** | `ADV-01`, `ADV-02`, `ADV-03` | The flagship feature currently returns fabricated results. Nothing else in this document matters as much. `ADV-02` is ~40 lines. `ADV-03` finishes a feature that is already two-thirds present. |
| **2 — Close the loop** | `ADV-04`, `ADV-08`, `ADV-05` | Turn advice into bookings and make the advice legible in Bangla. This is where advisory starts paying for itself. |
| **3 — Deepen advisory** | `ADV-06`, `ADV-07`, `ADV-09` | History, planning tools and caching. `ADV-07` §2 (sowing-date simulator) is the standout. |
| **4 — Discovery** | `DIS-01`, `DIS-02`, `DIS-03` | `DIS-01` step 1 (trigram indexes) is one migration for a large, immediate win. |
| **5 — Farmer economics** | `ECO-01`, `ECO-02` | Depends on `ADV-01`'s yield data and `ADV-03`'s saved advisories. |
| **6 — Reach** | `REA-01`, `REA-02`, `REA-03` | High user impact, low technical risk, best done once the content it caches has stabilized. |
| **7 — Quality** | `QLT-01`…`QLT-05` | Continuous; fold into the phases above where they touch the same files. |

---

# 7. Definition of done

- [ ] `dotnet build` — 0 warnings, 0 errors.
- [ ] `dotnet test` — green, including new tests for the advisory scorer (see below).
- [ ] `dotnet format --verify-no-changes` — clean.
- [ ] `dotnet ef migrations has-pending-model-changes` — clean.
- [ ] All three CI checks green: CI Pipeline, CodeQL, CD Deploy.
- [ ] **No hardcoded recommendation, yield, fertilizer or price string survives anywhere in
      `Controllers/` or `BLL/`.** Grep for the mock's literals (`"BRRI Dhan-89"`, `"Sunshine-55"`,
      `"Urea: 110 kg/acre"`) — zero hits outside the seed files.
- [ ] Every advisory number a farmer sees is derived from seeded data or live weather, and is
      explainable on screen via its factor breakdown.
- [ ] Every new user-facing string exists in **both** `SharedResource.en.resx` and
      `SharedResource.bn.resx`; every new agronomic field has a Bangla counterpart with an English
      fallback.
- [ ] No new admin screen, controller or route.
- [ ] Advisory URLs all route through `AppLinks` — zero raw `"/Equipment?"` / `"/Godown?"` literals.

**Tests to add** (into the `KrishiLink.Tests` project created by `loopholefix.md → REL-01`):

- The scorer is deterministic: identical inputs → identical scores.
- A season mismatch can never place a crop in the top 3.
- `HasIrrigation = false` + a `High` water-need crop scores 0 on that factor and surfaces the warning.
- Factor points always sum to the displayed total (the explanation cannot drift from the score).
- With inputs that match nothing, the service returns the honest empty result — it never pads.
- Unit conversions round-trip: decimal ↔ acre ↔ bigha (33 decimals), kg ↔ maund (40 kg).
- Search: a Bangla synonym returns the English-titled listing; a one-character typo still ranks the
  right listing first.

---

# 8. Out of scope

- ❌ **Any admin portal work.** Content changes go through versioned seed JSON + migration.
- ❌ Frontend frameworks, npm, bundlers, TypeScript, CSS frameworks other than Bootstrap 5.3.
- ❌ Machine learning for crop recommendation. A transparent weighted scorer over 23 curated DAE
  entries is more accurate, more explainable and more defensible to an agronomist than a model
  trained on data this project does not have. Do not replace `ADV-01` with an LLM call either — the
  AI agent in `prompt/aiagent.md` should *call* this scorer as a tool, not substitute for it.
- ❌ Real-time market price feeds, SMS/IVR channels, satellite or soil-sensor integration. All are
  reasonable someday; none are reachable without a data partner, and inventing the data is exactly
  the failure `ADV-01` exists to correct.
- ❌ Changing the ledger model, the booking state machine, or the 3-tier architecture.
