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
