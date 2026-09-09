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

    public interface IGodownRevenueService
    {
        GodownRevenueViewModel GetReport(string ownerId, RevenueFilter filter);
        string ExportCsv(string ownerId, RevenueFilter filter);
        BookingInvoiceViewModel? GetInvoice(string ownerId, int bookingId);
        bool AddExpense(string ownerId, int bookingId, decimal amount, string? note);
    }

    /// <summary>
    /// Revenue analytics for godown owners. Storage revenue is recognised when a booking completes
    /// (its EndDate); Accepted bookings count as upcoming revenue. All money is in BDT.
    /// </summary>
    public class GodownRevenueService : IGodownRevenueService
    {
        private const int MaxMonthBuckets = 24;
        private const int MaxWeekBuckets = 16;
        private const double DaysPerMonth = 30.0;
        private static readonly string[] ConfirmedStatuses = { "Accepted", "Completed" };

        private readonly IGodownRevenueRepository _repo;
        private readonly decimal _commissionRate;

        public GodownRevenueService(IGodownRevenueRepository repo, IOptions<RevenueOptions> options)
        {
            _repo = repo;
            _commissionRate = options.Value.PlatformCommissionRate;
        }

        public GodownRevenueViewModel GetReport(string ownerId, RevenueFilter filter)
        {
            var today = DateTime.Today;
            var (from, to) = ResolveRange(filter, today);

            var godowns = _repo.GetGodowns(ownerId);
            var allBookings = _repo.GetBookings(ownerId);
            var expenses = _repo.GetExpenses(ownerId);
            var payouts = _repo.GetPayouts(ownerId);
            var expenseByBooking = expenses.GroupBy(e => e.GodownBookingId).ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

            // Range + godown filters drive every analytics block; the status filter is for the transaction list only.
            var inRange = allBookings
                .Where(b => Overlaps(b, from, to) && (filter.GodownId is null || b.GodownId == filter.GodownId))
                .ToList();
            var scopedGodowns = godowns.Where(g => filter.GodownId is null || g.Id == filter.GodownId).ToList();

            var completedLifetime = allBookings.Where(b => b.Status == "Completed").ToList();
            var totalRevenue = completedLifetime.Sum(Gross);

            var model = new GodownRevenueViewModel
            {
                Filter = filter,
                RangeStart = from,
                RangeEnd = to,
                Godowns = godowns.Select(g => new RevenueGodownOption { Id = g.Id, Name = g.Name }).ToList(),

                TotalRevenue = totalRevenue,
                ThisMonthRevenue = completedLifetime
                    .Where(b => b.EndDate.Year == today.Year && b.EndDate.Month == today.Month)
                    .Sum(Gross),
                UpcomingRevenue = allBookings.Where(b => b.Status == "Accepted").Sum(Gross),
                CompletedBookings = completedLifetime.Count,

                Settlement = BuildSettlement(totalRevenue, expenses, payouts),
                Trend = BuildTrend(inRange, from, to, filter.IsWeekly),
                Breakdown = BuildBreakdown(scopedGodowns, inRange, from, to),
                Funnel = BuildFunnel(inRange, today),
                Transactions = BuildTransactions(inRange, allBookings, expenseByBooking, filter.Status),
                RecentPayout = payouts
                    .Where(p => p.Status == "Completed" && p.TransactionDate >= today.AddDays(-7))
                    .OrderByDescending(p => p.TransactionDate)
                    .Select(ToPayoutItem)
                    .FirstOrDefault()
            };

            model.Insights = BuildInsights(inRange, allBookings, model.Breakdown);
            return model;
        }

        public string ExportCsv(string ownerId, RevenueFilter filter)
        {
            var report = GetReport(ownerId, filter);
            var sb = new StringBuilder();
            sb.AppendLine("Booking ID,Start Date,End Date,Farmer,Godown,Storage (Tons),Months,Gross (BDT),Commission (BDT),Expenses (BDT),Net (BDT),Status");
            foreach (var t in report.Transactions)
            {
                sb.AppendLine(string.Join(",",
                    t.BookingId,
                    t.StartDate.ToString("yyyy-MM-dd"),
                    t.EndDate.ToString("yyyy-MM-dd"),
                    Csv(t.FarmerName),
                    Csv(t.GodownName),
                    t.StorageTons.ToString(CultureInfo.InvariantCulture),
                    t.Months.ToString("0.##", CultureInfo.InvariantCulture),
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
            var booking = _repo.GetBookings(ownerId).FirstOrDefault(b => b.Id == bookingId && b.Status == "Completed");
            if (booking?.Godown is null) return null;

            var gross = Gross(booking);
            return new BookingInvoiceViewModel
            {
                InvoiceNumber = $"KL-GB-{booking.Id:D6}",
                IssuedOn = booking.EndDate,
                FarmerName = booking.Farmer?.FullName ?? booking.FarmerId,
                FarmerLocation = booking.Farmer?.Location,
                GodownName = booking.Godown.Name,
                GodownLocation = booking.Godown.Location,
                StartDate = booking.StartDate,
                EndDate = booking.EndDate,
                StorageTons = booking.StorageTons,
                RatePerTonPerMonth = booking.Godown.PricePerTonPerMonth,
                Months = Months(booking),
                Gross = gross,
                CommissionRate = _commissionRate,
                Commission = Commission(gross),
                Status = booking.Status
            };
        }

        public bool AddExpense(string ownerId, int bookingId, decimal amount, string? note)
        {
            if (amount <= 0) return false;
            var booking = _repo.GetBookings(ownerId).FirstOrDefault(b => b.Id == bookingId && ConfirmedStatuses.Contains(b.Status));
            if (booking is null) return false;

            _repo.AddExpense(new BookingExpense
            {
                GodownBookingId = bookingId,
                OwnerId = ownerId,
                Amount = decimal.Round(amount, 2),
                Note = (note ?? string.Empty).Trim(),
                RecordedOn = DateTime.UtcNow
            });
            return true;
        }

        // ---- Pricing -------------------------------------------------------------------------

        private static double Months(GodownBooking b) =>
            Math.Max(0, (b.EndDate.Date - b.StartDate.Date).TotalDays) / DaysPerMonth;

        private static decimal Gross(GodownBooking b) =>
            b.Godown is null ? 0 : decimal.Round((decimal)b.StorageTons * b.Godown.PricePerTonPerMonth * (decimal)Months(b), 0);

        private decimal Commission(decimal gross) => decimal.Round(gross * _commissionRate, 0);

        private static bool Overlaps(GodownBooking b, DateTime from, DateTime to) =>
            b.StartDate.Date <= to && b.EndDate.Date >= from;

        // ---- Blocks --------------------------------------------------------------------------

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

        private static List<RevenueTrendPoint> BuildTrend(List<GodownBooking> inRange, DateTime from, DateTime to, bool weekly)
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
                        Amount = completed.Where(b => b.EndDate.Date >= start && b.EndDate.Date <= end).Sum(Gross)
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
                    Amount = completed.Where(b => b.EndDate.Year == m.Year && b.EndDate.Month == m.Month).Sum(Gross)
                });
            }
            return points;
        }

        private static List<GodownRevenueBreakdownItem> BuildBreakdown(List<Godown> godowns, List<GodownBooking> inRange, DateTime from, DateTime to)
        {
            var rangeDays = (to - from).TotalDays + 1;
            var items = godowns.Select(g =>
            {
                var completed = inRange.Where(b => b.GodownId == g.Id && b.Status == "Completed").ToList();
                var bookedTonDays = inRange
                    .Where(b => b.GodownId == g.Id && ConfirmedStatuses.Contains(b.Status))
                    .Sum(b => b.StorageTons * OverlapDays(b, from, to));
                var capacityTonDays = g.CapacityInTons * rangeDays;

                return new GodownRevenueBreakdownItem
                {
                    GodownId = g.Id,
                    Name = g.Name,
                    Bookings = completed.Count,
                    Revenue = completed.Sum(Gross),
                    UtilizationPercent = capacityTonDays > 0 ? (int)Math.Round(Math.Min(100, bookedTonDays / capacityTonDays * 100)) : 0
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

        private static double OverlapDays(GodownBooking b, DateTime from, DateTime to)
        {
            var start = b.StartDate.Date > from ? b.StartDate.Date : from;
            var end = b.EndDate.Date < to ? b.EndDate.Date : to;
            return Math.Max(0, (end - start).TotalDays + 1);
        }

        private static BookingFunnel BuildFunnel(List<GodownBooking> inRange, DateTime today) => new()
        {
            Requested = inRange.Count,
            Accepted = inRange.Count(b => b.Status == "Accepted"),
            Ongoing = inRange.Count(b => b.Status == "Accepted" && b.StartDate.Date <= today && b.EndDate.Date >= today),
            Completed = inRange.Count(b => b.Status == "Completed"),
            Cancelled = inRange.Count(b => b.Status == "Cancelled"),
            Rejected = inRange.Count(b => b.Status == "Rejected")
        };

        private List<RevenueTransactionItem> BuildTransactions(List<GodownBooking> inRange, IReadOnlyList<GodownBooking> allBookings,
            Dictionary<int, decimal> expenseByBooking, string? status)
        {
            var confirmed = allBookings.Where(b => ConfirmedStatuses.Contains(b.Status)).ToList();

            return inRange
                .Where(b => string.IsNullOrEmpty(status) || b.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(b => b.StartDate)
                .Select(b =>
                {
                    var gross = Gross(b);
                    return new RevenueTransactionItem
                    {
                        BookingId = b.Id,
                        StartDate = b.StartDate,
                        EndDate = b.EndDate,
                        FarmerName = b.Farmer?.FullName ?? b.FarmerId,
                        GodownName = b.Godown?.Name ?? string.Empty,
                        StorageTons = b.StorageTons,
                        Months = Months(b),
                        Gross = gross,
                        Commission = Commission(gross),
                        Expenses = expenseByBooking.GetValueOrDefault(b.Id),
                        Status = b.Status,
                        IsRepeatCustomer = confirmed.Any(o => o.FarmerId == b.FarmerId && o.Id != b.Id && o.StartDate < b.StartDate)
                    };
                })
                .ToList();
        }

        private static RevenueInsights BuildInsights(List<GodownBooking> inRange,
            IReadOnlyList<GodownBooking> allBookings, List<GodownRevenueBreakdownItem> breakdown)
        {
            var insights = new RevenueInsights();

            // Peak season: best 3 consecutive calendar months by booking start date (demand signal), needs ≥ 6 months of history
            var byMonth = new decimal[12];
            foreach (var b in allBookings.Where(b => ConfirmedStatuses.Contains(b.Status))) byMonth[b.StartDate.Month - 1] += Gross(b);
            if (byMonth.Count(v => v > 0) >= 6)
            {
                var best = Enumerable.Range(0, 12)
                    .Select(i => (Start: i, Total: byMonth[i] + byMonth[(i + 1) % 12] + byMonth[(i + 2) % 12]))
                    .OrderByDescending(x => x.Total)
                    .First();
                var culture = CultureInfo.InvariantCulture;
                insights.PeakSeason = $"{culture.DateTimeFormat.GetMonthName(best.Start + 1)} – {culture.DateTimeFormat.GetMonthName((best.Start + 2) % 12 + 1)}";
            }

            // Repeat customers among renters with confirmed/completed bookings in the range
            var confirmedCounts = allBookings
                .Where(b => ConfirmedStatuses.Contains(b.Status))
                .GroupBy(b => b.FarmerId)
                .ToDictionary(g => g.Key, g => g.Count());
            var rentersInRange = inRange.Where(b => ConfirmedStatuses.Contains(b.Status)).Select(b => b.FarmerId).Distinct().ToList();
            insights.DistinctCustomers = rentersInRange.Count;
            insights.RepeatCustomers = rentersInRange.Count(f => confirmedCounts.GetValueOrDefault(f) >= 2);

            // Top vs. weakest listing in the range
            if (breakdown.Count > 1 && breakdown[0].Revenue > 0)
            {
                var weakest = breakdown[^1];
                insights.TopListing = breakdown[0].Name;
                insights.WeakestListing = weakest.Name;
                insights.TopVsWeakestMultiplier = weakest.Revenue > 0
                    ? Math.Round((double)(breakdown[0].Revenue / weakest.Revenue), 1)
                    : null;
            }

            return insights;
        }

        private static string Csv(string value) =>
            value.Contains(',') || value.Contains('"') || value.Contains('\n')
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
    }
}
