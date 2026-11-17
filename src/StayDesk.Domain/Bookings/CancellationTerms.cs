namespace StayDesk.Domain.Bookings;

// BOOK-5: what cancelling now would cost. FreeUntil is the last moment a cancellation is free.
public sealed record CancellationTerms(decimal AmountPaid, decimal Fee, decimal Refund, DateTimeOffset FreeUntil);
