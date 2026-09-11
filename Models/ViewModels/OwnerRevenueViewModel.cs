namespace KrishiLink.Models.ViewModels
{
    /// <summary>Query-string bound filters for the revenue page.</summary>
    public class RevenueFilter
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        /// <summary>Completed | Accepted | Pending | Cancelled | Rejected (null = all)</summary>
        public string? Status { get; set; }
        public int? ListingId { get; set; }

        /// <summary>Trend chart granularity: month | week</summary>
        public string Period { get; set; } = "month";

        /// <summary>Breakdown table sort: revenue | utilization | avg | bookings</summary>
        public string Sort { get; set; } = "revenue";

        /// <summary>Transactions page (1-based).</summary>
        public int Page { get; set; } = 1;

        public bool IsWeekly => string.Equals(Period, "week", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ViewModel for the owner Revenue page (godown or equipment). KPI cards and settlement are lifetime
    /// figures; trend, breakdown, funnel, utilization and transactions honour <see cref="Filter"/>.
    /// </summary>
    public class OwnerRevenueViewModel
    {
        /// <summary>"Godown" | "Equipment" — drives labels and wording in the shared view.</summary>
        public string ListingLabel { get; set; } = "Godown";

        /// <summary>Wording after "X / Y days", e.g. "rented" or "at full capacity (ton-days)".</summary>
        public string UtilizationHint { get; set; } = string.Empty;

        public RevenueFilter Filter { get; set; } = new();
        public DateTime RangeStart { get; set; }
        public DateTime RangeEnd { get; set; }
        public List<RevenueListingOption> Listings { get; set; } = new();

        // KPI cards
        public decimal TotalRevenue { get; set; }
        public decimal ThisMonthRevenue { get; set; }
        public decimal LastMonthRevenue { get; set; }

        /// <summary>This month vs. last month; null when last month had no revenue (no meaningful baseline).</summary>
        public int? MonthChangePercent => LastMonthRevenue > 0
            ? (int)Math.Round((ThisMonthRevenue - LastMonthRevenue) / LastMonthRevenue * 100)
            : null;
        public decimal UpcomingRevenue { get; set; }
        public int CompletedBookings { get; set; }

        public RevenueSettlement Settlement { get; set; } = new();
        public List<RevenueTrendPoint> Trend { get; set; } = new();

        /// <summary>Bucket size actually used by the trend chart (7 = weekly, 14 = fortnightly, 0 = monthly).</summary>
        public int TrendBucketDays { get; set; }

        /// <summary>Set when the trend had to drop older buckets to stay readable.</summary>
        public string? TrendNote { get; set; }
        public List<ListingRevenueBreakdownItem> Breakdown { get; set; } = new();
        public BookingFunnel Funnel { get; set; } = new();

        /// <summary>The current page of transactions; see <see cref="TransactionsTotal"/> and <see cref="PageCount"/>.</summary>
        public List<RevenueTransactionItem> Transactions { get; set; } = new();
        public int TransactionsTotal { get; set; }
        public int PageCount { get; set; } = 1;
        public RevenueInsights Insights { get; set; } = new();

        /// <summary>Payout processed within the last 7 days, surfaced as an alert.</summary>
        public PayoutItem? RecentPayout { get; set; }
    }

    public class RevenueListingOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Money owed vs. paid out, with the platform commission shown transparently.</summary>
    public class RevenueSettlement
    {
        public decimal Gross { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal Commission { get; set; }
        public decimal Expenses { get; set; }
        public decimal NetEarned => Gross - Commission;
        public decimal NetProfit => NetEarned - Expenses;
        public decimal PaidOut { get; set; }
        public decimal Processing { get; set; }

        /// <summary>Running "pending payout": net revenue of completed bookings not yet linked to any payout.</summary>
        public decimal Owed { get; set; }
        public int UnpaidBookings { get; set; }
        public List<PayoutItem> Payouts { get; set; } = new();
    }

    public class PayoutItem
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Reference { get; set; } = string.Empty;
        public decimal Gross { get; set; }
        public decimal Commission { get; set; }

        /// <summary>Net amount paid to the owner.</summary>
        public decimal Amount { get; set; }
        public string Method { get; set; } = string.Empty;
        public string? Account { get; set; }
        public string Status { get; set; } = "Completed";

        /// <summary>How many completed bookings this payout settled.</summary>
        public int BookingCount { get; set; }
    }

    /// <summary>Dedicated payouts page: pending balance, commission explainer and the full settlement history.</summary>
    public class PayoutHistoryViewModel
    {
        public string ListingLabel { get; set; } = string.Empty;
        public RevenueSettlement Settlement { get; set; } = new();
        public int CompletedBookings { get; set; }
        public static readonly string[] PayoutMethods = { "bKash", "Nagad", "Rocket", "Bank Transfer" };
    }

    public class RevenueTrendPoint
    {
        public string Label { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class ListingRevenueBreakdownItem
    {
        public int ListingId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Bookings { get; set; }
        public decimal Revenue { get; set; }
        public decimal AveragePerBooking => Bookings > 0 ? Revenue / Bookings : 0;

        /// <summary>Full-capacity days booked (completed or ongoing) within the elapsed part of the selected range.</summary>
        public double BookedDays { get; set; }

        /// <summary>Elapsed days in the selected range (future days are excluded).</summary>
        public int PeriodDays { get; set; }

        /// <summary>BookedDays ÷ PeriodDays, as a percentage.</summary>
        public int UtilizationPercent { get; set; }

        /// <summary>True when utilization is below the service's low-utilization threshold.</summary>
        public bool IsUnderUtilized { get; set; }

        /// <summary>Top | Under | null</summary>
        public string? PerformanceFlag { get; set; }
    }

    /// <summary>Booking status funnel for the selected range.</summary>
    public class BookingFunnel
    {
        public int Requested { get; set; }
        public int Accepted { get; set; }
        public int Ongoing { get; set; }
        public int Completed { get; set; }
        public int Cancelled { get; set; }
        public int Rejected { get; set; }

        /// <summary>Cancelled ÷ (Accepted + Completed + Cancelled), as a percentage.</summary>
        public int CancellationRatePercent
        {
            get
            {
                var confirmed = Accepted + Completed + Cancelled;
                return confirmed > 0 ? (int)Math.Round(Cancelled * 100.0 / confirmed) : 0;
            }
        }
    }

    public class RevenueTransactionItem
    {
        public int BookingId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string ListingName { get; set; } = string.Empty;
        public string QuantityText { get; set; } = string.Empty;
        public decimal Gross { get; set; }
        public decimal Commission { get; set; }
        public decimal Expenses => ExpenseLines.Sum(e => e.Amount);
        public decimal Net => Gross - Commission - Expenses;
        public string Status { get; set; } = "Pending";
        public bool IsRepeatCustomer { get; set; }

        /// <summary>Reference of the payout that settled this booking; null while unpaid.</summary>
        public string? PayoutReference { get; set; }
        public bool IsPaid => PayoutReference is not null;
        public List<ExpenseLine> ExpenseLines { get; set; } = new();
    }

    public class ExpenseLine
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    public class RevenueInsights
    {
        /// <summary>e.g. "April – June"; null when there isn't enough history.</summary>
        public string? PeakSeason { get; set; }
        public int DistinctCustomers { get; set; }
        public int RepeatCustomers { get; set; }

        /// <summary>Listings whose utilization fell below the low-utilization threshold in the range.</summary>
        public int UnderUtilizedListings { get; set; }

        /// <summary>The configured threshold used for <see cref="UnderUtilizedListings"/>, for display.</summary>
        public int LowUtilizationPercent { get; set; }

        /// <summary>Top listing vs. weakest listing, e.g. multiplier of 3.2 → "earned 3.2× more".</summary>
        public string? TopListing { get; set; }
        public string? WeakestListing { get; set; }
        public double? TopVsWeakestMultiplier { get; set; }
    }

    /// <summary>Printable receipt for a completed booking.</summary>
    public class BookingInvoiceViewModel
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime IssuedOn { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string? OwnerBusiness { get; set; }
        public string? OwnerLocation { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerLocation { get; set; }
        public string ListingName { get; set; } = string.Empty;
        public string ListingLocation { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string QuantityText { get; set; } = string.Empty;
        public string RateText { get; set; } = string.Empty;
        public decimal Gross { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal Commission { get; set; }
        public decimal NetPayable => Gross - Commission;
        public string Status { get; set; } = string.Empty;
    }
}
