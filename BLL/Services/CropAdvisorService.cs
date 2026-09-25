using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using KrishiLink.BLL.Helpers;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace KrishiLink.BLL.Services
{
    /// <summary>What the farmer told the Smart Advisor.</summary>
    public sealed record CropAdvisoryInput(
        string District,
        string Season,
        string SoilType,
        double? SoilPh,
        double? LandSizeDecimal,
        bool HasIrrigation);

    public enum AdvisorFactor
    {
        Season,
        SowingWindow,
        Soil,
        Irrigation,
        Region,
        Ph
    }

    public enum FactorState
    {
        Met,
        Partial,
        Missed
    }

    /// <summary>One line of a score's derivation: how many of the factor's points the crop earned, and why.</summary>
    public sealed record MatchFactor(AdvisorFactor Factor, string Name, int Points, int MaxPoints, FactorState State, string Reason, bool IsWarning = false);

    public sealed class CropMatch
    {
        public required CropCalendarEntry Crop { get; init; }
        public required IReadOnlyList<MatchFactor> Factors { get; init; }

        /// <summary>Always the sum of the factor points — the explanation cannot drift from the number.</summary>
        public int Score => Factors.Sum(f => f.Points);

        public IEnumerable<MatchFactor> Warnings => Factors.Where(f => f.IsWarning);

        /// <summary>Indicative harvest for the farmer's land, in tonnes; null when the seed has no yield for this crop.</summary>
        public double? YieldMinTonnes { get; init; }
        public double? YieldMaxTonnes { get; init; }

        /// <summary>True when no land size was given and the yield is per acre.</summary>
        public bool YieldIsPerAcre { get; init; }
    }

    public sealed class CropAdvisoryResult
    {
        public required CropAdvisoryInput Input { get; init; }
        public int Month { get; init; }

        /// <summary>Crops that fit the season and clear <see cref="CropAdvisorScorer.StrongMatchFloor"/>, best first. Never padded.</summary>
        public IReadOnlyList<CropMatch> Matches { get; init; } = Array.Empty<CropMatch>();

        /// <summary>When nothing clears the floor: the nearest season-compatible crops, shown explicitly as weak options.</summary>
        public IReadOnlyList<CropMatch> ClosestOptions { get; init; } = Array.Empty<CropMatch>();

        public bool HasStrongMatch => Matches.Count > 0;
    }

    public enum SoilFamily
    {
        Sandy,
        SandyLoam,
        Loam,
        SiltLoam,
        Silt,
        ClayLoam,
        Clay
    }

    /// <summary>Maps soil descriptions to a small set of families so "Sandy Loam, River Char sandbeds" can be compared with a form choice.</summary>
    public static class SoilClassifier
    {
        /// <summary>The values the Smart Advisor form posts, in display order.</summary>
        public static readonly IReadOnlyList<(string Value, SoilFamily? Family)> FormOptions = new (string, SoilFamily?)[]
        {
            ("Clay Loam", SoilFamily.ClayLoam),
            ("Loam", SoilFamily.Loam),
            ("Sandy Loam", SoilFamily.SandyLoam),
            ("Silt Loam", SoilFamily.SiltLoam),
            ("Alluvial / Silt", SoilFamily.Silt),
            ("Clay", SoilFamily.Clay),
            ("Sandy", SoilFamily.Sandy),
            ("Not sure", null)
        };

        // Neighbouring textures: workable, though not what the crop prefers.
        private static readonly HashSet<(SoilFamily, SoilFamily)> Neighbours = new[]
        {
            (SoilFamily.Sandy, SoilFamily.SandyLoam),
            (SoilFamily.SandyLoam, SoilFamily.Loam),
            (SoilFamily.Loam, SoilFamily.SiltLoam),
            (SoilFamily.Loam, SoilFamily.ClayLoam),
            (SoilFamily.SiltLoam, SoilFamily.Silt),
            (SoilFamily.SiltLoam, SoilFamily.ClayLoam),
            (SoilFamily.Silt, SoilFamily.ClayLoam),
            (SoilFamily.ClayLoam, SoilFamily.Clay)
        }.SelectMany(p => new[] { p, (p.Item2, p.Item1) }).ToHashSet();

        public static SoilFamily? FromFormValue(string? value) =>
            FormOptions.FirstOrDefault(o => string.Equals(o.Value, value?.Trim(), StringComparison.OrdinalIgnoreCase)).Family;

        public static string NormalizeFormValue(string? value) =>
            FormOptions.FirstOrDefault(o => string.Equals(o.Value, value?.Trim(), StringComparison.OrdinalIgnoreCase)).Value ?? "Not sure";

        /// <summary>Families named in a seed description; only the English part (before any Bangla in brackets) is read.</summary>
        public static IReadOnlySet<SoilFamily> Parse(string? soilTypes)
        {
            var families = new HashSet<SoilFamily>();
            var english = (soilTypes ?? string.Empty).Split('(')[0];
            foreach (var part in english.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var token = part.ToLowerInvariant();
                SoilFamily? family =
                    token.Contains("clay loam") ? SoilFamily.ClayLoam
                    : token.Contains("silt loam") ? SoilFamily.SiltLoam
                    : token.Contains("sandy loam") ? SoilFamily.SandyLoam
                    : token.Contains("silt") ? SoilFamily.Silt
                    : token.Contains("loam") ? SoilFamily.Loam
                    : token.Contains("sand") || token.Contains("char") || token.Contains("riverbed") ? SoilFamily.Sandy
                    : token.Contains("clay") ? SoilFamily.Clay
                    : null;
                if (family is { } f) families.Add(f);
            }
            return families;
        }

        public static bool AreNeighbours(SoilFamily a, SoilFamily b) => Neighbours.Contains((a, b));
    }

    /// <summary>
    /// The Smart Advisor: a transparent weighted scorer over the seeded DAE crop calendar. Six additive factors, weights in
    /// one table, and every point explained on screen. Deterministic: the same input and month always give the same result.
    /// </summary>
    public static class CropAdvisorScorer
    {
        public static readonly IReadOnlyDictionary<AdvisorFactor, int> Weights = new Dictionary<AdvisorFactor, int>
        {
            [AdvisorFactor.Season] = 30,
            [AdvisorFactor.SowingWindow] = 20,
            [AdvisorFactor.Soil] = 20,
            [AdvisorFactor.Irrigation] = 15,
            [AdvisorFactor.Region] = 10,
            [AdvisorFactor.Ph] = 5
        };

        /// <summary>Below this a crop is not recommended; the result says so instead of padding.</summary>
        public const int StrongMatchFloor = 45;
        public const int MaxMatches = 5;
        public const int MaxClosestOptions = 3;

        public static readonly IReadOnlyList<string> SeasonValues = new[] { "Rabi", "Kharif-1", "Kharif-2" };

        /// <summary>The cropping season a month falls in (Rabi Nov-Mar, Kharif-1 Apr-Jun, Kharif-2 Jul-Oct).</summary>
        public static string SeasonForMonth(int month) => month is >= 4 and <= 6 ? "Kharif-1" : month is >= 7 and <= 10 ? "Kharif-2" : "Rabi";

        public static string NormalizeSeason(string? season, int month)
        {
            var value = season?.Trim() ?? string.Empty;
            return SeasonValues.FirstOrDefault(s => value.StartsWith(s, StringComparison.OrdinalIgnoreCase)) ?? SeasonForMonth(month);
        }

        /// <summary>Clamps and canonicalizes everything a form or tool call can send.</summary>
        public static CropAdvisoryInput Normalize(CropAdvisoryInput input, int month)
        {
            var district = BangladeshGeo.Canonical(input.District);
            district = BangladeshGeo.AllDistricts.FirstOrDefault(d => string.Equals(d, district, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
            double? ph = input.SoilPh is { } p && double.IsFinite(p) ? Math.Round(Math.Clamp(p, 3.5, 9.5), 1) : null;
            double? land = input.LandSizeDecimal is { } l && double.IsFinite(l) && l > 0 ? Math.Round(Math.Min(l, 100_000), 1) : null;
            return new CropAdvisoryInput(district, NormalizeSeason(input.Season, month), SoilClassifier.NormalizeFormValue(input.SoilType), ph, land, input.HasIrrigation);
        }

        public static CropAdvisoryResult Recommend(IEnumerable<CropCalendarEntry> crops, CropAdvisoryInput rawInput, int month, IStringLocalizer localizer)
        {
            var input = Normalize(rawInput, month);
            var scored = crops
                .Select(crop => Score(crop, input, month, localizer))
                // The season is the hard gate: a crop that cannot be grown in the chosen season is never offered.
                .Where(m => m.Factors.Single(f => f.Factor == AdvisorFactor.Season).Points > 0)
                .OrderByDescending(m => m.Score)
                .ThenBy(m => m.Crop.Name, StringComparer.Ordinal)
                .ToList();

            var strong = scored.Where(m => m.Score >= StrongMatchFloor).Take(MaxMatches).ToList();
            return new CropAdvisoryResult
            {
                Input = input,
                Month = month,
                Matches = strong,
                ClosestOptions = strong.Count > 0 ? Array.Empty<CropMatch>() : scored.Take(MaxClosestOptions).ToList()
            };
        }

        public static CropMatch Score(CropCalendarEntry crop, CropAdvisoryInput input, int month, IStringLocalizer l)
        {
            var bangla = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "bn";
            var factors = new List<MatchFactor>
            {
                SeasonFit(crop, input, l),
                SowingFit(crop, month, l),
                SoilFit(crop, input, bangla, l),
                IrrigationFit(crop, input, l),
                RegionFit(crop, input, l),
                PhFit(crop, input, l)
            };

            double? yieldMin = null, yieldMax = null;
            if (crop.TypicalYieldPerAcreMin is { } min && crop.TypicalYieldPerAcreMax is { } max)
            {
                var acres = input.LandSizeDecimal is { } land ? land / 100.0 : 1.0;
                yieldMin = min * acres;
                yieldMax = max * acres;
            }

            return new CropMatch
            {
                Crop = crop,
                Factors = factors,
                YieldMinTonnes = yieldMin,
                YieldMaxTonnes = yieldMax,
                YieldIsPerAcre = input.LandSizeDecimal is null
            };
        }

        private static MatchFactor Factor(AdvisorFactor factor, IStringLocalizer l, int points, FactorState state, string reason, bool warning = false) =>
            new(factor, FactorName(factor, l), points, Weights[factor], state, reason, warning);

        public static string FactorName(AdvisorFactor factor, IStringLocalizer l) => factor switch
        {
            AdvisorFactor.Season => l["Season"],
            AdvisorFactor.SowingWindow => l["Sowing time"],
            AdvisorFactor.Soil => l["Soil"],
            AdvisorFactor.Irrigation => l["Irrigation"],
            AdvisorFactor.Region => l["Region"],
            _ => l["Soil pH"]
        };

        public static string SeasonLabel(string season, IStringLocalizer l) => season switch
        {
            "Rabi" => l["Rabi (Winter)"],
            "Kharif-1" => l["Kharif-1 (Early Summer)"],
            "Kharif-2" => l["Kharif-2 (Monsoon)"],
            _ => l["Year-round"]
        };

        public static string SoilLabel(SoilFamily family, IStringLocalizer l) => family switch
        {
            SoilFamily.Sandy => l["Sandy"],
            SoilFamily.SandyLoam => l["Sandy Loam"],
            SoilFamily.Loam => l["Loam"],
            SoilFamily.SiltLoam => l["Silt Loam"],
            SoilFamily.Silt => l["Alluvial Silt"],
            SoilFamily.ClayLoam => l["Clay Loam"],
            _ => l["Heavy Clay"]
        };

        public static string Months(IEnumerable<int> months) =>
            string.Join(", ", months.Where(m => m is >= 1 and <= 12).Select(m => CultureInfo.CurrentUICulture.DateTimeFormat.GetAbbreviatedMonthName(m)));

        private static MatchFactor SeasonFit(CropCalendarEntry crop, CropAdvisoryInput input, IStringLocalizer l)
        {
            var weight = Weights[AdvisorFactor.Season];
            if (crop.Season == "YearRound")
                return Factor(AdvisorFactor.Season, l, weight, FactorState.Met, l["Grown year-round, so it fits any season"]);
            if (crop.Season == input.Season)
                return Factor(AdvisorFactor.Season, l, weight, FactorState.Met, l["{0} crop, matching your season", SeasonLabel(crop.Season, l)]);
            if (crop.Season.StartsWith("Kharif", StringComparison.Ordinal) && input.Season.StartsWith("Kharif", StringComparison.Ordinal))
                return Factor(AdvisorFactor.Season, l, weight / 2, FactorState.Partial, l["{0} crop, next to your {1} season", SeasonLabel(crop.Season, l), SeasonLabel(input.Season, l)]);
            return Factor(AdvisorFactor.Season, l, 0, FactorState.Missed, l["{0} crop, not grown in the {1} season", SeasonLabel(crop.Season, l), SeasonLabel(input.Season, l)]);
        }

        private static MatchFactor SowingFit(CropCalendarEntry crop, int month, IStringLocalizer l)
        {
            var sowing = Months(crop.SowingMonths);
            if (crop.SowingMonths.Contains(month))
                return Factor(AdvisorFactor.SowingWindow, l, Weights[AdvisorFactor.SowingWindow], FactorState.Met, l["Sowing time is now ({0})", sowing]);
            var distance = crop.SowingMonths.Count == 0 ? 12 : crop.SowingMonths.Min(m => Math.Min(Math.Abs(m - month), 12 - Math.Abs(m - month)));
            return distance <= 1
                ? Factor(AdvisorFactor.SowingWindow, l, 12, FactorState.Partial, l["Sowing window is within a month ({0})", sowing])
                : Factor(AdvisorFactor.SowingWindow, l, 0, FactorState.Missed, l["Not sowing time now; sow in {0}", sowing]);
        }

        private static MatchFactor SoilFit(CropCalendarEntry crop, CropAdvisoryInput input, bool bangla, IStringLocalizer l)
        {
            var weight = Weights[AdvisorFactor.Soil];
            var needs = bangla && !string.IsNullOrWhiteSpace(crop.SoilTypesBn) ? crop.SoilTypesBn! : crop.SoilTypes.Split('(')[0].Trim();
            var cropFamilies = SoilClassifier.Parse(crop.SoilTypes);
            if (SoilClassifier.FromFormValue(input.SoilType) is not { } family)
                return Factor(AdvisorFactor.Soil, l, weight / 2, FactorState.Partial, l["Soil type not given; this crop needs {0}", needs]);

            var label = SoilLabel(family, l);
            // Loose sand cannot hold the standing water a high-water crop depends on, whatever else it is listed with.
            if (family == SoilFamily.Sandy && crop.WaterNeed == CropWaterNeed.High)
                return Factor(AdvisorFactor.Soil, l, 0, FactorState.Missed, l["{0} soil cannot hold the water this crop needs ({1})", label, needs]);
            if (cropFamilies.Contains(family))
                return Factor(AdvisorFactor.Soil, l, weight, FactorState.Met, l["Your {0} soil suits this crop", label]);
            if (cropFamilies.Any(c => SoilClassifier.AreNeighbours(c, family)))
                return Factor(AdvisorFactor.Soil, l, 12, FactorState.Partial, l["{0} soil is workable; this crop prefers {1}", label, needs]);
            return Factor(AdvisorFactor.Soil, l, 0, FactorState.Missed, l["{0} soil does not suit; this crop needs {1}", label, needs]);
        }

        private static MatchFactor IrrigationFit(CropCalendarEntry crop, CropAdvisoryInput input, IStringLocalizer l)
        {
            var weight = Weights[AdvisorFactor.Irrigation];
            if (input.HasIrrigation)
                return Factor(AdvisorFactor.Irrigation, l, weight, FactorState.Met, l["You have irrigation for this crop's water needs"]);
            return crop.WaterNeed switch
            {
                CropWaterNeed.Low => Factor(AdvisorFactor.Irrigation, l, weight, FactorState.Met, l["Low water need, fine without irrigation"]),
                CropWaterNeed.Medium => Factor(AdvisorFactor.Irrigation, l, 8, FactorState.Partial, l["Needs a few irrigations or reliable rain"]),
                _ => Factor(AdvisorFactor.Irrigation, l, 0, FactorState.Missed, l["Needs assured irrigation; risky without a pump or canal"], warning: true)
            };
        }

        private static MatchFactor RegionFit(CropCalendarEntry crop, CropAdvisoryInput input, IStringLocalizer l)
        {
            if (string.IsNullOrWhiteSpace(input.District))
                return Factor(AdvisorFactor.Region, l, 0, FactorState.Missed, l["District not given"]);
            var division = BangladeshGeo.GetDivision(input.District);
            if (MajorDistricts(crop).Contains(input.District))
                return Factor(AdvisorFactor.Region, l, Weights[AdvisorFactor.Region], FactorState.Met, l["{0} is a major growing district", input.District]);
            if (!crop.Division.Equals("All", StringComparison.OrdinalIgnoreCase)
                && crop.Division.Split(',', StringSplitOptions.TrimEntries).Select(BangladeshGeo.Canonical).Contains(division, StringComparer.OrdinalIgnoreCase))
                return Factor(AdvisorFactor.Region, l, 6, FactorState.Partial, l["Suited to {0} division", division]);
            if (crop.Division.Equals("All", StringComparison.OrdinalIgnoreCase))
                return Factor(AdvisorFactor.Region, l, 5, FactorState.Partial, l["Grown across Bangladesh"]);
            return Factor(AdvisorFactor.Region, l, 0, FactorState.Missed, l["Not a usual crop for {0} division", division]);
        }

        private static MatchFactor PhFit(CropCalendarEntry crop, CropAdvisoryInput input, IStringLocalizer l)
        {
            var weight = Weights[AdvisorFactor.Ph];
            if (input.SoilPh is not { } ph)
                return Factor(AdvisorFactor.Ph, l, 3, FactorState.Partial, l["Soil pH not given"]);
            if (crop.MinPh is not { } min || crop.MaxPh is not { } max)
                return Factor(AdvisorFactor.Ph, l, 3, FactorState.Partial, l["No pH range recorded for this crop"]);
            var range = $"{min.ToString("0.0", CultureInfo.InvariantCulture)}–{max.ToString("0.0", CultureInfo.InvariantCulture)}";
            var value = ph.ToString("0.0", CultureInfo.InvariantCulture);
            if (ph >= min && ph <= max)
                return Factor(AdvisorFactor.Ph, l, weight, FactorState.Met, l["pH {0} is in this crop's range ({1})", value, range]);
            if (ph >= min - 0.5 && ph <= max + 0.5)
                return Factor(AdvisorFactor.Ph, l, 3, FactorState.Partial, l["pH {0} is just outside this crop's range ({1})", value, range]);
            return Factor(AdvisorFactor.Ph, l, 0, FactorState.Missed, l["pH {0} is outside this crop's range ({1})", value, range]);
        }

        private static readonly Regex Bracketed = new(@"\([^)]*\)", RegexOptions.CultureInvariant);

        /// <summary>"Natore (Gurudaspur, Baraigram), Bogra" → {Natore, Bogura}, in today's spellings.</summary>
        public static IReadOnlySet<string> MajorDistricts(CropCalendarEntry crop) =>
            Bracketed.Replace(crop.MajorDistricts ?? string.Empty, string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(d => BangladeshGeo.Canonical(d)!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public interface ICropAdvisorService
    {
        Task<CropAdvisoryResult> RecommendAsync(CropAdvisoryInput input, CancellationToken cancellationToken = default);

        /// <summary>Saves one crop from a fresh recommendation for these inputs; false when that crop is not among the results.</summary>
        Task<bool> SaveAsync(string userId, CropAdvisoryInput input, int cropId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SavedCropAdvisory>> GetSavedAsync(string userId, CancellationToken cancellationToken = default);
    }

    public sealed class CropAdvisorService : ICropAdvisorService
    {
        /// <summary>Newest wins beyond this many saved advisories per farmer.</summary>
        public const int MaxSavedPerFarmer = 3;

        private static readonly JsonSerializerOptions FactorJson = new(JsonSerializerDefaults.Web);

        private readonly ICropCalendarService _calendar;
        private readonly ApplicationDbContext _db;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public CropAdvisorService(ICropCalendarService calendar, ApplicationDbContext db, IStringLocalizer<SharedResource> localizer)
        {
            _calendar = calendar;
            _db = db;
            _localizer = localizer;
        }

        public async Task<CropAdvisoryResult> RecommendAsync(CropAdvisoryInput input, CancellationToken cancellationToken = default)
        {
            var crops = await _calendar.GetAllCropsAsync();
            return CropAdvisorScorer.Recommend(crops, input, BangladeshClock.Today.Month, _localizer);
        }

        public async Task<bool> SaveAsync(string userId, CropAdvisoryInput input, int cropId, CancellationToken cancellationToken = default)
        {
            // Recomputed on the server: a posted score or crop list is never trusted.
            var result = await RecommendAsync(input, cancellationToken);
            var match = result.Matches.Concat(result.ClosestOptions).FirstOrDefault(m => m.Crop.Id == cropId);
            if (match is null || !await _db.CropCalendarEntries.AnyAsync(c => c.Id == cropId, cancellationToken)) return false;

            _db.SavedCropAdvisories.Add(new SavedCropAdvisory
            {
                UserId = userId,
                CropCalendarEntryId = cropId,
                Season = result.Input.Season,
                SoilType = result.Input.SoilType,
                SoilPh = result.Input.SoilPh,
                LandSizeDecimal = result.Input.LandSizeDecimal,
                HasIrrigation = result.Input.HasIrrigation,
                District = result.Input.District,
                MatchScore = match.Score,
                FactorsJson = JsonSerializer.Serialize(match.Factors, FactorJson)
            });
            await _db.SaveChangesAsync(cancellationToken);

            var keep = await _db.SavedCropAdvisories.Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id)
                .Take(MaxSavedPerFarmer).Select(s => s.Id).ToListAsync(cancellationToken);
            await _db.SavedCropAdvisories.Where(s => s.UserId == userId && !keep.Contains(s.Id)).ExecuteDeleteAsync(cancellationToken);
            return true;
        }

        public async Task<IReadOnlyList<SavedCropAdvisory>> GetSavedAsync(string userId, CancellationToken cancellationToken = default) =>
            await _db.SavedCropAdvisories.AsNoTracking()
                .Include(s => s.Crop)
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id)
                .Take(MaxSavedPerFarmer)
                .ToListAsync(cancellationToken);

        public static string SeasonForMonth(int month) => CropAdvisorScorer.SeasonForMonth(month);

        public static IReadOnlyList<MatchFactor> ReadFactors(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<List<MatchFactor>>(json, FactorJson) ?? new List<MatchFactor>();
            }
            catch (JsonException)
            {
                return Array.Empty<MatchFactor>();
            }
        }
    }
}
