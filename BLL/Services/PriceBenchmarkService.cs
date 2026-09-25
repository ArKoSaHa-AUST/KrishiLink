using KrishiLink.BLL.Helpers;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace KrishiLink.BLL.Services
{
    /// <summary>How narrowly a benchmark could be drawn: the same month in the district, or any month in the district.</summary>
    public enum BenchmarkScope
    {
        DistrictMonth,
        District
    }

    /// <summary>
    /// What renters actually paid for comparable listings (DIS-03): the median and the middle half (p25–p75) of completed
    /// bookings. Equipment is per unit per day as paid (seasonal and weekend rates included); storage is per ton per month.
    /// </summary>
    public sealed record PriceBenchmark(decimal Median, decimal P25, decimal P75, int SampleSize, BenchmarkScope Scope, int? Month)
    {
        public PricePosition PositionOf(decimal price) => price < P25 ? PricePosition.Below : price > P75 ? PricePosition.Above : PricePosition.Typical;
    }

    public enum PricePosition
    {
        Below,
        Typical,
        Above
    }

    public interface IPriceBenchmarkService
    {
        /// <summary>Null when fewer than <see cref="PriceBenchmarkService.MinimumSample"/> completed rentals exist — never a guess.</summary>
        Task<PriceBenchmark?> ForEquipmentAsync(string category, string? district, DateTime? month = null, CancellationToken cancellationToken = default);

        Task<PriceBenchmark?> ForGodownAsync(string storageType, string? district, DateTime? month = null, CancellationToken cancellationToken = default);
    }

    public sealed class PriceBenchmarkService : IPriceBenchmarkService
    {
        /// <summary>A benchmark from fewer bookings than this is worse than none, so nothing is shown.</summary>
        public const int MinimumSample = 5;

        private static readonly TimeSpan CacheFor = TimeSpan.FromHours(1);

        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;

        public PriceBenchmarkService(ApplicationDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public Task<PriceBenchmark?> ForEquipmentAsync(string category, string? district, DateTime? month = null, CancellationToken cancellationToken = default) =>
            BenchmarkAsync("eq", category, district, month, async (canonical, ct) =>
            {
                var rows = await _db.EquipmentBookings.AsNoTracking()
                    .Where(b => b.Status == BookingStatus.Completed && b.AgreedGross != null && b.AgreedGross > 0
                        && b.Equipment!.Category.ToLower() == category.ToLower())
                    .Select(b => new { b.Equipment!.District, b.StartDate, b.EndDate, b.Units, Gross = b.AgreedGross!.Value })
                    .ToListAsync(ct);
                return rows
                    .Where(r => SameDistrict(r.District, canonical))
                    .Select(r => (r.StartDate.Month, Price: r.Gross / (ListingFormat.InclusiveDays(r.StartDate, r.EndDate) * Math.Max(1, r.Units))))
                    .ToList();
            }, cancellationToken);

        public Task<PriceBenchmark?> ForGodownAsync(string storageType, string? district, DateTime? month = null, CancellationToken cancellationToken = default) =>
            BenchmarkAsync("gd", storageType, district, month, async (canonical, ct) =>
            {
                var rows = await _db.GodownBookings.AsNoTracking()
                    .Where(b => b.Status == BookingStatus.Completed && b.AgreedRate != null && b.AgreedRate > 0
                        && b.Godown!.StorageType.ToLower() == storageType.ToLower())
                    .Select(b => new { b.Godown!.District, b.StartDate, Rate = b.AgreedRate!.Value })
                    .ToListAsync(ct);
                return rows
                    .Where(r => SameDistrict(r.District, canonical))
                    .Select(r => (r.StartDate.Month, Price: r.Rate))
                    .ToList();
            }, cancellationToken);

        private async Task<PriceBenchmark?> BenchmarkAsync(string kind, string category, string? district, DateTime? month,
            Func<string, CancellationToken, Task<List<(int Month, decimal Price)>>> load, CancellationToken ct)
        {
            var canonical = BangladeshGeo.Canonical(district);
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(canonical)) return null;
            var targetMonth = (month ?? BangladeshClock.Today).Month;

            var key = $"price-benchmark:{kind}:{category.ToLowerInvariant()}:{canonical.ToLowerInvariant()}";
            var prices = await _cache.GetOrCreateAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheFor;
                return await load(canonical, ct);
            }) ?? new List<(int Month, decimal Price)>();

            return Compute(prices, targetMonth);
        }

        /// <summary>The same month if it alone has enough bookings, else the whole year in the district, else nothing.</summary>
        public static PriceBenchmark? Compute(IReadOnlyCollection<(int Month, decimal Price)> prices, int month)
        {
            var inMonth = prices.Where(p => p.Month == month).Select(p => p.Price).ToList();
            if (inMonth.Count >= MinimumSample) return Summarize(inMonth, BenchmarkScope.DistrictMonth, month);
            var all = prices.Select(p => p.Price).ToList();
            return all.Count >= MinimumSample ? Summarize(all, BenchmarkScope.District, null) : null;
        }

        private static PriceBenchmark Summarize(List<decimal> prices, BenchmarkScope scope, int? month)
        {
            prices.Sort();
            return new PriceBenchmark(Round(Percentile(prices, 0.5)), Round(Percentile(prices, 0.25)), Round(Percentile(prices, 0.75)), prices.Count, scope, month);
        }

        /// <summary>Linear interpolation between closest ranks (PostgreSQL's percentile_cont) over sorted values.</summary>
        public static decimal Percentile(IReadOnlyList<decimal> sorted, double p)
        {
            if (sorted.Count == 0) throw new ArgumentException("No values.", nameof(sorted));
            var rank = p * (sorted.Count - 1);
            var lower = (int)Math.Floor(rank);
            var upper = (int)Math.Ceiling(rank);
            return sorted[lower] + (sorted[upper] - sorted[lower]) * (decimal)(rank - lower);
        }

        private static decimal Round(decimal value) => decimal.Round(value, 0, MidpointRounding.AwayFromZero);

        private static bool SameDistrict(string? stored, string canonical) =>
            string.Equals(BangladeshGeo.Canonical(stored), canonical, StringComparison.OrdinalIgnoreCase);
    }
}
