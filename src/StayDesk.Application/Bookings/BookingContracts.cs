using StayDesk.Application.Paging;
using StayDesk.Application.Pricing;
using StayDesk.Domain.Bookings;
using StayDesk.Domain.Rooms;

namespace StayDesk.Application.Bookings;

// BOOK-1. The guest details come pre-filled from the profile but can be changed per booking.
// ExpectedTotal is the total the guest was shown; PRICE-7 compares it with the server's price.
// GuestId or NewGuest: staff walk-ins only (BOOK-9), the guest the booking is for.
public sealed record CreateBookingRequest(
    Guid RoomId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Guests,
    string GuestFullName,
    string GuestEmail,
    string? GuestPhone,
    string? SpecialRequests,
    decimal? ExpectedTotal,
    Guid? GuestId = null,
    NewGuest? NewGuest = null) : IStayDates;

// BOOK-9: creates a Guest account without a password (no login until one is set).
public sealed record NewGuest(string FirstName, string LastName, string Email, string? Phone);

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
    bool FeeWaived,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? CheckedInAt,
    DateTimeOffset? CheckedOutAt,
    DateTimeOffset? CancelledAt);

public enum MyBookingsScope
{
    Upcoming,
    Past,
}

// One row of My bookings (BOOK-6) and of the back-office table and CSV export (BO-1, BO-3).
public sealed record BookingSummary(
    Guid Id,
    string Reference,
    Guid RoomId,
    string RoomNumber,
    RoomType RoomType,
    Guid PropertyId,
    string PropertyName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Guests,
    string GuestFullName,
    BookingStatus Status,
    decimal TotalPrice,
    DateTimeOffset? ExpiresAt);

public enum BookingListSort
{
    CheckIn,
    TotalPrice,
    GuestName,
}

// BO-1, bound from the query string. From/To pick bookings with at least one night in that
// range, both days included. Search matches the guest name or the reference, ignoring case.
// The CSV export (BO-3) takes the same filters and sort and ignores Page and PageSize.
public sealed class BookingListRequest
{
    public Guid? PropertyId { get; init; }
    public BookingStatus? Status { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public string? Search { get; init; }
    public BookingListSort Sort { get; init; } = BookingListSort.CheckIn;
    public SortDirection Dir { get; init; } = SortDirection.Asc;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

// BOOK-5: waiveFee is for Staff and Admin only.
public sealed record CancelBookingRequest(bool WaiveFee);

// BOOK-5 preview shown before the guest confirms the cancellation.
public sealed record CancellationQuote(decimal AmountPaid, decimal Fee, decimal Refund, DateTimeOffset FreeUntil);

// Created is false when an Idempotency-Key retry returned the original booking (BOOK-8).
public sealed record BookingCreation(BookingDetail Booking, bool Created);
