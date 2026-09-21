using System;
using System.Collections.Generic;

namespace KrishiLink.BLL.Services
{
    public class PricingOptions
    {
        public const string SectionName = "Pricing";

        /// <summary>Day names considered as weekend days, e.g. ["Friday", "Saturday"] for Bangladesh.</summary>
        public string[] WeekendDays { get; set; } = new[] { "Friday", "Saturday" };

        public IReadOnlySet<DayOfWeek> WeekendDaySet()
        {
            var set = new HashSet<DayOfWeek>();
            if (WeekendDays != null)
            {
                foreach (var day in WeekendDays)
                {
                    if (Enum.TryParse<DayOfWeek>(day, true, out var dow))
                    {
                        set.Add(dow);
                    }
                }
            }
            if (set.Count == 0)
            {
                set.Add(DayOfWeek.Friday);
                set.Add(DayOfWeek.Saturday);
            }
            return set;
        }
    }
}
