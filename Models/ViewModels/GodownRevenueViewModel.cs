namespace KrishiLink.Models.ViewModels
{
    /// <summary>Query-string bound filters for the revenue page and CSV export.</summary>
    public class RevenueFilter
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        /// <summary>Completed | Accepted | Pending | Cancelled | Rejected (null = all)</summary>
        public string? Status { get; set; }
        public int? GodownId { get; set; }

        /// <summary>Trend chart granularity: month | week</summary>
        public string Period { get; set; } = "month";

        public bool IsWeekly => string.Equals(Period, "week", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ViewModel for the Godown Owner Revenue page. KPI cards and settlement are lifetime figures;
    /// trend, breakdown, funnel, utilization and transactions honour <see cref="Filter"/>.
    /// </summary>
    public class GodownRevenueViewModel
    {
        public RevenueFilter Filter { get; set; } = new();
        public DateTime RangeStart { get; set; }
        public DateTime RangeEnd { get; set; }
        public List<RevenueGodownOption> Godowns { get; set; } = new();

        // KPI cards
        public decimal TotalRevenue { get; set; }
        public decimal ThisMonthRevenue { get; set; }
        public decimal UpcomingRevenue { get; set; }
        public int CompletedBookings { get; set; }

        public RevenueSettlement Settlement { get; set; } = new();
        public List<RevenueTrendPoint> Trend { get; set; } = new();
        public List<GodownRevenueBreakdownItem> Breakdown { get; set; } = new();
        public BookingFunnel Funnel { get; set; } = new();
        public List<RevenueTransactionItem> Transactions { get; set; } = new();
        public RevenueInsights Insights { get; set; } = new();

        /// <summary>Payout processed within the last 7 days, surfaced as an alert.</summary>
        public PayoutItem? RecentPayout { get; set; }
    }

    public class RevenueGodownOption
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
        public decimal Owed => Math.Max(0, NetEarned - PaidOut - Processing);
        public List<PayoutItem> Payouts { get; set; } = new();
    }

    public class PayoutItem
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; } = string.Empty;
        public string Status { get; set; } = "Completed";
    }

    public class RevenueTrendPoint
    {
        public string Label { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class GodownRevenueBreakdownItem
    {
        public int GodownId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Bookings { get; set; }
        public decimal Revenue { get; set; }
        public decimal AveragePerBooking => Bookings > 0 ? Revenue / Bookings : 0;

        /// <summary>Booked ton-days ÷ capacity ton-days across the selected range.</summary>
        public int UtilizationPercent { get; set; }

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
        public string FarmerName { get; set; } = string.Empty;
        public string GodownName { get; set; } = string.Empty;
        public double StorageTons { get; set; }
        public double Months { get; set; }
        public decimal Gross { get; set; }
        public decimal Commission { get; set; }
        public decimal Expenses { get; set; }
        public decimal Net => Gross - Commission - Expenses;
        public string Status { get; set; } = "Pending";
        public bool IsRepeatCustomer { get; set; }
    }

    public class RevenueInsights
    {
        /// <summary>e.g. "April – June"; null when there isn't enough history.</summary>
        public string? PeakSeason { get; set; }
        public int DistinctCustomers { get; set; }
        public int RepeatCustomers { get; set; }

        /// <summary>Top listing vs. weakest listing, e.g. multiplier of 3.2 → "earned 3.2× more".</summary>
        public string? TopListing { get; set; }
        public string? WeakestListing { get; set; }
        public double? TopVsWeakestMultiplier { get; set; }
    }

    /// <summary>Printable receipt for a completed godown booking.</summary>
    public class BookingInvoiceViewModel
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime IssuedOn { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string? OwnerBusiness { get; set; }
        public string? OwnerLocation { get; set; }
        public string FarmerName { get; set; } = string.Empty;
        public string? FarmerLocation { get; set; }
        public string GodownName { get; set; } = string.Empty;
        public string GodownLocation { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double StorageTons { get; set; }
        public decimal RatePerTonPerMonth { get; set; }
        public double Months { get; set; }
        public decimal Gross { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal Commission { get; set; }
        public decimal NetPayable => Gross - Commission;
        public string Status { get; set; } = string.Empty;
    }
}
