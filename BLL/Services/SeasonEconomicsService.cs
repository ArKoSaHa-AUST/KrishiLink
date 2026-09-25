using KrishiLink.BLL.Helpers;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.BLL.Services
{
    /// <summary>One rental or storage cost on the season sheet, taken from the plan's own bookings.</summary>
    public sealed record SeasonPlatformCost(int ItemId, string ItemType, string Title, string Category, DateTime Start, DateTime End,
        decimal Amount, bool IsEstimate, string? BookingStatus);

    /// <summary>
    /// A season's money in one place (ECO-01): what the plan's rentals and storage cost, what the farmer spent elsewhere,
    /// and — from the seeded yield range, the land size and a price the farmer typed in — the expected gross, net and
    /// break-even yield. Every number is derived from those inputs; nothing is fetched from a market feed.
    /// </summary>
    public sealed class SeasonSheet
    {
        public required HarvestPlan Plan { get; init; }

        /// <summary>The calendar entries the plan's crop resolves to; yields need exactly one.</summary>
        public IReadOnlyList<CropCalendarEntry> CropEntries { get; init; } = Array.Empty<CropCalendarEntry>();
        public CropCalendarEntry? Crop => CropEntries.Count == 1 ? CropEntries[0] : null;

        public IReadOnlyList<SeasonPlatformCost> PlatformCosts { get; init; } = Array.Empty<SeasonPlatformCost>();
        public IReadOnlyList<SeasonCost> OwnCosts { get; init; } = Array.Empty<SeasonCost>();

        public decimal PlatformTotal => PlatformCosts.Sum(c => c.Amount);
        public decimal OwnTotal => OwnCosts.Sum(c => c.Amount);
        public decimal TotalCost => PlatformTotal + OwnTotal;
        public bool HasEstimates => PlatformCosts.Any(c => c.IsEstimate);

        public double? LandDecimals => Plan.LandSizeDecimal is > 0 ? Plan.LandSizeDecimal : null;
        public double? Acres => LandDecimals / UnitFormat.DecimalsPerAcre;
        public decimal? CostPerDecimal => LandDecimals is { } d ? decimal.Round(TotalCost / (decimal)d, 0) : null;
        public decimal? CostPerAcre => Acres is { } a ? decimal.Round(TotalCost / (decimal)a, 0) : null;

        /// <summary>Seeded yield range for the whole plot, in kg; null without a single crop, its yield data, or a land size.</summary>
        public double? YieldMinKg => Crop?.TypicalYieldPerAcreMin is { } min && Acres is { } a ? min * 1000 * a : null;
        public double? YieldMaxKg => Crop?.TypicalYieldPerAcreMax is { } max && Acres is { } a ? max * 1000 * a : null;

        public decimal? Price => Plan.ExpectedPricePerKg is > 0 ? Plan.ExpectedPricePerKg : null;

        public decimal? GrossMin => Gross(YieldMinKg);
        public decimal? GrossMax => Gross(YieldMaxKg);
        public decimal? NetMin => GrossMin - TotalCost;
        public decimal? NetMax => GrossMax - TotalCost;

        /// <summary>The harvest (kg) at the farmer's price that exactly covers the season's costs.</summary>
        public double? BreakEvenKg => Price is { } p && TotalCost > 0 ? (double)(TotalCost / p) : null;
        public double? BreakEvenKgPerAcre => BreakEvenKg / Acres;

        /// <summary>True when the expected harvest range sits entirely below break-even.</summary>
        public bool LossLikely => BreakEvenKg is { } b && YieldMaxKg is { } max && max < b;

        private decimal? Gross(double? kg) => kg is { } k && Price is { } p ? decimal.Round((decimal)k * p, 0) : null;
    }

    public interface ISeasonEconomicsService
    {
        /// <summary>The season sheet for one of the farmer's own plans; null when the plan is not theirs.</summary>
        Task<SeasonSheet?> GetSheetAsync(string farmerId, int planId, CancellationToken cancellationToken = default);

        /// <summary>Saves crop, land size and the farmer's expected price. Returns an error message, or null on success.</summary>
        Task<string?> UpdateInputsAsync(string farmerId, int planId, int? cropEntryId, double? landDecimals, decimal? pricePerKg, CancellationToken cancellationToken = default);

        Task<string?> AddCostAsync(string farmerId, int planId, string category, string? note, decimal amount, DateTime? incurredOn, CancellationToken cancellationToken = default);
        Task<bool> DeleteCostAsync(string farmerId, int planId, int costId, CancellationToken cancellationToken = default);

        /// <summary>Starts a season plan from saved crop advice, carrying over the exact crop and the land size.</summary>
        Task<(string? Error, int? PlanId)> StartFromAdviceAsync(string farmerId, int savedAdvisoryId, CancellationToken cancellationToken = default);
    }

    public sealed class SeasonEconomicsService : ISeasonEconomicsService
    {
        public const int MaxCostsPerPlan = 50;
        public const decimal MaxAmount = 10_000_000m;
        public const double MaxLandDecimals = 100_000;
        public const decimal MaxPricePerKg = 10_000m;

        private readonly ApplicationDbContext _db;
        private readonly ICropCalendarService _calendar;
        private readonly IEquipmentQueries _equipment;
        private readonly IHarvestPlanService _plans;

        public SeasonEconomicsService(ApplicationDbContext db, ICropCalendarService calendar, IEquipmentQueries equipment, IHarvestPlanService plans)
        {
            _db = db;
            _calendar = calendar;
            _equipment = equipment;
            _plans = plans;
        }

        public async Task<SeasonSheet?> GetSheetAsync(string farmerId, int planId, CancellationToken cancellationToken = default)
        {
            var plan = await _db.HarvestPlans.AsNoTracking()
                .Include(p => p.Items)
                .Include(p => p.Costs)
                .FirstOrDefaultAsync(p => p.Id == planId && p.FarmerId == farmerId, cancellationToken);
            if (plan is null) return null;

            var calendar = await _calendar.GetAllCropsAsync();
            return new SeasonSheet
            {
                Plan = plan,
                CropEntries = CropTiming.Resolve(plan.Crop, plan.CropCalendarEntryId, calendar),
                PlatformCosts = await PlatformCostsAsync(plan, cancellationToken),
                OwnCosts = plan.Costs.OrderBy(c => c.IncurredOn).ThenBy(c => c.Id).ToList()
            };
        }

        /// <summary>
        /// What each plan item costs the farmer: the agreed price once an owner accepted (less any discount), otherwise the
        /// live quote, flagged as an estimate. Rejected and cancelled bookings cost nothing and are left out.
        /// </summary>
        private async Task<List<SeasonPlatformCost>> PlatformCostsAsync(HarvestPlan plan, CancellationToken ct)
        {
            var eqIds = plan.Items.Where(i => i.ItemType == HarvestPlanItemType.Equipment).Select(i => i.ListingId).ToList();
            var gdIds = plan.Items.Where(i => i.ItemType == HarvestPlanItemType.Godown).Select(i => i.ListingId).ToList();
            var eqBookingIds = plan.Items.Where(i => i.ItemType == HarvestPlanItemType.Equipment && i.BookingId.HasValue).Select(i => i.BookingId!.Value).ToList();
            var gdBookingIds = plan.Items.Where(i => i.ItemType == HarvestPlanItemType.Godown && i.BookingId.HasValue).Select(i => i.BookingId!.Value).ToList();

            var equipment = await _db.Equipment.AsNoTracking().Where(e => eqIds.Contains(e.Id))
                .Select(e => new { e.Id, e.Name, e.Category }).ToDictionaryAsync(e => e.Id, ct);
            var godowns = await _db.Godowns.AsNoTracking().Where(g => gdIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Name, g.StorageType, g.PricePerTonPerMonth }).ToDictionaryAsync(g => g.Id, ct);
            var eqBookings = await _db.EquipmentBookings.AsNoTracking().Where(b => eqBookingIds.Contains(b.Id) && b.FarmerId == plan.FarmerId)
                .Select(b => new { b.Id, b.Status, b.AgreedGross, b.QuotedGross, b.DiscountAmount }).ToDictionaryAsync(b => b.Id, ct);
            var gdBookings = await _db.GodownBookings.AsNoTracking().Where(b => gdBookingIds.Contains(b.Id) && b.FarmerId == plan.FarmerId)
                .Select(b => new { b.Id, b.Status, b.AgreedGross, b.DiscountAmount }).ToDictionaryAsync(b => b.Id, ct);

            var costs = new List<SeasonPlatformCost>();
            foreach (var item in plan.Items.OrderBy(i => i.StartDate).ThenBy(i => i.Id))
            {
                if (item.ItemType == HarvestPlanItemType.Equipment)
                {
                    if (!equipment.TryGetValue(item.ListingId, out var eq)) continue;
                    var booking = item.BookingId is { } bid && eqBookings.TryGetValue(bid, out var b) ? b : null;
                    if (booking?.Status is BookingStatus.Rejected or BookingStatus.Cancelled) continue;
                    decimal amount;
                    bool estimate;
                    if (booking?.AgreedGross is { } agreed) { amount = agreed - booking.DiscountAmount; estimate = false; }
                    else if (booking is { QuotedGross: > 0 }) { amount = booking.QuotedGross - booking.DiscountAmount; estimate = true; }
                    else { amount = (await _equipment.QuoteGrossAsync(eq.Id, item.StartDate, item.EndDate, item.Units)).Gross; estimate = true; }
                    costs.Add(new SeasonPlatformCost(item.Id, item.ItemType, eq.Name, eq.Category, item.StartDate, item.EndDate, Math.Max(0, amount), estimate, booking?.Status));
                }
                else
                {
                    if (!godowns.TryGetValue(item.ListingId, out var gd)) continue;
                    var booking = item.BookingId is { } bid && gdBookings.TryGetValue(bid, out var b) ? b : null;
                    if (booking?.Status is BookingStatus.Rejected or BookingStatus.Cancelled) continue;
                    var estimate = booking?.AgreedGross is null;
                    var amount = booking?.AgreedGross is { } agreed
                        ? agreed - booking.DiscountAmount
                        : BookingPricing.GodownGross(item.StartDate, item.EndDate, item.Tons, gd.PricePerTonPerMonth);
                    costs.Add(new SeasonPlatformCost(item.Id, item.ItemType, gd.Name, gd.StorageType, item.StartDate, item.EndDate, Math.Max(0, amount), estimate, booking?.Status));
                }
            }
            return costs;
        }

        public async Task<string?> UpdateInputsAsync(string farmerId, int planId, int? cropEntryId, double? landDecimals, decimal? pricePerKg, CancellationToken cancellationToken = default)
        {
            var plan = await _db.HarvestPlans.FirstOrDefaultAsync(p => p.Id == planId && p.FarmerId == farmerId, cancellationToken);
            if (plan is null) return "Harvest plan not found.";

            if (landDecimals is { } land && (!double.IsFinite(land) || land <= 0 || land > MaxLandDecimals))
                return "Enter a land size greater than zero.";
            if (pricePerKg is { } price && (price <= 0 || price > MaxPricePerKg))
                return "Enter a selling price greater than zero.";

            if (cropEntryId is { } id)
            {
                var crop = (await _calendar.GetAllCropsAsync()).FirstOrDefault(c => c.Id == id);
                if (crop is null) return "Please choose a crop from the list.";
                plan.CropCalendarEntryId = crop.Id;
                plan.Crop = Truncate(crop.Name, 60);
            }
            plan.LandSizeDecimal = landDecimals is { } l ? Math.Round(l, 2) : null;
            plan.ExpectedPricePerKg = pricePerKg is { } p ? decimal.Round(p, 2) : null;
            await _db.SaveChangesAsync(cancellationToken);
            return null;
        }

        public async Task<string?> AddCostAsync(string farmerId, int planId, string category, string? note, decimal amount, DateTime? incurredOn, CancellationToken cancellationToken = default)
        {
            if (!SeasonCostCategories.All.Contains(category)) return "Please choose a cost type from the list.";
            if (amount <= 0 || amount > MaxAmount) return "Enter an amount greater than zero.";

            var plan = await _db.HarvestPlans.AsNoTracking()
                .Where(p => p.Id == planId && p.FarmerId == farmerId)
                .Select(p => new { p.Id, CostCount = p.Costs.Count })
                .FirstOrDefaultAsync(cancellationToken);
            if (plan is null) return "Harvest plan not found.";
            if (plan.CostCount >= MaxCostsPerPlan) return "This season already has the maximum number of cost entries.";

            var date = (incurredOn ?? BangladeshClock.Today).Date;
            if (date < BangladeshClock.Today.AddYears(-2) || date > BangladeshClock.Today.AddYears(1)) date = BangladeshClock.Today;

            _db.SeasonCosts.Add(new SeasonCost
            {
                HarvestPlanId = plan.Id,
                Category = category,
                Note = Truncate((note ?? string.Empty).Trim(), 200),
                Amount = decimal.Round(amount, 2),
                IncurredOn = date
            });
            await _db.SaveChangesAsync(cancellationToken);
            return null;
        }

        public async Task<bool> DeleteCostAsync(string farmerId, int planId, int costId, CancellationToken cancellationToken = default) =>
            await _db.SeasonCosts
                .Where(c => c.Id == costId && c.HarvestPlanId == planId && c.Plan!.FarmerId == farmerId)
                .ExecuteDeleteAsync(cancellationToken) > 0;

        public async Task<(string? Error, int? PlanId)> StartFromAdviceAsync(string farmerId, int savedAdvisoryId, CancellationToken cancellationToken = default)
        {
            var advice = await _db.SavedCropAdvisories.AsNoTracking().Include(s => s.Crop)
                .FirstOrDefaultAsync(s => s.Id == savedAdvisoryId && s.UserId == farmerId, cancellationToken);
            if (advice?.Crop is null) return ("That saved advice was not found.", null);

            var sow = CropNeeds.NextStart(advice.Crop.SowingMonths.FirstOrDefault(BangladeshClock.Today.Month), BangladeshClock.Today);
            var name = $"{advice.Crop.Name} — {sow.ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)}";
            var (error, planId) = await _plans.CreatePlanAsync(farmerId, name.Length <= 80 ? name : name[..80], Truncate(advice.Crop.Name, 60));
            if (error is not null || planId is null) return (error, null);

            var plan = await _db.HarvestPlans.FirstAsync(p => p.Id == planId, cancellationToken);
            plan.CropCalendarEntryId = advice.CropCalendarEntryId;
            plan.LandSizeDecimal = advice.LandSizeDecimal;
            await _db.SaveChangesAsync(cancellationToken);
            return (null, planId);
        }

        private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
    }
}
