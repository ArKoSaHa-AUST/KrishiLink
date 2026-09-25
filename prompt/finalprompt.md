# Master Work Order: Bangladesh-Season Consistency in Crop Advisory
**For: Antigravity autonomous code agent**  
**Project: KrishiLink (`D:\KrishiLink`)**  
**Scope: Backend logic + data consistency fixes + minimal UI enhancement**

---

## §1 Role & Ground Rules

You are an autonomous coding agent named Antigravity. This document is your **complete, self-contained work order**. Treat it as your sole source of truth. You have no access to prior conversation history, no external context to consult, and no authority to deviate from the constraints in §2 below, no matter how reasonable a deviation might seem.

The changes you will make are **narrow, surgical corrections** to make the Crop Advisory feature (Smart Advisor, Crop Calendar, Planner, Pest Alerts, Weather Suggestions) truly Bangladesh-consistent, not broad refactoring. The codebase is 95% correct already — these are bugs in the remaining 5%.

---

## §2 Non-Negotiable Constraints

**File whitelist** — You may touch ONLY these files:
- `BLL/Services/CropAdvisorService.cs` (add shared season helper, update tests)
- `BLL/Services/CropCalendarService.cs` (call shared helper instead of duplicating logic)
- `App_Data/seed/crop-calendar.json` (fix 3 crop season tags)
- `Views/Advisory/Alerts.cshtml` (add season-context badge)
- `Views/Advisory/Suggestions.cshtml` (add season-context badge)
- `Resources/SharedResource.en.resx` (add new string keys)
- `Resources/SharedResource.bn.resx` (add matching Bangla strings)
- `tests/KrishiLink.Tests/CropAdvisorTests.cs` (add/update season tests)
- `.csproj` file: **only if absolutely unavoidable to add a missing test reference** — and only that, nothing else

**Do Not Touch** — Explicitly forbidden:
- The other **~236 currently-modified files** in your working tree (e.g. `AccountController.cs`, `BadgeService.cs`, dozens of views, CSS, Razor partials). The tree is dirty with unrelated work-in-progress. Do not run `git add -A` or `git add .` — it will capture those files. Do not run `dotnet format` repo-wide — it will reformat those files. Do not `git reset --hard` or `git checkout .` — they will discard that work. **Never stage, revert, or touch these files, even if they look related.**
- Any new NuGet package references (a security audit gate will fail the build)
- Any frontend framework (React, Vue, Angular, TypeScript, npm, webpack — they are forbidden per README.md:289)
- Any change to ledger, booking state machine, payment processing, or authentication code
- Any change to CSS variables, layout, or design outside the one specific badge addition in Task 3
- Any change to `SeasonFit` scoring logic in `CropAdvisorService.cs` — the season-as-hard-gate design is intentional and must not be modified

---

## §3 Project Context

**Tech Stack** (from `README.md`):
- Backend: ASP.NET Core MVC, .NET 8 LTS
- ORM: Entity Framework Core + Npgsql against Supabase PostgreSQL
- Frontend: Razor Views, Bootstrap 5.3, Bootstrap Icons, vanilla JavaScript only (no heavy runtime framework)
- Bilingual: UI labels via `IStringLocalizer<SharedResource>` backed by `Resources/SharedResource.{en,bn}.resx` (test enforces key parity and matching placeholder counts); domain content (crop names, tips, remedies) via bilingual entity columns (`BanglaName`, `KeyTipsBn`, etc.)
- Content Security Policy (CSP): Inline `onclick=`/`onchange=` attributes are **blocked** — must use `data-onclick="functionName()"` and register `functionName` in `wwwroot/js/inline-handlers.js` ALLOWED_CALLS array, or CI fails
- CI gates: `dotnet format --verify-no-changes`, `dotnet build`, `dotnet test` (run twice, once with `KRISHILINK_TEST_SHARDED_LOCKS=true`), localization key-parity check, CSP handler-safety check, NuGet vulnerability audit (fails on Critical/High/Moderate), secret scanner, EF migration validation, CodeQL SAST

---

## §4 Ground Truth: Bangladesh Agricultural Seasons (Given, Not to Be Re-Derived)

**Official season boundaries** per the Bangladesh Agro-Meteorological Information Service (BAMIS), Banglapedia, and BARI (Bangladesh Rice Research Institute) published crop calendars:

| Season | Official Boundary | Codebase Simplification | Status |
|---|---|---|---|
| Rabi (Winter Crops) | ~Mid-Oct/Nov – Mid-Mar | Nov – Mar (months 11, 12, 1, 2, 3) | ✓ Correct as-is |
| Kharif-1 (Early Summer) | ~Mid-Mar – Jun/Jul | Apr – Jun (months 4, 5, 6) | ✓ Correct as-is |
| Kharif-2 (Monsoon) | ~Jul – Mid-Oct/Nov | Jul – Oct (months 7, 8, 9, 10) | ✓ Correct as-is |

**The codebase's whole-month approximation (ignoring the fuzzy mid-month boundaries DAE itself tolerates) is intentional and reasonable.** The only problem is that **this same boundary definition is encoded twice in different places with different values**, creating a contradiction. Your job is to unify them, not to redefine them.

**Sources for independent verification:**
- [Bangladesh Agro-Meteorological Information Service (BAMIS) Crop Calendar](https://www.bamis.gov.bd/en/calendar/)
- [Banglapedia — Crop](https://en.banglapedia.org/index.php/Crop)
- BARI published variety guides (referenced in crop entries themselves, e.g. "BARI Morich 1")

---

## §5 Task 1: Unify Duplicate & Contradictory Season Logic

### The Bug

Two different definitions of "which month belongs to which season" exist in the same codebase:

**Definition A** — `BLL/Services/CropAdvisorService.cs:168` (the **canonical, correct** one currently used by the recommendation scorer):
```csharp
public static string SeasonForMonth(int month) => 
    month is >= 4 and <= 6 ? "Kharif-1" : 
    month is >= 7 and <= 10 ? "Kharif-2" : 
    "Rabi";
```
Maps: Kharif-1 = 4–6 (Apr–Jun), Kharif-2 = 7–10 (Jul–Oct), Rabi = 11–3 (Nov–Mar)

**Definition B** — `BLL/Services/CropCalendarService.cs:406-411` (the **buggy, wrong** one):
```csharp
private static string GetCurrentSeasonName(int month) => month switch
{
    11 or 12 or 1 or 2 or 3 => "Rabi Season (রবি মৌসুম - শীতকালীন)",
    4 or 5 or 6 or 7 => "Kharif-1 Season (খরিফ-১ - প্রাক-খরিফ / গ্রীষ্মকালীন)",
    8 or 9 or 10 => "Kharif-2 Season (খরিফ-২ - বর্ষাকালীন)",
    _ => "Rabi Season"
};
```
Maps: Kharif-1 = 4–**7** (Apr–**Jul**, wrong — July should be Kharif-2), Kharif-2 = 8–10 (Aug–Oct)

**The consequence**: The "Current Agricultural Window" label shown on the Crop Calendar page (`Calendar.cshtml:58`) uses Definition B and incorrectly labels July as Kharif-1 instead of Kharif-2.

### The Fix

Create **one canonical, shared season-boundary function** and make both places call it (do not duplicate the logic):

1. **Option A** (recommended): Extend the existing `CropAdvisorService.SeasonForMonth(int month)` method to be public and static, then have `CropCalendarService.GetCurrentSeasonName(int month)` call it:
   - Keep `CropAdvisorService.SeasonForMonth` exactly as-is (it is already correct)
   - Replace the `switch` inside `GetCurrentSeasonName` with a call: `var season = CropAdvisorService.SeasonForMonth(month);` then use that `season` value to construct the localized label
   
2. **Option B** (if Option A causes a circular dependency): Extract the logic into a new file `BLL/Helpers/BangladeshAgriculturalSeason.cs` with a static method `public static string SeasonForMonth(int month)` that both services call

   Choose Option A if possible. Only use Option B if the build fails due to a circular dependency between the two service files.

### Acceptance Criteria

- [ ] **Exact behavior**: month 1→Rabi, 4→Kharif-1, 7→Kharif-2, 10→Kharif-2 (not Kharif-1), 11→Rabi
- [ ] **One canonical source**: Only one place in the codebase defines the month-to-season mapping; the other calls it
- [ ] **Unit test**: Add or update a test in `tests/KrishiLink.Tests/CropAdvisorTests.cs` called `SeasonBoundaryTest` or similar that asserts all 12 months map correctly (e.g. `Assert.Equal("Kharif-1", CropAdvisorService.SeasonForMonth(4))`) — or if that method is now internal, add the test to `CropCalendarPersistenceTests.cs` under a new test class if needed
- [ ] **No other behavior change**: `CropAdvisorService.SeasonFit()` must still work exactly as before (the season-as-hard-gate design is intentional, do not modify it)
- [ ] **CI passes**: `dotnet format --include BLL/Services/CropAdvisor*.cs,BLL/Services/CropCalendar*.cs --verify-no-changes` must pass, `dotnet build` must pass, `dotnet test` must pass

---

## §6 Task 2: Fix 3 Mistagged Crops in Seed Data

### The Bug

Three of 23 crops in `App_Data/seed/crop-calendar.json` have a `"season"` field that contradicts their own `"sowingMonths"` array. Because `CropAdvisorService` uses season as a **hard gate** (mismatch → score 0 → excluded), these crops are wrongly excluded from recommendations during real, valid sowing windows per Bangladesh's official crop calendars (BAMIS, BARI).

Two crops in the same file (`brinjal` at line 693, `sugarcane` at line 868) already show the correct solution: they are tagged `"season": "YearRound"` to handle multi-season crops.

### The Crops to Fix

| Key | Current `season` | `sowingMonths` | Fix | Reason |
|---|---|---|---|---|
| `chili` (lines 601–644) | `"Rabi"` | `[10, 11, 3]` | Change to `"YearRound"` | BAMIS lists 3 separate official sowing windows for BARI chili: Kharif-1 (Feb 15–Mar 15), Kharif-2 (Jul 15–Sep 15), Rabi (Sep–Oct). Genuinely year-round across varieties. |
| `mungbean` (lines 431–472) | `"Kharif-1"` | `[2, 3, 8]` | Change to `"YearRound"` | BARI confirms two distinct official seasons: Kharif-1 (late Feb–mid Mar, "major season") and Kharif-2 (mid-Aug–late Sep). Current single tag captures neither. |
| `groundnut` (lines 956–999) | `"Rabi"` | `[11, 12, 5]` | Change to `"YearRound"` | Confirmed dual-season in Bangladesh: Rabi (mid-Oct–mid-Nov, char land) and Kharif (Jul–Aug). Current tag omits Kharif sowing. |

### The Crop to Leave Alone

| Key | Current `season` | `sowingMonths` | Action | Reason |
|---|---|---|---|---|
| `country-bean` (lines 777–820) | `"Rabi"` | `[6, 7, 8]` | **Do not modify.** Leave as-is. | This one is **correct despite appearing mismatched**. BARI sowing window is Jun 15–Sep 15 (Aug standard), but sheem is classified by its *harvesting* season (Rabi: Nov–Mar), following the same convention as other "Rabi vegetables" (cauliflower, cabbage, tomato). It is not year-round — it has one cycle per year. Retagging to `YearRound` would be wrong. |

### The Fix

For `chili`, `mungbean`, `groundnut` only:
1. Open `App_Data/seed/crop-calendar.json`
2. Locate each entry by its `"key"` field
3. Change `"season": "Rabi"` or `"season": "Kharif-1"` to `"season": "YearRound"`
4. **Change nothing else** in those entries — not `sowingMonths`, not `keyTips`, not any other field
5. Do not modify `country-bean` or any other crop

### Acceptance Criteria

- [ ] `chili` has `"season": "YearRound"`
- [ ] `mungbean` has `"season": "YearRound"`
- [ ] `groundnut` has `"season": "YearRound"`
- [ ] `country-bean` remains `"season": "Rabi"` with `sowingMonths` unchanged
- [ ] No other crops are modified
- [ ] JSON is valid (paste the file into a JSON validator if unsure)
- [ ] No trailing commas, no syntax errors

---

## §7 Task 3: Add Season-Context Badge to Pest Alerts & Weather Suggestions

### The Enhancement

The Crop Calendar page (`Views/Advisory/Calendar.cshtml`) already displays a "Current Agricultural Window" section (lines 48–99) with season context. The Pest Alerts and Weather Suggestions pages lack this context, making it less obvious to users which season's data they are viewing. Add a visually identical season-context badge to those two pages.

### The Badge Markup (from Calendar.cshtml)

Copy this **exact** badge structure from `Views/Advisory/Calendar.cshtml:54-59`:

```razor
<!-- Current Month/Season Badge Row (lines 54–59) -->
<div class="row align-items-center gy-3">
    <div class="col-12 col-lg-7">
        <div class="d-flex align-items-center gap-2 mb-2 flex-wrap">
            <span class="badge bg-warning text-dark px-3 py-1 rounded-pill fw-bold text-uppercase" style="font-size: 0.75rem; letter-spacing: 0.5px;">
                <i class="bi bi-clock-history me-1"></i>@L["Current Agricultural Window"]
            </span>
            <span class="badge bg-white bg-opacity-25 text-white px-2.5 py-1 rounded-pill small">
                @Model.CurrentSeason
            </span>
        </div>
        <!-- ... rest of hero section ... -->
```

### Where to Add It

**`Views/Advisory/Alerts.cshtml`**: After the tab navigation (after line ~54, before the first content card), add a minimal season-context block:
```razor
<!-- Current Season Context -->
<div class="d-flex align-items-center gap-2 mb-3 flex-wrap">
    <span class="badge bg-warning text-dark px-3 py-1 rounded-pill fw-bold text-uppercase" style="font-size: 0.75rem; letter-spacing: 0.5px;">
        <i class="bi bi-clock-history me-1"></i>@L["Current Agricultural Window"]
    </span>
    <span class="badge bg-light text-dark px-2.5 py-1 rounded-pill small">
        @Model.CurrentSeason
    </span>
</div>
```

**`Views/Advisory/Suggestions.cshtml`**: Same as above, in the same position.

### Data Source

- The `CurrentSeason` value must come from the **corrected, shared helper from Task 1** — after your refactor in Task 1, `CropCalendarService.GetCurrentSeasonName()` will call the canonical `SeasonForMonth()` helper and return the correct value
- Ensure the controller action passes `CurrentSeason` to the view model for both Alerts and Suggestions (check `Controllers/AdvisoryController.cs` Alerts and Suggestions actions; they may already populate this field or you may need to add `model.CurrentSeason = CropCalendarService...` before returning the view — confirm by reading the view model class first)

### Localization Strings

If the existing `L["Current Agricultural Window"]` key already exists in both `.resx` files, use it as-is. If not, add this key to both:

**`Resources/SharedResource.en.resx`**:
```xml
<data name="Current Agricultural Window" xml:space="preserve">
  <value>Current Agricultural Window</value>
</data>
```

**`Resources/SharedResource.bn.resx`** (must have identical key, matching value in Bangla):
```xml
<data name="Current Agricultural Window" xml:space="preserve">
  <value>বর্তমান কৃষি সময়কাল</value>
</data>
```

### Acceptance Criteria

- [ ] Alerts page displays the badge (screenshot or inspection)
- [ ] Suggestions page displays the badge (screenshot or inspection)
- [ ] Badge styling is pixel-identical to the Calendar page badge (use browser inspector to confirm class names match exactly)
- [ ] `CurrentSeason` displays the correct season for the current month (e.g., "Rabi Season (রবি মৌসুম - শীতকালীন)" in September)
- [ ] No other layout, CSS, or design change to these two pages — only the badge addition
- [ ] Localization keys exist in both `.en.resx` and `.bn.resx` with matching keys (a test enforces this)
- [ ] CI passes: `dotnet format --include Views/Advisory/Alerts.cshtml,Views/Advisory/Suggestions.cshtml,Resources/*.resx --verify-no-changes`, `dotnet build`, `dotnet test`

---

## §8 Explicit Non-Goals

These are **out of scope**. Do not attempt them:

- **Do not modify `CropAdvisorService.SeasonFit()` scoring logic.** The season-as-hard-gate design (mismatch → score 0 → exclusion) is intentional per `prompt/improvement.md:560`. Softening it (e.g., giving Kharif-1/Kharif-2 mismatch a half-score like the adjacency rule) is a separate, larger behavioral change.
- **Do not build an ML/LLM-based recommendation engine.** Keep the transparent weighted scorer as-is.
- **Do not add an admin portal** to manage crop calendars via UI. Seed data in JSON is sufficient.
- **Do not add SMS, push notifications, or third-party API integrations** for advisory alerts.
- **Do not change ledger, booking state machine, or payment processing code.**
- **Do not change auth code** — Supabase Auth + ASP.NET Identity integration is final.
- **Do not run repo-wide `dotnet format`.** Only format the files you touched.

---

## §9 Known Risk: SeasonFit Behavior Change

### What Changes
After retagging `chili`, `mungbean`, `groundnut` from single-season to `"YearRound"`, the `SeasonFit()` method will treat them as fitting *all* seasons with full credit (line 277 in `CropAdvisorService.cs`):
```csharp
if (crop.Season == "YearRound")
    return Factor(AdvisorFactor.Season, l, weight, FactorState.Met, 
        l["Grown year-round, so it fits any season"]);
```

This is **correct** — these crops genuinely grow across multiple seasons in Bangladesh. However, it means:
- A user asking for "Kharif-1 crops" will now see `chili`, `mungbean`, `groundnut` in the recommendation list (previously hidden by the hard gate)
- Recommendation rankings for users in Feb–Mar or Jul–Aug may shift (new crops become eligible, pushing others out of the top 5)

### What to Do
1. Run `dotnet test` before and after your changes
2. Specifically run `CropAdvisorTests.cs` or a dedicated test that checks crop scoring for a few scenarios (e.g., "Feb farmer in Bogura wanting early summer crops")
3. **Report any surprising ranking shifts** in a comment in your commit message (e.g., "Warning: Kharif-1 recommendations in March now include chili; verify this is expected")
4. Do not silently assume silence = correctness. The shifting rankings are intentional and correct, but flag it so the user is aware

---

## §10 Definition of Done: Verification Checklist

Run these commands exactly, in order, and confirm each passes:

### A. Code Formatting & Build
```bash
cd D:\KrishiLink

# Format only the files you touched (NOT the whole repo)
dotnet format --include "BLL/Services/CropAdvisor*.cs,BLL/Services/CropCalendar*.cs,Views/Advisory/Alerts.cshtml,Views/Advisory/Suggestions.cshtml,tests/KrishiLink.Tests/CropAdvisorTests.cs" --verify-no-changes

# Build
dotnet build

# Test (standard run)
dotnet test

# Test (with shard locking)
set KRISHILINK_TEST_SHARDED_LOCKS=true
dotnet test
```

All must succeed without errors or warnings.

### B. JSON Validation
- Paste `App_Data/seed/crop-calendar.json` into a JSON validator (e.g., https://jsonlint.com/) and confirm valid syntax

### C. Localization Key Parity
- Open both `Resources/SharedResource.en.resx` and `Resources/SharedResource.bn.resx` in a text editor (or use Visual Studio's `.resx` editor)
- Confirm every key you added exists in **both** files with identical key name
- Confirm no key is missing a value in either file

### D. No New Package References
```bash
# Check for changes to .csproj files
git diff "*.csproj"
```
Output must be empty or show only trivial formatting. No new `<PackageReference>` tags.

### E. No New CSP Violations
- Grep for new `onclick=` or `onchange=` attributes (NOT `data-onclick=`):
  ```bash
  grep -r "onclick=\|onchange=" Views/Advisory/*.cshtml
  ```
  Must return zero matches (or only matches in pre-existing code, not your additions)

### F. File Whitelist Compliance
```bash
# Show all modified files
git status --short

# Confirm output shows ONLY:
# - BLL/Services/CropAdvisor*.cs
# - BLL/Services/CropCalendar*.cs
# - App_Data/seed/crop-calendar.json
# - Views/Advisory/Alerts.cshtml
# - Views/Advisory/Suggestions.cshtml
# - Resources/SharedResource.en.resx
# - Resources/SharedResource.bn.resx
# - tests/KrishiLink.Tests/CropAdvisorTests.cs
```

---

## §11 Git Hygiene

### Staging
Do **not** use `git add -A` or `git add .` — these will capture the other ~236 dirty files.

Instead, stage files **explicitly by exact path**:
```bash
git add BLL/Services/CropAdvisorService.cs
git add BLL/Services/CropCalendarService.cs
git add App_Data/seed/crop-calendar.json
git add Views/Advisory/Alerts.cshtml
git add Views/Advisory/Suggestions.cshtml
git add Resources/SharedResource.en.resx
git add Resources/SharedResource.bn.resx
git add tests/KrishiLink.Tests/CropAdvisorTests.cs
```

### Commit Message
```
fix(advisory): unify season logic, correct 3 crop tags, add season badges to Alerts/Suggestions

- Extract season-to-month mapping to one canonical helper (CropAdvisorService.SeasonForMonth)
- Fix duplicate/contradictory GetCurrentSeasonName() in CropCalendarService
- Retag chili, mungbean, groundnut to YearRound per BAMIS/BARI crop calendars
- Add season-context badge to Pest Alerts and Weather Suggestions pages
- Tested: all 12 months map correctly, crop recommendations shift as expected

Co-Authored-By: Claude Haiku 4.5 <noreply@anthropic.com>
```

### Push
Only push to the feature branch you're on — do not force-push, do not merge into main yourself.

---

## §12 Final Self-Check Before Declaring Done

Answer each question with yes or no. If any answer is **no**, fix it and re-run §10 before marking done.

- [ ] **Task 1**: `CropAdvisorService.SeasonForMonth` and `CropCalendarService.GetCurrentSeasonName` are now unified (one calls the other, no duplication)
- [ ] **Task 1**: Month 7 maps to `"Kharif-2"`, not `"Kharif-1"`
- [ ] **Task 1**: Unit test exists that checks all 12 months
- [ ] **Task 2**: `chili` is `"YearRound"`
- [ ] **Task 2**: `mungbean` is `"YearRound"`
- [ ] **Task 2**: `groundnut` is `"YearRound"`
- [ ] **Task 2**: `country-bean` remains `"Rabi"` and is untouched
- [ ] **Task 3**: Pest Alerts page has season badge
- [ ] **Task 3**: Weather Suggestions page has season badge
- [ ] **Task 3**: Badges are byte-identical in class names to Calendar page badge
- [ ] **Task 3**: New localization keys exist in both `.en.resx` and `.bn.resx`
- [ ] **All tasks**: Only the 8 whitelisted files were modified
- [ ] **All tasks**: `dotnet format`, `dotnet build`, `dotnet test` all pass
- [ ] **All tasks**: No new NuGet packages added
- [ ] **All tasks**: No CSP violations (no new `onclick=`/`onchange=`)
- [ ] **Git**: Commit message includes the Co-Authored-By line

If all answers are yes, you are done. Create the commit, push it (do not merge), and notify the user.

---

## Appendix: Why These Fixes Matter

Bangladesh's agricultural seasons are the backbone of crop advisory — they determine planting windows, pest/disease pressure, weather patterns, and harvest timing. When the code says "July is Kharif-1" (wrong) instead of "July is Kharif-2" (right), the hero banner on the calendar is misleading. When a crop's season tag doesn't match its own sowing-months data, the recommendation engine silently excludes a viable crop during its real planting season, making the advisory unhelpful.

These are not cosmetic bugs — they undermine the feature's core value to farmers. Fixing them is the right move.

---

**Sources for independent verification:**
- [Bangladesh Agro-Meteorological Information Service (BAMIS) Crop Weather Calendar](https://www.bamis.gov.bd/en/calendar/)
- [Banglapedia: Crop](https://en.banglapedia.org/index.php/Crop)
- BARI Crop Variety Publications (referenced in seed data, e.g., "BARI Morich 1", "BARI Mung 6", "BARI Chinabadam 8")
