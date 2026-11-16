using StayDesk.Domain.Bookings;
using StayDesk.Domain.Rooms;

namespace StayDesk.Domain.Pricing;

// The breakdown is the same NightPrice list a booking stores as its snapshot (PRICE-5), so
// the quote a guest sees and the price saved on the booking can't drift apart.
public sealed record PriceQuote(IReadOnlyList<NightPrice> Nights, decimal Total);

// PRICE-1/3/6: pure, no clock, no database. Callers pass the rules they loaded and today's
// date at the property.
public static class PricingEngine
{
    public const decimal MinimumNightlyPrice = 1.00m;

    private static readonly Dictionary<PriceRuleType, IPriceRuleStrategy> Strategies =
        new IPriceRuleStrategy[]
        {
            new SeasonalStrategy(),
            new WeekendStrategy(),
            new LengthOfStayStrategy(),
            new LastMinuteStrategy(),
        }.ToDictionary(s => s.Type);

    public static PriceQuote Quote(Room room, IEnumerable<PriceRule> rules, Stay stay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(stay.Nights, 1, nameof(stay));

        // PRICE-3: property-wide rules and this room's rules both apply. Inactive rules are
        // already gone: callers load rules through the soft-delete query filter.
        var roomRules = rules
            .Where(r => r.PropertyId == room.PropertyId && (r.RoomId is null || r.RoomId == room.Id))
            .ToList();

        var nights = new List<NightPrice>(stay.Nights);
        for (var night = stay.CheckIn; night < stay.CheckOut; night = night.AddDays(1))
        {
            var applied = roomRules.Where(r => Strategies[r.Type].Matches(r, night, stay)).ToList();
            nights.Add(PriceNight(room.BasePrice, night, applied));
        }

        // The total is the sum of the rounded nights, so it always equals the breakdown.
        return new PriceQuote(nights, nights.Sum(n => n.Price));
    }

    // Multiplicative stacking is order-independent, so rules need no priority (D4).
    // Rounding happens once per night, after all rules, never between them.
    private static NightPrice PriceNight(decimal basePrice, DateOnly night, List<PriceRule> applied)
    {
        var price = applied.Aggregate(basePrice, (current, rule) => current * (1 + rule.PercentChange / 100));
        var rounded = Math.Round(price, 2, MidpointRounding.AwayFromZero);

        return new NightPrice
        {
            Date = night,
            BasePrice = basePrice,
            Rules = [.. applied.Select(r => new AppliedRule { Name = r.Name, Type = r.Type.ToString(), PercentChange = r.PercentChange })],
            Price = Math.Max(rounded, MinimumNightlyPrice),
        };
    }
}
