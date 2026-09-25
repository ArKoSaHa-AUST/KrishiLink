namespace KrishiLink.Models.Entities
{
    /// <summary>Cost headings a farmer spends on outside the platform, mirroring the owners' <see cref="ExpenseCategories"/>.</summary>
    public static class SeasonCostCategories
    {
        public const string Seed = "Seed";
        public const string Fertilizer = "Fertilizer";
        public const string Pesticide = "Pesticide";
        public const string Labour = "Labour";
        public const string Irrigation = "Irrigation";
        public const string Transport = "Transport";
        public const string LandLease = "LandLease";
        public const string Other = "Other";

        public static readonly string[] All = { Seed, Fertilizer, Pesticide, Labour, Irrigation, Transport, LandLease, Other };
    }

    /// <summary>
    /// A cost a farmer records against a season (a harvest plan) that was not paid through KrishiLink — seed, fertilizer,
    /// labour. Platform rentals and storage are summed from the plan's bookings, never re-entered here (ECO-01).
    /// </summary>
    public class SeasonCost
    {
        public int Id { get; set; }

        public int HarvestPlanId { get; set; }
        public HarvestPlan? Plan { get; set; }

        public string Category { get; set; } = SeasonCostCategories.Other;
        public string Note { get; set; } = string.Empty;
        public decimal Amount { get; set; }

        /// <summary>The day the money was spent (a calendar date).</summary>
        public DateTime IncurredOn { get; set; } = DateTime.UtcNow.Date;

        public DateTime RecordedOn { get; set; } = DateTime.UtcNow;
    }
}
