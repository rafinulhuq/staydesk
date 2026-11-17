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

    public PriceRule(
        Guid propertyId,
        Guid? roomId,
        string name,
        PriceRuleType type,
        decimal percentChange,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        int? minNights = null,
        int? daysBeforeCheckIn = null)
    {
        PropertyId = propertyId;
        RoomId = roomId;
        Name = name;
        Type = type;
        PercentChange = percentChange;
        StartDate = startDate;
        EndDate = endDate;
        MinNights = minNights;
        DaysBeforeCheckIn = daysBeforeCheckIn;
    }

    public void Update(
        Guid propertyId,
        Guid? roomId,
        string name,
        PriceRuleType type,
        decimal percentChange,
        DateOnly? startDate,
        DateOnly? endDate,
        int? minNights,
        int? daysBeforeCheckIn)
    {
        PropertyId = propertyId;
        RoomId = roomId;
        Name = name;
        Type = type;
        PercentChange = percentChange;
        StartDate = startDate;
        EndDate = endDate;
        MinNights = minNights;
        DaysBeforeCheckIn = daysBeforeCheckIn;
    }

    // Soft delete: bookings keep the rule's name and percent in their price snapshot (PRICE-5).
    public void Deactivate() => IsActive = false;
}
