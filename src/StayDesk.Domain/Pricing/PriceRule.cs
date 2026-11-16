namespace StayDesk.Domain.Pricing;

public class PriceRule
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid PropertyId { get; private set; }

    // Null means the rule applies to every room of the property (PRICE-3).
    public Guid? RoomId { get; private set; }
    public string Name { get; private set; }
    public PriceRuleType Type { get; private set; }

    // Signed: +20 is a surcharge, -10 a discount (PRICE-2).
    public decimal PercentChange { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public int? MinNights { get; private set; }
    public int? DaysBeforeCheckIn { get; private set; }
    public bool IsActive { get; private set; } = true;

    public PriceRule(Guid propertyId, Guid? roomId, string name, PriceRuleType type, decimal percentChange)
    {
        PropertyId = propertyId;
        RoomId = roomId;
        Name = name;
        Type = type;
        PercentChange = percentChange;
    }
}
