using System.Globalization;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;

namespace KrishiLink.BLL.Services
{
    /// <summary>Bound from the "Revenue" section of appsettings.json.</summary>
    public class RevenueOptions
    {
        public const string SectionName = "Revenue";

        /// <summary>Fraction of gross booking value retained by the platform (0.05 = 5%).</summary>
        public decimal PlatformCommissionRate { get; set; } = 0.05m;

        /// <summary>Below this share of elapsed days booked, a listing gets a pricing/listing-quality suggestion.</summary>
        public int LowUtilizationPercent { get; set; } = 25;

        /// <summary>A listing earning less than this share of the top listing's revenue is flagged "Under-performing" (0.33 = one third).</summary>
        public decimal UnderPerformerShare { get; set; } = 0.33m;

        /// <summary>Rows per page in the revenue transactions table.</summary>
        public int TransactionsPageSize { get; set; } = 25;
    }

    public interface IOwnerRevenueService
    {
        OwnerRevenueViewModel GetReport(string ownerId, RevenueFilter filter);
        BookingInvoiceViewModel? GetInvoice(string ownerId, int bookingId);

        /// <summary>Builds the bank-statement style PDF for one calendar month; returns the bytes and a file name.</summary>
        (byte[] Content, string FileName) GenerateMonthlyStatement(string ownerId, DateTime month, StatementOwner owner);

        PayoutHistoryViewModel GetPayoutHistory(string ownerId);

        /// <summary>Creates a "Processing" payout for every unpaid completed booking. Returns an error message, or null on success.</summary>
        Task<string?> RequestPayoutAsync(string ownerId, string method, string? account);
        string? RequestPayout(string ownerId, string method, string? account);

        /// <summary>Adds (expenseId null) or updates an expense. Returns an error message, or null on success.</summary>
        string? SaveExpense(string ownerId, int? expenseId, int bookingId, decimal amount, string? note);
        bool DeleteExpense(string ownerId, int expenseId);
    }

    public interface IGodownRevenueService : IOwnerRevenueService { }

    public interface IEquipmentRevenueService : IOwnerRevenueService { }

    /// <summary>Listing-type specific wording used by the shared revenue view and invoice.</summary>
    public record RevenueProfile(string ListingLabel, string UtilizationHint, string InvoicePrefix, string InvoiceTitle);

    public class GodownRevenueService : OwnerRevenueService, IGodownRevenueService
    {
        public GodownRevenueService(IGodownRevenueRepository repo, IOptions<RevenueOptions> options, INotificationService notifications)
            : base(repo, options, new RevenueProfile("Godown", "at full capacity (ton-days)", "KL-GB", "Godown Storage Receipt"), notifications) { }
    }

    public class EquipmentRevenueService : OwnerRevenueService, IEquipmentRevenueService
    {
        public EquipmentRevenueService(IEquipmentRevenueRepository repo, IOptions<RevenueOptions> options, INotificationService notifications)
            : base(repo, options, new RevenueProfile("Equipment", "rented", "KL-EQ", "Equipment Rental Receipt"), notifications) { }
    }

    /// <summary>
    /// Revenue analytics shared by godown and equipment owners. Revenue is recognised when a booking
    /// completes (its EndDate); Accepted bookings count as upcoming revenue. All money is in BDT.
    /// Pricing is resolved by the repository, so this class only aggregates.
    /// </summary>
    public class OwnerRevenueService : IOwnerRevenueService
    {
        private const int MaxMonthBuckets = 24;
        private const int MaxTrendBuckets = 16;
        private static readonly string[] ConfirmedStatuses = { BookingStatus.Accepted, BookingStatus.Paid, BookingStatus.Completed };

        private readonly IOwnerRevenueRepository _repo;
        private readonly RevenueProfile _profile;
        private readonly RevenueOptions _options;
        private readonly INotificationService _notifications;

        /// <summary>Current rate for display and for rows that pre-date snapshots (e.g. still-pending requests).</summary>
        private decimal CommissionRate => _options.PlatformCommissionRate;

        public OwnerRevenueService(IOwnerRevenueRepository repo, IOptions<RevenueOptions> options, RevenueProfile profile, INotificationService notifications)
        {
            _repo = repo;
            _profile = profile;
            _options = options.Value;
            _notifications = notifications;
        }

        public OwnerRevenueViewModel GetReport(string ownerId, RevenueFilter filter)
        {
            var today = DateTime.Today;
            var (from, to) = ResolveRange(filter, today);

            var listings = _repo.GetListings(ownerId);
            var allBookings = _repo.GetBookings(ownerId);
            var expenses = _repo.GetExpenses(ownerId);
            var payouts = _repo.GetPayouts(ownerId);

            // Range + listing filters drive every analytics block; the status filter is for the transaction list only.
            var inRange = allBookings
                .Where(b => Overlaps(b, from, to) && (filter.ListingId is null || b.ListingId == filter.ListingId))
                .ToList();
            var scopedListings = listings.Where(l => filter.ListingId is null || l.Id == filter.ListingId).ToList();

            var completedLifetime = allBookings.Where(b => b.Status == BookingStatus.Completed).ToList();
            var totalRevenue = completedLifetime.Sum(b => b.Gross);
            var lastMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-1);

            var (trend, bucketDays, trendNote) = BuildTrend(inRange, from, to, filter.IsWeekly);
            var breakdown = BuildBreakdown(scopedListings, inRange, from, to, today);
            var allTransactions = BuildTransactions(inRange, allBookings, expenses, filter.Status);
            var pageSize = Math.Max(1, _options.TransactionsPageSize);
            var pageCount = Math.Max(1, (int)Math.Ceiling(allTransactions.Count / (double)pageSize));
            filter.Page = Math.Clamp(filter.Page, 1, pageCount);

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
                LastMonthRevenue = completedLifetime
                    .Where(b => b.EndDate.Year == lastMonth.Year && b.EndDate.Month == lastMonth.Month)
                    .Sum(b => b.Gross),
                UpcomingRevenue = allBookings.Where(b => BookingStatus.Confirmed.Contains(b.Status)).Sum(b => b.Gross),
                CompletedBookings = completedLifetime.Count,

                Settlement = BuildSettlement(allBookings, expenses, payouts),
                Trend = trend,
                TrendBucketDays = bucketDays,
                TrendNote = trendNote,
                Breakdown = SortBreakdown(breakdown, filter.Sort),
                Funnel = BuildFunnel(inRange, today),
                Transactions = allTransactions.Skip((filter.Page - 1) * pageSize).Take(pageSize).ToList(),
                TransactionsTotal = allTransactions.Count,
                PageCount = pageCount,
                Insights = BuildInsights(inRange, allBookings),
                RecentPayout = payouts
                    .Where(p => p.Status == PayoutStatus.Completed && p.TransactionDate >= today.AddDays(-7))
                    .OrderByDescending(p => p.TransactionDate)
                    .Select(p => ToPayoutItem(p, allBookings))
                    .FirstOrDefault()
            };

            // Top vs. weakest listing in the range (breakdown is built revenue-desc before any user sort)
            model.Insights.UnderUtilizedListings = breakdown.Count(b => b.IsUnderUtilized);
            model.Insights.LowUtilizationPercent = _options.LowUtilizationPercent;
            if (breakdown.Count > 1 && breakdown[0].Revenue > 0)
            {
                var weakest = breakdown[^1];
                model.Insights.TopListing = breakdown[0].Name;
                model.Insights.WeakestListing = weakest.Name;
                model.Insights.TopVsWeakestMultiplier = weakest.Revenue > 0
                    ? Math.Round((double)(breakdown[0].Revenue / weakest.Revenue), 1)
                    : null;
            }

            return model;
        }

        public BookingInvoiceViewModel? GetInvoice(string ownerId, int bookingId)
        {
            var b = _repo.GetBookings(ownerId).FirstOrDefault(x => x.Id == bookingId && x.Status == BookingStatus.Completed);
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
                CommissionRate = RateFor(b),
                Commission = Commission(b),
                Status = b.Status,
                PaymentMethod = b.PaymentMethod,
                PaymentReference = b.PaymentReference,
                PaidOn = b.PaidOn
            };
        }

        public (byte[] Content, string FileName) GenerateMonthlyStatement(string ownerId, DateTime month, StatementOwner owner)
        {
            var first = new DateTime(month.Year, month.Month, 1);
            var report = GetReport(ownerId, new RevenueFilter { From = first, To = first.AddMonths(1).AddDays(-1) });
            var document = new MonthlyStatementDocument(report, owner, first);
            return (document.GeneratePdf(), document.FileName);
        }

        public string? SaveExpense(string ownerId, int? expenseId, int bookingId, decimal amount, string? note)
        {
            if (amount <= 0) return "Expense must be a positive amount.";
            var booking = _repo.GetBookings(ownerId).FirstOrDefault(b => b.Id == bookingId && ConfirmedStatuses.Contains(b.Status));
            if (booking is null) return "Expenses can only be recorded against accepted or completed bookings.";

            var expense = expenseId is null ? null : _repo.GetExpense(ownerId, expenseId.Value);
            if (expenseId is not null && expense is null) return "That expense no longer exists.";

            if (expense is null)
            {
                _repo.AddExpense(new BookingExpense
                {
                    BookingId = bookingId,
                    OwnerId = ownerId,
                    Amount = decimal.Round(amount, 2),
                    Note = (note ?? string.Empty).Trim(),
                    RecordedOn = DateTime.UtcNow
                });
            }
            else
            {
                expense.Amount = decimal.Round(amount, 2);
                expense.Note = (note ?? string.Empty).Trim();
                _repo.UpdateExpense(expense);
            }
            return null;
        }

        public bool DeleteExpense(string ownerId, int expenseId)
        {
            var expense = _repo.GetExpense(ownerId, expenseId);
            if (expense is null) return false;
            _repo.RemoveExpense(expense);
            return true;
        }

        public PayoutHistoryViewModel GetPayoutHistory(string ownerId)
        {
            var bookings = _repo.GetBookings(ownerId);
            return new PayoutHistoryViewModel
            {
                ListingLabel = _profile.ListingLabel,
                CompletedBookings = bookings.Count(b => b.Status == BookingStatus.Completed),
                Settlement = BuildSettlement(bookings, _repo.GetExpenses(ownerId), _repo.GetPayouts(ownerId))
            };
        }

        public string? RequestPayout(string ownerId, string method, string? account) =>
            RequestPayoutAsync(ownerId, method, account).GetAwaiter().GetResult();

        public async Task<string?> RequestPayoutAsync(string ownerId, string method, string? account)
        {
            if (!PayoutHistoryViewModel.PayoutMethods.Contains(method)) return "Please choose a valid payout method.";
            if (string.IsNullOrWhiteSpace(account) || account.Trim().Length < 6) return "Please enter the account or wallet number the payout should go to.";

            // Only an in-flight transfer blocks a new request; a Failed one has already released its bookings.
            if (_repo.GetPayouts(ownerId).Any(p => p.Status == PayoutStatus.Processing))
                return "A payout is already being processed. Please wait for it to complete.";

            // Settle exactly the completed bookings that no earlier payout has covered
            var unpaid = _repo.GetBookings(ownerId).Where(b => b.Status == BookingStatus.Completed && b.PayoutId is null).ToList();
            if (unpaid.Count == 0) return "There is no pending balance to pay out yet.";

            // Escrow can only pay out what the farmer actually put in: every booking needs a matching succeeded payment.
            var unfunded = unpaid.FirstOrDefault(b => !b.IsPaid);
            if (unfunded is not null)
                return $"Booking #{unfunded.Id} has no confirmed farmer payment, so it cannot be paid out yet. Please contact support.";

            var gross = unpaid.Sum(b => b.Gross);
            var commission = unpaid.Sum(Commission);
            var netAmount = gross - commission;
            var payoutId = _repo.AddPayout(new Transaction
            {
                UserId = ownerId,
                Reference = $"KL-PO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                ListingType = _profile.ListingLabel,
                GrossAmount = gross,
                Commission = commission,
                Amount = netAmount,
                PaymentMethod = method,
                PayoutAccount = account.Trim(),
                Status = PayoutStatus.Processing,
                TransactionDate = DateTime.Now
            });
            _repo.MarkBookingsPaid(unpaid.Select(b => b.Id), payoutId);

            var payoutLink = AppLinks.OwnerPayouts(_profile.ListingLabel);

            await _notifications.NotifyAsync(new NotificationRequest
            {
                UserId = ownerId,
                Type = NotificationTypes.PayoutProcessed,
                TitleKey = "Payout Requested",
                MessageKey = "Your payout request of ৳{0:N0} via {1} is being processed.",
                Args = new object[] { netAmount, method },
                LinkUrl = payoutLink,
                DedupeKey = $"payout:{payoutId}:Requested",
                SendEmail = false
            });

            return null;
        }

        // ---- Helpers -------------------------------------------------------------------------

        private decimal RateFor(RevenueBooking b) => b.CommissionRateSnapshot > 0 ? b.CommissionRateSnapshot : CommissionRate;

        /// <summary>Commission from the booking's own snapshot, so a later change to the platform rate never rewrites history.</summary>
        private decimal Commission(RevenueBooking b) => BookingPricing.Commission(b.Gross, RateFor(b));

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

        private RevenueSettlement BuildSettlement(IReadOnlyList<RevenueBooking> all, IReadOnlyList<BookingExpense> expenses, IReadOnlyList<Transaction> payouts)
        {
            var completed = all.Where(b => b.Status == BookingStatus.Completed).ToList();
            var gross = completed.Sum(b => b.Gross);
            var unpaid = completed.Where(b => b.PayoutId is null).ToList();
            return new RevenueSettlement
            {
                Gross = gross,
                CommissionRate = CommissionRate,
                Commission = completed.Sum(Commission),
                Expenses = expenses.Sum(e => e.Amount),
                InEscrow = all.Where(b => b.Status == BookingStatus.Paid).Sum(b => b.Gross),
                PaidOut = payouts.Where(p => p.Status == PayoutStatus.Completed).Sum(p => p.Amount),
                Processing = payouts.Where(p => p.Status == PayoutStatus.Processing).Sum(p => p.Amount),
                Owed = unpaid.Sum(b => b.Gross - Commission(b)),
                UnpaidBookings = unpaid.Count,
                Payouts = payouts.OrderByDescending(p => p.TransactionDate).Select(p => ToPayoutItem(p, completed)).ToList()
            };
        }

        private static PayoutItem ToPayoutItem(Transaction p, IReadOnlyList<RevenueBooking> bookings) => new()
        {
            Id = p.Id,
            Date = p.TransactionDate,
            Reference = p.Reference,
            Gross = p.GrossAmount,
            Commission = p.Commission,
            Amount = p.Amount,
            Method = p.PaymentMethod,
            Account = p.PayoutAccount,
            Status = p.Status,
            SettledOn = p.SettledOn,
            FailureReason = p.FailureReason,
            BookingCount = bookings.Count(b => b.PayoutId == p.Id)
        };

        /// <summary>
        /// Monthly buckets (capped at 24) or, for weekly, 7-day buckets that automatically widen to fortnights when the
        /// range would exceed <see cref="MaxTrendBuckets"/>; if even fortnights overflow, older buckets are dropped and a note says so.
        /// </summary>
        private static (List<RevenueTrendPoint> Points, int BucketDays, string? Note) BuildTrend(List<RevenueBooking> inRange, DateTime from, DateTime to, bool weekly)
        {
            var completed = inRange.Where(b => b.Status == BookingStatus.Completed).ToList();
            var points = new List<RevenueTrendPoint>();

            if (weekly)
            {
                var weeksInRange = (int)Math.Ceiling(((to - from).TotalDays + 1) / 7.0);
                var bucketDays = weeksInRange > MaxTrendBuckets ? 14 : 7;
                var bucketsNeeded = (int)Math.Ceiling(((to - from).TotalDays + 1) / bucketDays);
                var note = bucketsNeeded > MaxTrendBuckets
                    ? $"Showing the most recent {MaxTrendBuckets} fortnights of the selected range."
                    : bucketDays == 14 ? "Range is longer than 16 weeks, so the chart shows fortnights." : null;

                // Buckets end on the range end, newest last (weeks are aligned to Monday)
                var bucketEnd = bucketDays == 7 ? to.AddDays(-(((int)to.DayOfWeek + 6) % 7)).AddDays(6) : to;
                var starts = new List<DateTime>();
                for (var i = 0; i < MaxTrendBuckets && bucketEnd >= from; i++, bucketEnd = bucketEnd.AddDays(-bucketDays))
                    starts.Add(bucketEnd.AddDays(-(bucketDays - 1)));
                starts.Reverse();

                foreach (var start in starts)
                {
                    var end = start.AddDays(bucketDays - 1);
                    points.Add(new RevenueTrendPoint
                    {
                        Label = start.ToString("d MMM", CultureInfo.InvariantCulture),
                        Amount = completed.Where(b => b.EndDate.Date >= start && b.EndDate.Date <= end).Sum(b => b.Gross)
                    });
                }
                return (points, bucketDays, note);
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
            return (points, 0, months > MaxMonthBuckets ? $"Showing the most recent {MaxMonthBuckets} months of the selected range." : null);
        }

        private static List<ListingRevenueBreakdownItem> SortBreakdown(List<ListingRevenueBreakdownItem> items, string? sort) =>
            (sort ?? string.Empty).ToLowerInvariant() switch
            {
                "utilization" => items.OrderByDescending(i => i.UtilizationPercent).ThenByDescending(i => i.Revenue).ToList(),
                "avg" => items.OrderByDescending(i => i.AveragePerBooking).ThenByDescending(i => i.Revenue).ToList(),
                "bookings" => items.OrderByDescending(i => i.Bookings).ThenByDescending(i => i.Revenue).ToList(),
                _ => items
            };

        private List<ListingRevenueBreakdownItem> BuildBreakdown(List<RevenueListing> listings, List<RevenueBooking> inRange, DateTime from, DateTime to, DateTime today)
        {
            // Utilization only counts days that have actually elapsed — future accepted days aren't "used" yet
            var usageEnd = to < today ? to : today;
            var periodDays = Math.Max(0, (usageEnd - from).TotalDays + 1);

            var items = listings.Select(l =>
            {
                var completed = inRange.Where(b => b.ListingId == l.Id && b.Status == BookingStatus.Completed).ToList();
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
                    IsUnderUtilized = periodDays > 0 && utilization < _options.LowUtilizationPercent
                };
            })
            .OrderByDescending(i => i.Revenue)
            .ToList();

            if (items.Count > 1 && items[0].Revenue > 0)
            {
                items[0].PerformanceFlag = "Top";
                foreach (var weak in items.Skip(1).Where(i => i.Revenue < items[0].Revenue * _options.UnderPerformerShare))
                    weak.PerformanceFlag = "Under";
            }
            return items;
        }

        private static BookingFunnel BuildFunnel(List<RevenueBooking> inRange, DateTime today) => new()
        {
            Requested = inRange.Count,
            Accepted = inRange.Count(b => b.Status == BookingStatus.Accepted),
            Paid = inRange.Count(b => b.Status == BookingStatus.Paid),
            Ongoing = inRange.Count(b => BookingStatus.Confirmed.Contains(b.Status) && b.StartDate.Date <= today && b.EndDate.Date >= today),
            Completed = inRange.Count(b => b.Status == BookingStatus.Completed),
            Cancelled = inRange.Count(b => b.Status == BookingStatus.Cancelled),
            Rejected = inRange.Count(b => b.Status == BookingStatus.Rejected)
        };

        private List<RevenueTransactionItem> BuildTransactions(List<RevenueBooking> inRange, IReadOnlyList<RevenueBooking> allBookings,
            IReadOnlyList<BookingExpense> expenses, string? status)
        {
            var confirmed = allBookings.Where(b => ConfirmedStatuses.Contains(b.Status)).ToList();
            var expensesByBooking = expenses.GroupBy(e => e.BookingId).ToDictionary(g => g.Key, g => g
                .OrderBy(e => e.RecordedOn)
                .Select(e => new ExpenseLine { Id = e.Id, Amount = e.Amount, Note = e.Note })
                .ToList());

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
                    Commission = Commission(b),
                    ExpenseLines = expensesByBooking.GetValueOrDefault(b.Id) ?? new List<ExpenseLine>(),
                    Status = b.Status,
                    PayoutReference = b.PayoutReference,
                    IsPaid = b.IsPaid,
                    PaymentReference = b.PaymentReference,
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
    }
}
