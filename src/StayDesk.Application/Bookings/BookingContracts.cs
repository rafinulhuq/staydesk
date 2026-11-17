using StayDesk.Application.Pricing;
using StayDesk.Domain.Bookings;

namespace StayDesk.Application.Bookings;

// BOOK-1. The guest details come pre-filled from the profile but can be changed per booking.
// ExpectedTotal is the total the guest was shown; PRICE-7 compares it with the server's price.
public sealed record CreateBookingRequest(
    Guid RoomId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Guests,
    string GuestFullName,
    string GuestEmail,
    string? GuestPhone,
    string? SpecialRequests,
    decimal? ExpectedTotal) : IStayDates;

public sealed record BookingDetail(
    Guid Id,
    string Reference,
    Guid RoomId,
    string RoomNumber,
    Guid PropertyId,
    string PropertyName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Guests,
    string GuestFullName,
    string GuestEmail,
    string? GuestPhone,
    string? SpecialRequests,
    BookingStatus Status,
    decimal TotalPrice,
    IReadOnlyList<QuoteNight> PriceBreakdown,
    decimal? CancellationFee,
    decimal? RefundAmount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? CheckedInAt,
    DateTimeOffset? CheckedOutAt,
    DateTimeOffset? CancelledAt);

// Created is false when an Idempotency-Key retry returned the original booking (BOOK-8).
public sealed record BookingCreation(BookingDetail Booking, bool Created);
