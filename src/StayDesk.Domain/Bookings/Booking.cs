using System.Linq.Expressions;

namespace StayDesk.Domain.Bookings;

public class Booking
{
    // BOOK-3: an unpaid booking holds the room for this long.
    public static readonly TimeSpan PendingHold = TimeSpan.FromMinutes(15);

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public string Reference { get; private init; }
    public Guid RoomId { get; private init; }
    public Guid GuestId { get; private init; }
    public Guid CreatedById { get; private init; }
    public DateOnly CheckIn { get; private init; }
    public DateOnly CheckOut { get; private init; }
    public int Guests { get; private init; }
    public string GuestFullName { get; private set; }
    public string GuestEmail { get; private set; }
    public string? GuestPhone { get; private set; }
    public string? SpecialRequests { get; private set; }
    public BookingStatus Status { get; private set; }
    public decimal TotalPrice { get; private init; }
    public List<NightPrice> PriceBreakdown { get; private init; } = [];
    public decimal? CancellationFee { get; private set; }
    public decimal? RefundAmount { get; private set; }
    public bool FeeWaived { get; private set; }
    public string? InternalNotes { get; private set; }
    public Guid? IdempotencyKey { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset? CheckedInAt { get; private set; }
    public DateTimeOffset? CheckedOutAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    // Concurrency token (BOOK-4); mapped to PostgreSQL's xmin.
    public uint Version { get; private set; }

    // SRCH-3: a booking blocks its room while Confirmed or CheckedIn, or Pending and not yet
    // expired. An expression, so availability queries run it in SQL.
    public static Expression<Func<Booking, bool>> IsActiveAt(DateTimeOffset now) => b =>
        b.Status == BookingStatus.Confirmed
        || b.Status == BookingStatus.CheckedIn
        || (b.Status == BookingStatus.Pending && b.ExpiresAt > now);

    // SRCH-3: check-out is exclusive, so a stay may start on the day another ends.
    public static Expression<Func<Booking, bool>> Overlaps(DateOnly checkIn, DateOnly checkOut) => b =>
        b.CheckIn < checkOut && b.CheckOut > checkIn;

    // EF Core materialises through this; owned PriceBreakdown can't bind to a constructor.
    private Booking()
    {
        Reference = null!;
        GuestFullName = null!;
        GuestEmail = null!;
    }

    // Creates a guest booking awaiting payment.
    public Booking(
        string reference,
        Guid roomId,
        Guid guestId,
        DateOnly checkIn,
        DateOnly checkOut,
        int guests,
        string guestFullName,
        string guestEmail,
        string? guestPhone,
        string? specialRequests,
        List<NightPrice> priceBreakdown,
        Guid? idempotencyKey,
        DateTimeOffset createdAt)
    {
        Reference = reference;
        RoomId = roomId;
        GuestId = guestId;
        CreatedById = guestId;
        CheckIn = checkIn;
        CheckOut = checkOut;
        Guests = guests;
        GuestFullName = guestFullName;
        GuestEmail = guestEmail;
        GuestPhone = guestPhone;
        SpecialRequests = specialRequests;
        PriceBreakdown = priceBreakdown;
        TotalPrice = priceBreakdown.Sum(n => n.Price);
        IdempotencyKey = idempotencyKey;
        CreatedAt = createdAt;
        Status = BookingStatus.Pending;
        ExpiresAt = createdAt + PendingHold;
    }

    // BOOK-1: the number comes from a database sequence; the year is the creation year in UTC.
    public static string FormatReference(int year, long number) => $"SD-{year}-{number:D6}";

    // BOOK-3/4 transitions. `today` is the calendar date at the property (Property.TodayAt),
    // because check-in and no-show windows are local dates, not UTC instants. The SRS calls
    // these CheckIn/CheckOut, but those names are taken by the stay dates.

    // Payment. An expired hold can't be paid, even if no one has marked it Cancelled yet.
    public void Confirm(DateTimeOffset now)
    {
        EnsureStatus("confirmed", BookingStatus.Pending);
        if (now >= ExpiresAt)
        {
            throw new BookingExpiredException(Reference);
        }

        Status = BookingStatus.Confirmed;
        ConfirmedAt = now;
    }

    public void MarkCheckedIn(DateOnly today, DateTimeOffset now)
    {
        EnsureStatus("checked in", BookingStatus.Confirmed);
        if (today < CheckIn || today >= CheckOut)
        {
            throw new InvalidStatusTransitionException(
                $"Booking {Reference} can be checked in from {CheckIn:yyyy-MM-dd} until {CheckOut.AddDays(-1):yyyy-MM-dd}.");
        }

        Status = BookingStatus.CheckedIn;
        CheckedInAt = now;
    }

    // An early departure keeps the full stored price, so the price is not touched here.
    public void MarkCheckedOut(DateTimeOffset now)
    {
        EnsureStatus("checked out", BookingStatus.CheckedIn);
        Status = BookingStatus.CheckedOut;
        CheckedOutAt = now;
    }

    // The fee and refund (BOOK-5) are added in 4c, together with the cancellation quote.
    public void Cancel(DateTimeOffset now)
    {
        EnsureStatus("cancelled", BookingStatus.Pending, BookingStatus.Confirmed);
        Status = BookingStatus.Cancelled;
        CancelledAt = now;
    }

    // BOOK-5: a no-show is charged the first night of the stored snapshot.
    public void MarkNoShow(DateOnly today)
    {
        EnsureStatus("marked as no-show", BookingStatus.Confirmed);
        if (today <= CheckIn)
        {
            throw new InvalidStatusTransitionException(
                $"Booking {Reference} can be marked as no-show from {CheckIn.AddDays(1):yyyy-MM-dd}.");
        }

        Status = BookingStatus.NoShow;
        CancellationFee = PriceBreakdown[0].Price;
    }

    private void EnsureStatus(string action, params BookingStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidStatusTransitionException($"A {Status} booking can't be {action}.");
        }
    }
}
