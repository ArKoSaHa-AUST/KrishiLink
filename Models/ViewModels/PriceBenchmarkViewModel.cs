using KrishiLink.BLL.Services;

namespace KrishiLink.Models.ViewModels
{
    /// <summary>Model for <c>Views/Shared/_PriceBenchmark.cshtml</c>: one listing's price against what renters paid nearby (DIS-03).</summary>
    public class PriceBenchmarkViewModel
    {
        /// <summary>Null when there are not enough completed bookings to compare against.</summary>
        public PriceBenchmark? Benchmark { get; init; }
        public decimal Price { get; init; }

        /// <summary>Storage is priced per ton per month; equipment per day.</summary>
        public bool PerTonMonth { get; init; }
        public string Category { get; init; } = string.Empty;
        public string District { get; init; } = string.Empty;

        /// <summary>Owners see "your rate"; farmers see "this rate".</summary>
        public bool ForOwner { get; init; }
    }
}

namespace KrishiLink.Models.ViewModels
{
    /// <summary>Model for <c>Views/Shared/_ListingGeoControls.cshtml</c>: the "within N km" and list/map controls (DIS-02).</summary>
    public class ListingGeoControlsViewModel
    {
        /// <summary>The page's district select, used as the origin when the user has not shared their position.</summary>
        public string DistrictSelectId { get; init; } = string.Empty;

        /// <summary>Comma-separated ids of the list-only elements (grid, pagination) hidden while the map shows.</summary>
        public string HideInMap { get; init; } = string.Empty;

        /// <summary>The JSON field holding the formatted price for the map popups.</summary>
        public string PriceField { get; init; } = string.Empty;

        public double? RadiusKm { get; init; }
    }
}
