namespace KrishiLink.BLL.Helpers
{
    /// <summary>
    /// Utilities for expanding date intervals with optional weekday filters and grouping consecutive dates by reason.
    /// </summary>
    public static class DateRanges
    {
        public const int MaxBulkDays = 366;

        /// <summary>
        /// Expands an inclusive [from, to] date interval, optionally filtered to specific days of the week.
        /// </summary>
        public static IEnumerable<DateTime> Expand(DateTime from, DateTime to, IReadOnlyCollection<DayOfWeek>? daysOfWeek)
        {
            var f = from.Date;
            var t = to.Date;
            if (t < f) yield break;

            var filterDays = daysOfWeek != null && daysOfWeek.Count > 0;
            for (var d = f; d <= t; d = d.AddDays(1))
            {
                if (!filterDays || daysOfWeek!.Contains(d.DayOfWeek))
                {
                    yield return d;
                }
            }
        }

        /// <summary>
        /// Collapses consecutive dates with the exact same reason into contiguous date intervals [From, To].
        /// </summary>
        public static List<(DateTime From, DateTime To, string? Reason)> Group(IEnumerable<(DateTime Date, string? Reason)> dates)
        {
            var sorted = dates.OrderBy(d => d.Date.Date).ToList();
            var result = new List<(DateTime From, DateTime To, string? Reason)>();
            if (!sorted.Any()) return result;

            DateTime curFrom = sorted[0].Date.Date;
            DateTime curTo = sorted[0].Date.Date;
            string? curReason = sorted[0].Reason;

            for (int i = 1; i < sorted.Count; i++)
            {
                var nextDate = sorted[i].Date.Date;
                var nextReason = sorted[i].Reason;

                if (nextDate == curTo.AddDays(1) && string.Equals(curReason, nextReason, StringComparison.Ordinal))
                {
                    curTo = nextDate;
                }
                else
                {
                    result.Add((curFrom, curTo, curReason));
                    curFrom = nextDate;
                    curTo = nextDate;
                    curReason = nextReason;
                }
            }
            result.Add((curFrom, curTo, curReason));
            return result;
        }
    }
}
