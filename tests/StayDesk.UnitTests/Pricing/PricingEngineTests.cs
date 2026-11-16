using StayDesk.Domain.Pricing;
using StayDesk.Domain.Rooms;

namespace StayDesk.UnitTests.Pricing;

// PRICE-1, PRICE-2, PRICE-3, PRICE-6.
public class PricingEngineTests
{
    private static readonly Guid PropertyId = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 3, 1);

    private static Room RoomAt(decimal basePrice) => new(PropertyId, "101", RoomType.Double, 2, basePrice);

    private static Stay Stay(string checkIn, string checkOut, DateOnly? today = null) =>
        new(DateOnly.Parse(checkIn), DateOnly.Parse(checkOut), today ?? Today);

    private static PriceRule Rule(
        PriceRuleType type, decimal percent, string name = "Rule", Guid? roomId = null, Guid? propertyId = null,
        string? start = null, string? end = null, int? minNights = null, int? daysBeforeCheckIn = null) =>
        new(propertyId ?? PropertyId, roomId, name, type, percent,
            start is null ? null : DateOnly.Parse(start), end is null ? null : DateOnly.Parse(end),
            minNights, daysBeforeCheckIn);

    private static decimal[] NightPrices(PriceQuote quote) => [.. quote.Nights.Select(n => n.Price)];

    [Fact]
    public void Without_rules_every_night_costs_the_base_price()
    {
        var quote = PricingEngine.Quote(RoomAt(89m), [], Stay("2026-03-09", "2026-03-12"));

        Assert.Equal([89m, 89m, 89m], NightPrices(quote));
        Assert.Equal(267m, quote.Total);
        Assert.Equal(
            [new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 11)],
            quote.Nights.Select(n => n.Date));
    }

    [Fact]
    public void Seasonal_rule_includes_both_end_dates_and_a_stay_can_cross_its_edges()
    {
        var christmas = Rule(PriceRuleType.Seasonal, 20m, start: "2026-12-22", end: "2026-12-23");

        // Nights of 21, 22, 23 and 24 December: only 22 and 23 are in the season.
        var quote = PricingEngine.Quote(RoomAt(100m), [christmas], Stay("2026-12-21", "2026-12-25"));

        Assert.Equal([100m, 120m, 120m, 100m], NightPrices(quote));
    }

    [Fact]
    public void Weekend_rule_applies_to_friday_and_saturday_nights_only()
    {
        var weekend = Rule(PriceRuleType.Weekend, 15m);

        // Thursday 12 March to Monday 16 March: Thu, Fri, Sat, Sun nights.
        var quote = PricingEngine.Quote(RoomAt(100m), [weekend], Stay("2026-03-12", "2026-03-16"));

        Assert.Equal([100m, 115m, 115m, 100m], NightPrices(quote));
    }

    [Theory]
    [InlineData(6, 100)]
    [InlineData(7, 90)]
    public void Length_of_stay_rule_discounts_every_night_once_the_stay_is_long_enough(int nights, decimal expectedNightly)
    {
        var weekLong = Rule(PriceRuleType.LengthOfStay, -10m, minNights: 7);
        var checkIn = new DateOnly(2026, 4, 6);

        var quote = PricingEngine.Quote(RoomAt(100m), [weekLong], new Stay(checkIn, checkIn.AddDays(nights), Today));

        Assert.All(quote.Nights, n => Assert.Equal(expectedNightly, n.Price));
    }

    [Theory]
    [InlineData(0, 85)]
    [InlineData(3, 85)]
    [InlineData(4, 100)]
    public void Last_minute_rule_applies_when_check_in_is_at_most_n_days_after_today(int daysAhead, decimal expectedNightly)
    {
        var lastMinute = Rule(PriceRuleType.LastMinute, -15m, daysBeforeCheckIn: 3);
        var checkIn = Today.AddDays(daysAhead);

        var quote = PricingEngine.Quote(RoomAt(100m), [lastMinute], new Stay(checkIn, checkIn.AddDays(2), Today));

        Assert.All(quote.Nights, n => Assert.Equal(expectedNightly, n.Price));
    }

    [Fact]
    public void Matching_rules_multiply_instead_of_adding()
    {
        var rules = new[]
        {
            Rule(PriceRuleType.Weekend, 20m),
            Rule(PriceRuleType.LengthOfStay, -10m, minNights: 1),
        };

        // Friday night: 100 × 1.20 × 0.90 = 108.00, not 100 × (1 + 0.20 − 0.10) = 110.00.
        var quote = PricingEngine.Quote(RoomAt(100m), rules, Stay("2026-03-13", "2026-03-14"));

        Assert.Equal(108.00m, quote.Total);
    }

    [Fact]
    public void Rule_order_does_not_change_the_price()
    {
        var rules = new[]
        {
            Rule(PriceRuleType.Weekend, 17m),
            Rule(PriceRuleType.Seasonal, 33m, start: "2026-03-01", end: "2026-03-31"),
            Rule(PriceRuleType.LengthOfStay, -7m, minNights: 2),
        };
        var stay = Stay("2026-03-12", "2026-03-16");

        var forward = PricingEngine.Quote(RoomAt(97.45m), rules, stay);
        var reversed = PricingEngine.Quote(RoomAt(97.45m), [.. rules.Reverse()], stay);

        Assert.Equal(NightPrices(forward), NightPrices(reversed));
    }

    [Fact]
    public void Nightly_price_rounds_half_away_from_zero()
    {
        // 22.53 × 0.5 = 11.265: banker's rounding would give 11.26.
        var quote = PricingEngine.Quote(
            RoomAt(22.53m), [Rule(PriceRuleType.LengthOfStay, -50m, minNights: 1)], Stay("2026-03-09", "2026-03-10"));

        Assert.Equal(11.27m, quote.Total);
    }

    [Fact]
    public void Rounding_happens_once_per_night_after_all_rules()
    {
        var rules = new[]
        {
            Rule(PriceRuleType.Weekend, 10m),
            Rule(PriceRuleType.LengthOfStay, 10m, minNights: 1),
        };

        // 10.05 × 1.1 × 1.1 = 12.1605 → 12.16. Rounding after each rule would give 11.06 × 1.1 = 12.166 → 12.17.
        var quote = PricingEngine.Quote(RoomAt(10.05m), rules, Stay("2026-03-13", "2026-03-14"));

        Assert.Equal(12.16m, quote.Total);
    }

    [Fact]
    public void Total_is_the_sum_of_the_rounded_nights()
    {
        var plusTen = Rule(PriceRuleType.LengthOfStay, 10m, minNights: 1);

        // Each night 33.33 × 1.1 = 36.663 → 36.66; three nights 109.98. Rounding the
        // unrounded sum (109.989) would give 109.99 and disagree with the breakdown.
        var quote = PricingEngine.Quote(RoomAt(33.33m), [plusTen], Stay("2026-03-09", "2026-03-12"));

        Assert.Equal(109.98m, quote.Total);
        Assert.Equal(quote.Nights.Sum(n => n.Price), quote.Total);
    }

    [Fact]
    public void Nightly_price_never_drops_below_one_euro()
    {
        var quote = PricingEngine.Quote(
            RoomAt(5m), [Rule(PriceRuleType.LengthOfStay, -90m, minNights: 1)], Stay("2026-03-09", "2026-03-11"));

        Assert.Equal([1.00m, 1.00m], NightPrices(quote));
    }

    [Fact]
    public void Property_rules_and_this_rooms_rules_apply_but_not_other_rooms_or_properties()
    {
        var room = RoomAt(100m);
        var rules = new[]
        {
            Rule(PriceRuleType.LengthOfStay, 10m, name: "Property-wide", minNights: 1),
            Rule(PriceRuleType.LengthOfStay, 10m, name: "This room", roomId: room.Id, minNights: 1),
            Rule(PriceRuleType.LengthOfStay, 50m, name: "Other room", roomId: Guid.CreateVersion7(), minNights: 1),
            Rule(PriceRuleType.LengthOfStay, 50m, name: "Other property", propertyId: Guid.CreateVersion7(), minNights: 1),
        };

        var quote = PricingEngine.Quote(room, rules, Stay("2026-03-09", "2026-03-10"));

        Assert.Equal(121.00m, quote.Total);
        Assert.Equal(["Property-wide", "This room"], quote.Nights[0].Rules.Select(r => r.Name));
    }

    [Fact]
    public void Breakdown_names_each_applied_rule_with_its_type_and_percent()
    {
        var weekend = Rule(PriceRuleType.Weekend, 15m, name: "Weekend");

        var night = PricingEngine.Quote(RoomAt(129m), [weekend], Stay("2026-03-13", "2026-03-14")).Nights.Single();

        Assert.Equal(129m, night.BasePrice);
        Assert.Equal(148.35m, night.Price);
        var applied = Assert.Single(night.Rules);
        Assert.Equal(("Weekend", "Weekend", 15m), (applied.Name, applied.Type, applied.PercentChange));
    }

    [Fact]
    public void A_stay_without_nights_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PricingEngine.Quote(RoomAt(100m), [], Stay("2026-03-09", "2026-03-09")));
    }
}
