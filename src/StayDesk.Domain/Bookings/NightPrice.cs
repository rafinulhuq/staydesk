namespace StayDesk.Domain.Bookings;

// One line of the price snapshot stored on a booking (PRICE-4, PRICE-5).
public class NightPrice
{
    public DateOnly Date { get; init; }
    public decimal BasePrice { get; init; }
    public List<AppliedRule> Rules { get; init; } = [];
    public decimal Price { get; init; }
}

public class AppliedRule
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public decimal PercentChange { get; init; }
}
