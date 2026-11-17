using StayDesk.Domain.Pricing;

namespace StayDesk.Application.Pricing;

// Check-in and check-out as the guest picks them; shared by quote, preview and (later) search and booking.
public interface IStayDates
{
    DateOnly CheckIn { get; }
    DateOnly CheckOut { get; }
}

public sealed record QuoteRequest(Guid RoomId, DateOnly CheckIn, DateOnly CheckOut) : IStayDates;

// The rule as Admin enters it: saved by /price-rules, or only previewed by /pricing/preview.
public sealed record PriceRuleRequest(
    Guid PropertyId,
    Guid? RoomId,
    string Name,
    PriceRuleType Type,
    decimal PercentChange,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int? MinNights,
    int? DaysBeforeCheckIn);

public sealed record PreviewRequest(Guid RoomId, DateOnly CheckIn, DateOnly CheckOut, PriceRuleRequest Rule) : IStayDates;

// Type is a string, as in the booking's stored snapshot (PRICE-5).
public sealed record AppliedRuleSummary(string Name, string Type, decimal PercentChange);

public sealed record QuoteNight(DateOnly Date, decimal BasePrice, IReadOnlyList<AppliedRuleSummary> Rules, decimal Price);

// PRICE-4: per-night breakdown plus the total, which is always the sum of the nights.
public sealed record StayQuote(Guid RoomId, DateOnly CheckIn, DateOnly CheckOut, IReadOnlyList<QuoteNight> Nights, decimal Total);

// PRICE-8: the price today and the price if the draft rule were saved.
public sealed record PricePreview(StayQuote Current, StayQuote WithRule);

public sealed record PriceRuleDetail(
    Guid Id,
    Guid PropertyId,
    Guid? RoomId,
    string Name,
    PriceRuleType Type,
    decimal PercentChange,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int? MinNights,
    int? DaysBeforeCheckIn);
