namespace StayDesk.Domain.Pricing;

// The nights being priced. Today is the date at the property (Property.TodayAt), so
// LastMinute counts days in the property's time zone, not the server's.
public sealed record Stay(DateOnly CheckIn, DateOnly CheckOut, DateOnly Today)
{
    public int Nights => CheckOut.DayNumber - CheckIn.DayNumber;
}

// PRICE-6: one strategy per rule type. Each answers only "does this rule apply to this
// night?"; the engine owns the arithmetic, so a new rule type can't get rounding wrong.
internal interface IPriceRuleStrategy
{
    PriceRuleType Type { get; }

    bool Matches(PriceRule rule, DateOnly night, Stay stay);
}

// Both ends inclusive (PRICE-2).
internal sealed class SeasonalStrategy : IPriceRuleStrategy
{
    public PriceRuleType Type => PriceRuleType.Seasonal;

    public bool Matches(PriceRule rule, DateOnly night, Stay stay) =>
        rule.StartDate <= night && night <= rule.EndDate;
}

// A night is named after its date: Friday night is the night from Friday to Saturday.
internal sealed class WeekendStrategy : IPriceRuleStrategy
{
    public PriceRuleType Type => PriceRuleType.Weekend;

    public bool Matches(PriceRule rule, DateOnly night, Stay stay) =>
        night.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;
}

// Applies to every night of a stay that is long enough, not only the nights past MinNights.
internal sealed class LengthOfStayStrategy : IPriceRuleStrategy
{
    public PriceRuleType Type => PriceRuleType.LengthOfStay;

    public bool Matches(PriceRule rule, DateOnly night, Stay stay) =>
        stay.Nights >= rule.MinNights;
}

internal sealed class LastMinuteStrategy : IPriceRuleStrategy
{
    public PriceRuleType Type => PriceRuleType.LastMinute;

    public bool Matches(PriceRule rule, DateOnly night, Stay stay) =>
        stay.CheckIn.DayNumber - stay.Today.DayNumber <= rule.DaysBeforeCheckIn;
}
