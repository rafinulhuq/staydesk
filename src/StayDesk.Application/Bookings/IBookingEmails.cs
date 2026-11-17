namespace StayDesk.Application.Bookings;

// BOOK-7. Implemented in Infrastructure over SMTP (Mailpit in development). Sending never
// throws: a failed email is logged and must not fail the booking.
public interface IBookingEmails
{
    Task SendConfirmationAsync(BookingDetail booking, CancellationToken ct);
}
