using System.Globalization;
using System.Text;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.Extensions.Options;

namespace KrishiLink.BLL.Services
{
    /// <summary>Bound from the "Revenue" section of appsettings.json.</summary>
    public class RevenueOptions
    {
        public const string SectionName = "Revenue";

        /// <summary>Fraction of gross booking value retained by the platform (0.05 = 5%).</summary>
        public decimal PlatformCommissionRate { get; set; } = 0.05m;
    }

    public interface IOwnerRevenueService
    {
        OwnerRevenueViewModel GetReport(string ownerId, RevenueFilter filter);
        string ExportCsv(string ownerId, RevenueFilter filter);
        BookingInvoiceViewModel? GetInvoice(string ownerId, int bookingId);
        bool AddExpense(string ownerId, int bookingId, decimal amount, string? note);
    }

    public interface IGodownRevenueService : IOwnerRevenueService { }

    public interface IEquipmentRevenueService : IOwnerRevenueService { }

    /// <summary>Listing-type specific wording used by the shared revenue view and invoice.</summary>
    public record RevenueProfile(string ListingLabel, string UtilizationHint, string InvoicePrefix, string InvoiceTitle);

    public class GodownRevenueService : OwnerRevenueService, IGodownRevenueService
    {
        public GodownRevenueService(IGodownRevenueRepository repo, IOptions<RevenueOptions> options)
            : base(repo, options, new RevenueProfile("Godown", "at full capacity (ton-days)", "KL-GB", "Godown Storage Receipt")) { }
    }

    public class EquipmentRevenueService : OwnerRevenueService, IEquipmentRevenueService
    {
        public EquipmentRevenueService(IEquipmentRevenueRepository repo, IOptions<RevenueOptions> options)
            : base(repo, options, new RevenueProfile("Equipment", "rented", "KL-EQ", "Equipment Rental Receipt")) { }
    }

    /// <summary>
    /// Revenue analytics shared by godown and equipment owners. Revenue is recognised when a booking
    /// completes (its EndDate); Accepted bookings count as upcoming revenue. All money is in BDT.
    /// Pricing is resolved by the repository, so this class only aggregates.
    /// </summary>
    public class OwnerRevenueService : IOwnerRevenueService
    {
        private const int MaxMonthBuckets = 24;
        private const int MaxWeekBuckets = 16;

        /// <summary>Below this share of elapsed days booked, a listing is flagged with a pricing/listing-quality suggestion.</summary>
        public const int LowUtilizationPercent = 25;
        private static readonly string[] ConfirmedStatuses = { "Accepted", "Completed" };

        private readonly IOwnerRevenueRepository _repo;
        private readonly RevenueProfile _profile;
        private readonly decimal _commissionRate;

        public OwnerRevenueService(IOwnerRevenueRepository repo, IOptions<RevenueOptions> options, RevenueProfile profile)
        {
            _repo = repo;
            _profile = profile;
            _commissionRate = options.Value.PlatformCommissionRate;
        }

        public OwnerRevenueViewModel GetReport(string ownerId, RevenueFilter filter)
        {
            var today = DateTime.Today;
            var (from, to) = ResolveRange(filter, today);

            var listings = _repo.GetListings(ownerId);
            var allBookings = _repo.GetBookings(ownerId);
            var expenses = _repo.GetExpenses(ownerId);
            var payouts = _repo.GetPayouts(ownerId);
            var expenseByBooking = expenses.GroupBy(e => e.BookingId).ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

            // Range + listing filters drive every analytics block; the status filter is for the transaction list only.
            var inRange = allBookings
                .Where(b => Overlaps(b, from, to) && (filter.ListingId is null || b.ListingId == filter.ListingId))
                .ToList();
            var scopedListings = listings.Where(l => filter.ListingId is null || l.Id == filter.ListingId).ToList();

            var completedLifetime = allBookings.Where(b => b.Status == "Completed").ToList();
            var totalRevenue = completedLifetime.Sum(b => b.Gross);

            var model = new OwnerRevenueViewModel
            {
                ListingLabel = _profile.ListingLabel,
                UtilizationHint = _profile.UtilizationHint,
                Filter = filter,
                RangeStart = from,
                RangeEnd = to,
                Listings = listings.Select(l => new RevenueListingOption { Id = l.Id, Name = l.Name }).ToList(),

                TotalRevenue = totalRevenue,
                ThisMonthRevenue = completedLifetime
                    .Where(b => b.EndDate.Year == today.Year && b.EndDate.Month == today.Month)
                    .Sum(b => b.Gross),
                UpcomingRevenue = allBookings.Where(b => b.Status == "Accepted").Sum(b => b.Gross),
                CompletedBookings = completedLifetime.Count,

                Settlement = BuildSettlement(totalRevenue, expenses, payouts),
                Trend = BuildTrend(inRange, from, to, filter.IsWeekly),
                Breakdown = BuildBreakdown(scopedListings, inRange, from, to, today),
                Funnel = BuildFunnel(inRange, today),
                Transactions = BuildTransactions(inRange, allBookings, expenseByBooking, filter.Status),
                Insights = BuildInsights(inRange, allBookings),
                RecentPayout = payouts
                    .Where(p => p.Status == "Completed" && p.TransactionDate >= today.AddDays(-7))
                    .OrderByDescending(p => p.TransactionDate)
                    .Select(ToPayoutItem)
                    .FirstOrDefault()
            };

            // Top vs. weakest listing in the range
            model.Insights.UnderUtilizedListings = model.Breakdown.Count(b => b.IsUnderUtilized);
            if (model.Breakdown.Count > 1 && model.Breakdown[0].Revenue > 0)
            {
                var weakest = model.Breakdown[^1];
                model.Insights.TopListing = model.Breakdown[0].Name;
                model.Insights.WeakestListing = weakest.Name;
                model.Insights.TopVsWeakestMultiplier = weakest.Revenue > 0
                    ? Math.Round((double)(model.Breakdown[0].Revenue / weakest.Revenue), 1)
                    : null;
            }

            return model;
        }

        public string ExportCsv(string ownerId, RevenueFilter filter)
        {
            var report = GetReport(ownerId, filter);
            var sb = new StringBuilder();
            sb.AppendLine($"Booking ID,Start Date,End Date,Customer,{_profile.ListingLabel},Quantity,Gross (BDT),Commission (BDT),Expenses (BDT),Net (BDT),Status");
            foreach (var t in report.Transactions)
            {
                sb.AppendLine(string.Join(",",
                    t.BookingId,
                    t.StartDate.ToString("yyyy-MM-dd"),
                    t.EndDate.ToString("yyyy-MM-dd"),
                    Csv(t.CustomerName),
                    Csv(t.ListingName),
                    Csv(t.QuantityText),
                    t.Gross.ToString("0.##", CultureInfo.InvariantCulture),
                    t.Commission.ToString("0.##", CultureInfo.InvariantCulture),
                    t.Expenses.ToString("0.##", CultureInfo.InvariantCulture),
                    t.Net.ToString("0.##", CultureInfo.InvariantCulture),
                    t.Status));
            }
            return sb.ToString();
        }

        public BookingInvoiceViewModel? GetInvoice(string ownerId, int bookingId)
        {
            var b = _repo.GetBookings(ownerId).FirstOrDefault(x => x.Id == bookingId && x.Status == "Completed");
            if (b is null) return null;

            return new BookingInvoiceViewModel
            {
                InvoiceNumber = $"{_profile.InvoicePrefix}-{b.Id:D6}",
                Title = _profile.InvoiceTitle,
                IssuedOn = b.EndDate,
                CustomerName = b.CustomerName,
                CustomerLocation = b.CustomerLocation,
                ListingName = b.ListingName,
                ListingLocation = b.ListingLocation,
                StartDate = b.StartDate,
                EndDate = b.EndDate,
                QuantityText = b.QuantityText,
                RateText = b.RateText,
                Gross = b.Gross,
                CommissionRate = _commissionRate,
                Commission = Commission(b.Gross),
                Status = b.Status
            };
        }

        public bool AddExpense(string ownerId, int bookingId, decimal amount, string? note)
        {
            if (amount <= 0) return false;
            var booking = _repo.GetBookings(ownerId).FirstOrDefault(b => b.Id == bookingId && ConfirmedStatuses.Contains(b.Status));
            if (booking is null) return false;

            _repo.AddExpense(new BookingExpense
            {
                BookingId = bookingId,
                OwnerId = ownerId,
                Amount = decimal.Round(amount, 2),
                Note = (note ?? string.Empty).Trim(),
                RecordedOn = DateTime.UtcNow
            });
            return true;
        }

        // ---- Helpers -------------------------------------------------------------------------

        private decimal Commission(decimal gross) => decimal.Round(gross * _commissionRate, 0);

        private static bool Overlaps(RevenueBooking b, DateTime from, DateTime to) =>
            b.StartDate.Date <= to && b.EndDate.Date >= from;

        private static double OverlapDays(RevenueBooking b, DateTime from, DateTime to)
        {
            var start = b.StartDate.Date > from ? b.StartDate.Date : from;
            var end = b.EndDate.Date < to ? b.EndDate.Date : to;
            return Math.Max(0, (end - start).TotalDays + 1);
        }

        private static (DateTime From, DateTime To) ResolveRange(RevenueFilter filter, DateTime today)
        {
            var to = (filter.To ?? today).Date;
            var from = (filter.From ?? new DateTime(today.Year, today.Month, 1).AddMonths(-11)).Date;
            if (from > to) (from, to) = (to, from);
            filter.From = from;
            filter.To = to;
            return (from, to);
        }

        private RevenueSettlement BuildSettlement(decimal gross, IReadOnlyList<BookingExpense> expenses, IReadOnlyList<Transaction> payouts) => new()
        {
            Gross = gross,
            CommissionRate = _commissionRate,
            Commission = Commission(gross),
            Expenses = expenses.Sum(e => e.Amount),
            PaidOut = payouts.Where(p => p.Status == "Completed").Sum(p => p.Amount),
            Processing = payouts.Where(p => p.Status == "Processing").Sum(p => p.Amount),
            Payouts = payouts.OrderByDescending(p => p.TransactionDate).Select(ToPayoutItem).ToList()
        };

        private static PayoutItem ToPayoutItem(Transaction p) => new()
        {
            Date = p.TransactionDate,
            Amount = p.Amount,
            Method = p.PaymentMethod,
            Status = p.Status
        };

        private static List<RevenueTrendPoint> BuildTrend(List<RevenueBooking> inRange, DateTime from, DateTime to, bool weekly)
        {
            var completed = inRange.Where(b => b.Status == "Completed").ToList();
            var points = new List<RevenueTrendPoint>();

            if (weekly)
            {
                // ISO-style weeks starting Monday, newest week last, capped for readability
                var weekStart = to.AddDays(-(((int)to.DayOfWeek + 6) % 7));
                var starts = new List<DateTime>();
                for (var i = 0; i < MaxWeekBuckets && weekStart.AddDays(6) >= from; i++, weekStart = weekStart.AddDays(-7))
                    starts.Add(weekStart);
                starts.Reverse();

                foreach (var start in starts)
                {
                    var end = start.AddDays(6);
                    points.Add(new RevenueTrendPoint
                    {
                        Label = start.ToString("d MMM", CultureInfo.InvariantCulture),
                        Amount = completed.Where(b => b.EndDate.Date >= start && b.EndDate.Date <= end).Sum(b => b.Gross)
                    });
                }
                return points;
            }

            var first = new DateTime(from.Year, from.Month, 1);
            var last = new DateTime(to.Year, to.Month, 1);
            var months = (last.Year - first.Year) * 12 + last.Month - first.Month + 1;
            if (months > MaxMonthBuckets) first = last.AddMonths(-(MaxMonthBuckets - 1));

            for (var m = first; m <= last; m = m.AddMonths(1))
            {
                points.Add(new RevenueTrendPoint
                {
                    Label = m.ToString("MMM yy", CultureInfo.InvariantCulture),
                    Amount = completed.Where(b => b.EndDate.Year == m.Year && b.EndDate.Month == m.Month).Sum(b => b.Gross)
                });
            }
            return points;
        }

        private static List<ListingRevenueBreakdownItem> BuildBreakdown(List<RevenueListing> listings, List<RevenueBooking> inRange, DateTime from, DateTime to, DateTime today)
        {
            // Utilization only counts days that have actually elapsed — future accepted days aren't "used" yet
            var usageEnd = to < today ? to : today;
            var periodDays = Math.Max(0, (usageEnd - from).TotalDays + 1);

            var items = listings.Select(l =>
            {
                var completed = inRange.Where(b => b.ListingId == l.Id && b.Status == "Completed").ToList();
                var bookedCapacityDays = inRange
                    .Where(b => b.ListingId == l.Id && ConfirmedStatuses.Contains(b.Status))
                    .Sum(b => b.CapacityUsed * OverlapDays(b, from, usageEnd));
                // Normalise to "full-capacity days" so godowns (tons) and equipment (1 unit) read the same way
                var bookedDays = l.Capacity > 0 ? Math.Min(periodDays, bookedCapacityDays / l.Capacity) : 0;
                var utilization = periodDays > 0 ? (int)Math.Round(bookedDays / periodDays * 100) : 0;

                return new ListingRevenueBreakdownItem
                {
                    ListingId = l.Id,
                    Name = l.Name,
                    Bookings = completed.Count,
                    Revenue = completed.Sum(b => b.Gross),
                    BookedDays = bookedDays,
                    PeriodDays = (int)periodDays,
                    UtilizationPercent = utilization,
                    IsUnderUtilized = periodDays > 0 && utilization < LowUtilizationPercent
                };
            })
            .OrderByDescending(i => i.Revenue)
            .ToList();

            if (items.Count > 1 && items[0].Revenue > 0)
            {
                items[0].PerformanceFlag = "Top";
                foreach (var weak in items.Skip(1).Where(i => i.Revenue < items[0].Revenue / 3))
                    weak.PerformanceFlag = "Under";
            }
            return items;
        }

        private static BookingFunnel BuildFunnel(List<RevenueBooking> inRange, DateTime today) => new()
        {
            Requested = inRange.Count,
            Accepted = inRange.Count(b => b.Status == "Accepted"),
            Ongoing = inRange.Count(b => b.Status == "Accepted" && b.StartDate.Date <= today && b.EndDate.Date >= today),
            Completed = inRange.Count(b => b.Status == "Completed"),
            Cancelled = inRange.Count(b => b.Status == "Cancelled"),
            Rejected = inRange.Count(b => b.Status == "Rejected")
        };

        private List<RevenueTransactionItem> BuildTransactions(List<RevenueBooking> inRange, IReadOnlyList<RevenueBooking> allBookings,
            Dictionary<int, decimal> expenseByBooking, string? status)
        {
            var confirmed = allBookings.Where(b => ConfirmedStatuses.Contains(b.Status)).ToList();

            return inRange
                .Where(b => string.IsNullOrEmpty(status) || b.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(b => b.StartDate)
                .Select(b => new RevenueTransactionItem
                {
                    BookingId = b.Id,
                    StartDate = b.StartDate,
                    EndDate = b.EndDate,
                    CustomerName = b.CustomerName,
                    ListingName = b.ListingName,
                    QuantityText = b.QuantityText,
                    Gross = b.Gross,
                    Commission = Commission(b.Gross),
                    Expenses = expenseByBooking.GetValueOrDefault(b.Id),
                    Status = b.Status,
                    IsRepeatCustomer = confirmed.Any(o => o.CustomerId == b.CustomerId && o.Id != b.Id && o.StartDate < b.StartDate)
                })
                .ToList();
        }

        private static RevenueInsights BuildInsights(List<RevenueBooking> inRange, IReadOnlyList<RevenueBooking> allBookings)
        {
            var insights = new RevenueInsights();
            var confirmedAll = allBookings.Where(b => ConfirmedStatuses.Contains(b.Status)).ToList();

            // Peak season: best 3 consecutive calendar months by booking start date (demand signal), needs ≥ 6 months of history
            var byMonth = new decimal[12];
            foreach (var b in confirmedAll) byMonth[b.StartDate.Month - 1] += b.Gross;
            if (byMonth.Count(v => v > 0) >= 6)
            {
                var best = Enumerable.Range(0, 12)
                    .Select(i => (Start: i, Total: byMonth[i] + byMonth[(i + 1) % 12] + byMonth[(i + 2) % 12]))
                    .OrderByDescending(x => x.Total)
                    .First();
                var months = CultureInfo.InvariantCulture.DateTimeFormat;
                insights.PeakSeason = $"{months.GetMonthName(best.Start + 1)} – {months.GetMonthName((best.Start + 2) % 12 + 1)}";
            }

            // Repeat customers among renters with confirmed/completed bookings in the range
            var confirmedCounts = confirmedAll.GroupBy(b => b.CustomerId).ToDictionary(g => g.Key, g => g.Count());
            var rentersInRange = inRange.Where(b => ConfirmedStatuses.Contains(b.Status)).Select(b => b.CustomerId).Distinct().ToList();
            insights.DistinctCustomers = rentersInRange.Count;
            insights.RepeatCustomers = rentersInRange.Count(c => confirmedCounts.GetValueOrDefault(c) >= 2);

            return insights;
        }

        private static string Csv(string value) =>
            value.Contains(',') || value.Contains('"') || value.Contains('\n')
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
    }
}
