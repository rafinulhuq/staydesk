using StayDesk.Application.Pricing;
using StayDesk.Domain.Pricing;

namespace StayDesk.UnitTests.Pricing;

// PRICE-2: signed percent in −90..+300, and each rule type takes only its own fields.
public class PriceRuleRequestValidatorTests
{
    private static readonly PriceRuleRequestValidator Validator = new();
    private static readonly DateOnly Dec1 = new(2026, 12, 1);

    private static PriceRuleRequest Rule(
        PriceRuleType type, decimal percent = 10m, DateOnly? start = null, DateOnly? end = null,
        int? minNights = null, int? daysBeforeCheckIn = null) =>
        new(Guid.CreateVersion7(), null, "Rule", type, percent, start, end, minNights, daysBeforeCheckIn);

    private static string[] InvalidFields(PriceRuleRequest rule) =>
        [.. Validator.Validate(rule).Errors.Select(e => e.PropertyName).Distinct()];

    [Theory]
    [InlineData(-90)]
    [InlineData(300)]
    [InlineData(-12.5)]
    public void Percent_change_inside_the_range_is_accepted(decimal percent)
    {
        Assert.Empty(InvalidFields(Rule(PriceRuleType.Weekend, percent)));
    }

    [Theory]
    [InlineData(-90.01)]
    [InlineData(300.01)]
    [InlineData(10.005)]
    public void Percent_change_outside_the_range_or_with_more_than_two_decimals_is_rejected(decimal percent)
    {
        Assert.Equal(["PercentChange"], InvalidFields(Rule(PriceRuleType.Weekend, percent)));
    }

    [Fact]
    public void Seasonal_rule_needs_both_dates_in_order()
    {
        Assert.Equal(["StartDate", "EndDate"], InvalidFields(Rule(PriceRuleType.Seasonal)));
        Assert.Equal(["EndDate"], InvalidFields(Rule(PriceRuleType.Seasonal, start: Dec1, end: Dec1.AddDays(-1))));
        Assert.Empty(InvalidFields(Rule(PriceRuleType.Seasonal, start: Dec1, end: Dec1)));
    }

    [Fact]
    public void Length_of_stay_rule_needs_a_minimum_of_1_to_30_nights()
    {
        Assert.Equal(["MinNights"], InvalidFields(Rule(PriceRuleType.LengthOfStay)));
        Assert.Equal(["MinNights"], InvalidFields(Rule(PriceRuleType.LengthOfStay, minNights: 31)));
        Assert.Empty(InvalidFields(Rule(PriceRuleType.LengthOfStay, minNights: 7)));
    }

    [Fact]
    public void Last_minute_rule_needs_zero_or_more_days_before_check_in()
    {
        Assert.Equal(["DaysBeforeCheckIn"], InvalidFields(Rule(PriceRuleType.LastMinute, daysBeforeCheckIn: -1)));
        Assert.Empty(InvalidFields(Rule(PriceRuleType.LastMinute, daysBeforeCheckIn: 0)));
    }

    [Fact]
    public void Fields_of_another_rule_type_are_rejected()
    {
        var weekendWithExtras = Rule(PriceRuleType.Weekend, start: Dec1, end: Dec1, minNights: 2, daysBeforeCheckIn: 3);

        Assert.Equal(["StartDate", "EndDate", "MinNights", "DaysBeforeCheckIn"], InvalidFields(weekendWithExtras));
    }
}
