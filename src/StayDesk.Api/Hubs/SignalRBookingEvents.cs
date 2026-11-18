using Microsoft.AspNetCore.SignalR;
using StayDesk.Application.Bookings;

namespace StayDesk.Api.Hubs;

// CAL-2: one message per change, to the property's calendar group and the dashboard group.
// The method name is the event type (BookingCreated, BookingUpdated, BookingCancelled).
public sealed class SignalRBookingEvents(IHubContext<BookingsHub> hub) : IBookingEvents
{
    public Task PublishAsync(BookingEvent bookingEvent, CancellationToken ct) =>
        hub.Clients
            .Groups(BookingsHub.PropertyGroup(bookingEvent.PropertyId), BookingsHub.DashboardGroup)
            .SendAsync(bookingEvent.Type.ToString(), new
            {
                bookingEvent.BookingId,
                bookingEvent.PropertyId,
                bookingEvent.RoomId,
                bookingEvent.CheckIn,
                bookingEvent.CheckOut,
                bookingEvent.Status,
            }, ct);
}
