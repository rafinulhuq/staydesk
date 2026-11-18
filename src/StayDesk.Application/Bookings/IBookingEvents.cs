using StayDesk.Domain.Bookings;

namespace StayDesk.Application.Bookings;

// CAL-2: tells open calendars and dashboards that a booking changed. Implemented over
// SignalR in the API, so Application raises the events without depending on SignalR.
public interface IBookingEvents
{
    Task PublishAsync(BookingEvent bookingEvent, CancellationToken ct);
}

public enum BookingEventType
{
    BookingCreated,
    BookingUpdated,
    BookingCancelled,
}

// SRS §6 hub payload. Enough for a client to refetch only the affected room and dates.
public sealed record BookingEvent(
    BookingEventType Type,
    Guid BookingId,
    Guid PropertyId,
    Guid RoomId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    BookingStatus Status);
