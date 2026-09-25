using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;

namespace KrishiLink.Tests;

public class BookingPricingTests
{
    private static readonly IReadOnlySet<DayOfWeek> Weekend = new HashSet<DayOfWeek> { DayOfWeek.Friday, DayOfWeek.Saturday };

    // 2026-09-21 is a Monday; 25 Fri, 26 Sat, 27 Sun.
    private static readonly DateTime Monday = new(2026, 9, 21);

    [Fact]
    public void Base_rate_is_units_times_daily_rate_times_inclusive_days()
    {
        var (gross, segments) = BookingPricing.EquipmentGross(Monday, Monday.AddDays(2), 1000m, Array.Empty<EquipmentRateRule>(), Weekend, units: 2);

        Assert.Equal(6000m, gross);
        Assert.Equal(new RateSegment(1000m, 3, "Base"), Assert.Single(segments));
        Assert.Equal(6000m, BookingPricing.EquipmentGross(Monday, Monday.AddDays(2), 1000m, units: 2));
    }

    [Fact]
    public void Single_day_rental_counts_as_one_day()
    {
        Assert.Equal(1, ListingFormat.InclusiveDays(Monday, Monday));
        Assert.Equal(1000m, BookingPricing.EquipmentGross(Monday, Monday, 1000m));
    }

    [Fact]
    public void Weekend_rule_applies_only_on_weekend_days()
    {
        var rules = new[] { Rule(RateRuleKind.Weekend, "Weekend", 1500m) };

        var (gross, segments) = BookingPricing.EquipmentGross(Monday.AddDays(3), Monday.AddDays(6), 1000m, rules, Weekend);

        // Thu base, Fri+Sat weekend, Sun base
        Assert.Equal(1000m + 1500m * 2 + 1000m, gross);
        Assert.Equal(new[]
        {
            new RateSegment(1000m, 1, "Base"),
            new RateSegment(1500m, 2, "Weekend"),
            new RateSegment(1000m, 1, "Base")
        }, segments);
    }

    [Fact]
    public void Season_rule_takes_precedence_over_weekend_rule()
    {
        var rules = new[]
        {
            Rule(RateRuleKind.Weekend, "Weekend", 1500m),
            Rule(RateRuleKind.Season, "Boro harvest", 2000m, Monday.AddDays(4), Monday.AddDays(5))
        };

        var (gross, segments) = BookingPricing.EquipmentGross(Monday.AddDays(3), Monday.AddDays(6), 1000m, rules, Weekend);

        // Thu base, Fri+Sat season (not weekend), Sun base
        Assert.Equal(1000m + 2000m * 2 + 1000m, gross);
        Assert.Contains(new RateSegment(2000m, 2, "Boro harvest"), segments);
        Assert.DoesNotContain(segments, s => s.Label == "Weekend");
    }

    [Fact]
    public void Inactive_rules_are_ignored()
    {
        var season = Rule(RateRuleKind.Season, "Off", 9000m, Monday, Monday.AddDays(6));
        season.IsActive = false;
        var weekend = Rule(RateRuleKind.Weekend, "Weekend", 9000m);
        weekend.IsActive = false;

        var (gross, _) = BookingPricing.EquipmentGross(Monday, Monday.AddDays(6), 1000m, new[] { season, weekend }, Weekend);

        Assert.Equal(7000m, gross);
    }

    [Fact]
    public void End_before_start_prices_to_zero()
    {
        var (gross, segments) = BookingPricing.EquipmentGross(Monday, Monday.AddDays(-1), 1000m, Array.Empty<EquipmentRateRule>(), Weekend);
        Assert.Equal(0m, gross);
        Assert.Empty(segments);
    }

    [Fact]
    public void Units_scale_rule_aware_price()
    {
        var rules = new[] { Rule(RateRuleKind.Weekend, "Weekend", 1500m) };
        var (one, _) = BookingPricing.EquipmentGross(Monday, Monday.AddDays(6), 1000m, rules, Weekend, units: 1);
        var (three, _) = BookingPricing.EquipmentGross(Monday, Monday.AddDays(6), 1000m, rules, Weekend, units: 3);
        Assert.Equal(one * 3, three);
    }

    [Fact]
    public void Describe_lists_each_segment_and_units()
    {
        using var _ = new InvariantCultureScope();
        var segments = new[] { new RateSegment(1000m, 1, "Base"), new RateSegment(1500m, 2, "Weekend") };
        Assert.Equal("৳1,000 × 1 day + ৳1,500 × 2 days (Weekend) × 2 units", BookingPricing.Describe(segments, 2));
        Assert.Equal("৳1,000 / day × 3 days", BookingPricing.Describe(new[] { new RateSegment(1000m, 3, "Base") }));
        Assert.Equal(string.Empty, BookingPricing.Describe(Array.Empty<RateSegment>()));
    }

    [Fact]
    public void Accepted_snapshot_wins_over_quote_and_quote_wins_over_list_price()
    {
        var booking = new EquipmentBooking { StartDate = Monday, EndDate = Monday.AddDays(1), Units = 1, QuotedGross = 2500m };
        Assert.Equal(2500m, BookingPricing.EquipmentGrossOf(booking, 1000m));

        booking.AgreedGross = 2750m;
        Assert.Equal(2750m, BookingPricing.EquipmentGrossOf(booking, 1000m));

        booking.AgreedGross = null;
        booking.QuotedGross = 0m;
        Assert.Equal(2000m, BookingPricing.EquipmentGrossOf(booking, 1000m));
    }

    [Fact]
    public void Godown_price_is_tons_times_monthly_rate_times_months_rounded()
    {
        // 30 days = exactly one month
        Assert.Equal(3000m, BookingPricing.GodownGross(Monday, Monday.AddDays(30), 10, 300m));
        // 45 days = 1.5 months
        Assert.Equal(4500m, BookingPricing.GodownGross(Monday, Monday.AddDays(45), 10, 300m));
        // Billed per day (as a fraction of a 30-day month), with a one-day minimum
        Assert.Equal(300m, BookingPricing.GodownGross(Monday, Monday.AddDays(3), 10, 300m));
        Assert.Equal(100m, BookingPricing.GodownGross(Monday, Monday, 10, 300m));
    }

    [Fact]
    public void Commission_rounds_to_whole_taka()
    {
        Assert.Equal(225m, BookingPricing.Commission(4500m, 0.05m));
        Assert.Equal(62m, BookingPricing.Commission(1234m, 0.05m));
        Assert.Equal(0m, BookingPricing.Commission(new EquipmentBooking()));
    }

    private static EquipmentRateRule Rule(string kind, string name, decimal rate, DateTime? start = null, DateTime? end = null) => new()
    {
        Kind = kind,
        Name = name,
        DailyRate = rate,
        IsActive = true,
        StartDate = start,
        EndDate = end
    };
}
