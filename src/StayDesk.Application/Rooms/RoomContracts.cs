using StayDesk.Domain.Rooms;

namespace StayDesk.Application.Rooms;

// The fields create and update share, so both are validated by one set of rules.
public interface IRoomFields
{
    string RoomNumber { get; }
    RoomType Type { get; }
    int MaxGuests { get; }
    decimal BasePrice { get; }
    IReadOnlyList<string> ImageUrls { get; }
    IReadOnlyList<Guid> AmenityIds { get; }
}

public sealed record CreateRoomRequest(
    Guid PropertyId,
    string RoomNumber,
    RoomType Type,
    int MaxGuests,
    decimal BasePrice,
    IReadOnlyList<string> ImageUrls,
    IReadOnlyList<Guid> AmenityIds) : IRoomFields;

// No PropertyId: a room keeps its property, so its bookings and history stay in one place.
// Version is the value the client last read; a stale one returns 409.
public sealed record UpdateRoomRequest(
    string RoomNumber,
    RoomType Type,
    int MaxGuests,
    decimal BasePrice,
    IReadOnlyList<string> ImageUrls,
    IReadOnlyList<Guid> AmenityIds,
    uint Version) : IRoomFields;

public sealed record AmenitySummary(Guid Id, string Name, string Icon);

public sealed record RoomDetail(
    Guid Id,
    Guid PropertyId,
    string RoomNumber,
    RoomType Type,
    int MaxGuests,
    decimal BasePrice,
    IReadOnlyList<string> ImageUrls,
    IReadOnlyList<AmenitySummary> Amenities,
    bool IsActive,
    uint Version);

// PROP-5: deactivation succeeds, but Admin sees which bookings still use the room.
public sealed record RoomDeactivation(IReadOnlyList<ActiveBookingWarning> ActiveBookings);

public sealed record ActiveBookingWarning(Guid BookingId, string Reference, DateOnly CheckIn, DateOnly CheckOut, string GuestFullName);

// Bound from the query string (from, to); to is exclusive, like check-out.
public sealed class RoomAvailabilityRequest
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
}

// SRCH-5: a date is booked when its night is taken.
public sealed record RoomAvailability(Guid RoomId, DateOnly From, DateOnly To, IReadOnlyList<DateOnly> BookedDates);
