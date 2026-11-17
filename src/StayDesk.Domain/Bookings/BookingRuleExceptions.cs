namespace StayDesk.Domain.Bookings;

// BOOK-4: the API maps this to 400 invalid_status_transition.
public sealed class InvalidStatusTransitionException(string message) : Exception(message);

// BOOK-3: paying after the 15-minute hold. The API maps this to 409 booking_expired.
public sealed class BookingExpiredException(string reference)
    : Exception($"Booking {reference} expired before payment. Search again to book the room.");
