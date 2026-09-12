using System.Globalization;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;

namespace KrishiLink.BLL.Services
{
    /// <summary>Small formatting/parsing helpers shared by the listing and booking services.</summary>
    internal static class ListingFormat
    {
        private const char Separator = '|';

        public static List<string> Split(string? delimited) =>
            string.IsNullOrWhiteSpace(delimited)
                ? new List<string>()
                : delimited.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        public static string Join(IEnumerable<string>? values) =>
            values is null ? string.Empty : string.Join(Separator, values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()));

        public static string Taka(decimal amount) => $"৳{amount:N0}";

        /// <summary>"02 Sep – 06 Sep 2026" (year shown once when both dates share it).</summary>
        public static string DateRange(DateTime start, DateTime end) =>
            start.Year == end.Year
                ? $"{start:dd MMM} – {end:dd MMM yyyy}"
                : $"{start:dd MMM yyyy} – {end:dd MMM yyyy}";

        public static string MemberSince(DateTime createdAt) => createdAt.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

        public static int InclusiveDays(DateTime start, DateTime end) => Math.Max(1, (end.Date - start.Date).Days + 1);

        public static double Months(DateTime start, DateTime end) => Math.Max(1, (end.Date - start.Date).TotalDays) / 30.0;
    }

    public record RateSegment(decimal Rate, int Days, string Label); // Label: "Base", rule.Name

    /// <summary>
    /// The single source of truth for what a booking costs. Used by the farmer views, the owner revenue
    /// repositories, the acceptance snapshot and the demo seed so every side sees the same number.
    /// </summary>
    public static class BookingPricing
    {
        public static decimal EquipmentGross(DateTime start, DateTime end, decimal dailyRate) =>
            ListingFormat.InclusiveDays(start, end) * dailyRate;

        /// <summary>
        /// Rule-aware dynamic equipment gross calculation honoring seasonal rules (highest precedence),
        /// weekend rules (medium precedence), and listing base daily rate (fallback).
        /// Consecutive identical-rate segments are grouped together.
        /// </summary>
        public static (decimal Gross, List<RateSegment> Segments) EquipmentGross(
            DateTime start,
            DateTime end,
            decimal baseRate,
            IReadOnlyList<EquipmentRateRule> activeRules,
            IReadOnlySet<DayOfWeek> weekendDays)
        {
            var s = start.Date;
            var t = end.Date;
            if (t < s) return (0m, new List<RateSegment>());

            var seasonRules = activeRules
                .Where(r => r.IsActive && r.Kind == RateRuleKind.Season && r.StartDate.HasValue && r.EndDate.HasValue)
                .ToList();

            var weekendRule = activeRules.FirstOrDefault(r => r.IsActive && r.Kind == RateRuleKind.Weekend);

            var dailyRates = new List<(decimal Rate, string Label)>();
            for (var d = s; d <= t; d = d.AddDays(1))
            {
                var season = seasonRules.FirstOrDefault(r => d >= r.StartDate!.Value.Date && d <= r.EndDate!.Value.Date);
                if (season != null)
                {
                    dailyRates.Add((season.DailyRate, season.Name));
                }
                else if (weekendRule != null && weekendDays.Contains(d.DayOfWeek))
                {
                    dailyRates.Add((weekendRule.DailyRate, weekendRule.Name));
                }
                else
                {
                    dailyRates.Add((baseRate, "Base"));
                }
            }

            var segments = new List<RateSegment>();
            if (dailyRates.Count > 0)
            {
                var currentRate = dailyRates[0].Rate;
                var currentLabel = dailyRates[0].Label;
                var currentDays = 1;

                for (int i = 1; i < dailyRates.Count; i++)
                {
                    if (dailyRates[i].Rate == currentRate && dailyRates[i].Label == currentLabel)
                    {
                        currentDays++;
                    }
                    else
                    {
                        segments.Add(new RateSegment(currentRate, currentDays, currentLabel));
                        currentRate = dailyRates[i].Rate;
                        currentLabel = dailyRates[i].Label;
                        currentDays = 1;
                    }
                }
                segments.Add(new RateSegment(currentRate, currentDays, currentLabel));
            }

            decimal gross = segments.Sum(seg => seg.Rate * seg.Days);
            return (gross, segments);
        }

        public static string Describe(IEnumerable<RateSegment> segments)
        {
            var list = segments.ToList();
            if (list.Count == 0) return string.Empty;

            if (list.Count == 1)
            {
                var s = list[0];
                var dayUnit = s.Days == 1 ? "day" : "days";
                return s.Label == "Base"
                    ? $"৳{s.Rate:N0} / day × {s.Days} {dayUnit}"
                    : $"৳{s.Rate:N0} / day × {s.Days} {dayUnit} ({s.Label})";
            }

            return string.Join(" + ", list.Select(s =>
                s.Label == "Base"
                    ? $"৳{s.Rate:N0} × {s.Days} {(s.Days == 1 ? "day" : "days")}"
                    : $"৳{s.Rate:N0} × {s.Days} {(s.Days == 1 ? "day" : "days")} ({s.Label})"));
        }

        public static decimal EquipmentGrossOf(EquipmentBooking b, decimal dailyRate) =>
            b.AgreedGross ?? (b.QuotedGross > 0 ? b.QuotedGross : EquipmentGross(b.StartDate, b.EndDate, dailyRate));

        public static decimal EquipmentGrossOf(decimal? agreedGross, decimal quotedGross, DateTime start, DateTime end, decimal dailyRate) =>
            agreedGross ?? (quotedGross > 0 ? quotedGross : EquipmentGross(start, end, dailyRate));

        public static decimal GodownGross(DateTime start, DateTime end, double tons, decimal pricePerTonPerMonth) =>
            decimal.Round((decimal)tons * pricePerTonPerMonth * (decimal)ListingFormat.Months(start, end), 0);

        public static decimal Commission(decimal gross, decimal rate) => decimal.Round(gross * rate, 0);

        /// <summary>Commission owed on a booking from its snapshot (0 for rows accepted before snapshots existed).</summary>
        public static decimal Commission(IPayableBooking b) => Commission(b.AgreedGross ?? 0, b.CommissionRate ?? 0);
    }

    /// <summary>Outcome of an owner decision. <see cref="AutoRejectedIds"/> lists pending requests that were declined as a side effect.</summary>
    public record DecisionResult(bool Success, string? Error = null, IReadOnlyList<int>? AutoRejectedIds = null)
    {
        public static DecisionResult Ok(IReadOnlyList<int>? autoRejected = null) => new(true, null, autoRejected ?? Array.Empty<int>());
        public static DecisionResult Fail(string error) => new(false, error, Array.Empty<int>());
    }

    /// <summary>Owner decision state machine shared by equipment rentals and godown storage bookings.</summary>
    internal static class BookingWorkflow
    {
        /// <summary>Reason stored on pending requests that lose out when the owner accepts an overlapping one.</summary>
        public const string AutoRejectReason = "Automatically declined: the owner accepted another booking for overlapping dates.";

        /// <summary>Decision the payment service uses to move an accepted booking to Paid; never accepted from the owner UI.</summary>
        public const string PaidDecision = "paid";

        /// <summary>Returns the new status for <paramref name="decision"/>, or null when the transition is not allowed.</summary>
        public static string? Next(string current, string decision) => (current, decision.ToLowerInvariant()) switch
        {
            (BookingStatus.Pending, "accept") => BookingStatus.Accepted,
            (BookingStatus.Pending, "reject") => BookingStatus.Rejected,
            (BookingStatus.Accepted, "undo") => BookingStatus.Pending,
            (BookingStatus.Rejected, "undo") => BookingStatus.Pending,
            (BookingStatus.Accepted, PaidDecision) => BookingStatus.Paid,
            (BookingStatus.Paid, "complete") => BookingStatus.Completed,
            (BookingStatus.Completed, "undo") => BookingStatus.Paid,
            _ => null
        };

        /// <summary>
        /// Data-dependent guards that the pure <see cref="Next"/> cannot see (payment and payout state).
        /// Returns an error message for the owner, or null when the decision may proceed.
        /// </summary>
        public static string? Guard(IPayableBooking b, string decision, string? next)
        {
            var d = decision.ToLowerInvariant();
            if (next is null)
                return b.Status == BookingStatus.Accepted && d == "complete"
                    ? "This booking can't be completed until the farmer has paid."
                    : $"A {b.Status.ToLowerInvariant()} request cannot be {(d == "undo" ? "undone" : d + "ed")}.";
            if (d == PaidDecision) return "Payment is confirmed by the farmer's checkout, not by the owner.";
            if (b.Status == BookingStatus.Completed && b.PayoutId is not null)
                return "This booking has already been paid out and can no longer be changed.";
            if (b.Status == BookingStatus.Accepted && next == BookingStatus.Pending && b.Payment?.Status == PaymentStatus.Succeeded)
                return "This booking has been paid by the farmer; cancel and refund it instead of undoing.";
            return null;
        }

        /// <summary>
        /// Money side-effects of a transition, applied before <c>Status</c> changes: the price snapshot on accept,
        /// and the commission ledger row on complete (or its reversal on undo). The caller's SaveChanges commits both.
        /// </summary>
        public static void ApplyMoney(IPayableBooking b, string next, string bookingType, string ownerId, ILedgerRepository ledger,
            decimal listingRate, decimal gross, decimal commissionRate)
        {
            if (next == BookingStatus.Accepted)
            {
                b.AgreedRate = listingRate;
                b.AgreedGross = gross;
                b.CommissionRate = commissionRate;
            }
            else if (next == BookingStatus.Completed)
            {
                b.CompletedOn = DateTime.UtcNow;
                if (BookingPricing.Commission(b) > 0) ledger.Add(LedgerPostings.CommissionEarned(bookingType, b, ownerId));
            }
            else if (b.Status == BookingStatus.Completed && next == BookingStatus.Paid)
            {
                b.CompletedOn = null;
                if (BookingPricing.Commission(b) > 0) ledger.Add(LedgerPostings.CommissionReversed(bookingType, b, ownerId));
            }
        }

        public static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd) =>
            aStart.Date <= bEnd.Date && bStart.Date <= aEnd.Date;
    }
}
