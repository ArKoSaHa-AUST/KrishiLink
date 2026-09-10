using System.Globalization;
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

    /// <summary>Owner decision state machine shared by equipment rentals and godown storage bookings.</summary>
    internal static class BookingWorkflow
    {
        /// <summary>Returns the new status for <paramref name="decision"/>, or null when the transition is not allowed.</summary>
        public static string? Next(string current, string decision) => (current, decision.ToLowerInvariant()) switch
        {
            (BookingStatus.Pending, "accept") => BookingStatus.Accepted,
            (BookingStatus.Pending, "reject") => BookingStatus.Rejected,
            (BookingStatus.Accepted, "complete") => BookingStatus.Completed,
            (BookingStatus.Accepted, "undo") => BookingStatus.Pending,
            (BookingStatus.Rejected, "undo") => BookingStatus.Pending,
            (BookingStatus.Completed, "undo") => BookingStatus.Accepted,
            _ => null
        };

        public static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd) =>
            aStart.Date <= bEnd.Date && bStart.Date <= aEnd.Date;
    }
}
