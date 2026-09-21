using System;

namespace KrishiLink.BLL.Helpers
{
    /// <summary>
    /// Calendar days for scheduled work. Bookings are stored as dates with no time component and are
    /// read by farmers in Bangladesh, so "today" and "tomorrow" must be resolved in Asia/Dhaka rather
    /// than in whatever zone the host happens to run in.
    /// </summary>
    public static class BangladeshClock
    {
        private static readonly TimeZoneInfo Zone = ResolveZone();

        /// <summary>Current wall-clock time in Bangladesh.</summary>
        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

        /// <summary>Current calendar date in Bangladesh.</summary>
        public static DateTime Today => Now.Date;

        private static TimeZoneInfo ResolveZone()
        {
            foreach (var id in new[] { "Asia/Dhaka", "Bangladesh Standard Time" })
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(id);
                }
                catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
                {
                    // Try the next identifier; hosts carry either the IANA or the Windows name.
                }
            }

            // Bangladesh has observed a fixed UTC+06:00 offset with no daylight saving since 2010.
            return TimeZoneInfo.CreateCustomTimeZone("KrishiLink/Dhaka", TimeSpan.FromHours(6), "Bangladesh Standard Time", "Bangladesh Standard Time");
        }
    }
}
