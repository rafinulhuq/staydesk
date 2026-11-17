using StayDesk.Application.Paging;
using StayDesk.Application.Pricing;
using StayDesk.Domain.Rooms;

namespace StayDesk.Application.Rooms;

public enum RoomSearchSort
{
    Price,
    Capacity,
}

// Bound from the query string, so a class with settable properties rather than a positional record.
// Amenities is an array: MVC left an IReadOnlyList property initialised to [] unbound.
public sealed class RoomSearchRequest : IStayDates
{
    public string City { get; init; } = "";
    public DateOnly CheckIn { get; init; }
    public DateOnly CheckOut { get; init; }
    public int Guests { get; init; }
    public RoomType? Type { get; init; }

    // SRCH-4: the maximum total for the stay, not per night.
    public decimal? MaxPrice { get; init; }

    // SRCH-4: a room must have all of these.
    public Guid[] Amenities { get; init; } = [];
    public RoomSearchSort Sort { get; init; } = RoomSearchSort.Price;
    public SortDirection Dir { get; init; } = SortDirection.Asc;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record RoomSearchResult(
    Guid RoomId,
    Guid PropertyId,
    string PropertyName,
    string City,
    string RoomNumber,
    RoomType Type,
    int MaxGuests,
    string? ImageUrl,
    IReadOnlyList<AmenitySummary> Amenities,
    decimal Total);
