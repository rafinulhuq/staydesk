using System.Linq.Expressions;

namespace StayDesk.Domain.Bookings;

public class Booking
{
    // BOOK-3: an unpaid booking holds the room for this long.
    public static readonly TimeSpan PendingHold = TimeSpan.FromMinutes(15);

    // BOOK-5: cancelling is free until this long before the check-in instant.
    public static readonly TimeSpan FreeCancellationNotice = TimeSpan.FromHours(48);

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

    // BOOK-9: a walk-in is created by staff and paid at the desk, so it skips Pending and has
    // no hold to expire. CreatedById records the staff member.
    public static Booking WalkIn(
        string reference,
        Guid roomId,
        Guid guestId,
        Guid createdById,
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
        var booking = new Booking(reference, roomId, guestId, checkIn, checkOut, guests, guestFullName, guestEmail,
            guestPhone, specialRequests, priceBreakdown, idempotencyKey, createdAt)
        {
            CreatedById = createdById,
        };
        booking.Status = BookingStatus.Confirmed;
        booking.ConfirmedAt = createdAt;
        booking.ExpiresAt = null;
        return booking;
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

    // BOOK-5. A Pending booking is unpaid, so it costs nothing. A Confirmed one was paid in
    // full (online or at the desk); after the free window the first night is kept.
    public CancellationTerms CancellationTermsAt(DateTimeOffset now, DateTimeOffset checkInInstant, bool waiveFee)
    {
        EnsureStatus("cancelled", BookingStatus.Pending, BookingStatus.Confirmed);
        var freeUntil = checkInInstant - FreeCancellationNotice;
        var paid = Status == BookingStatus.Confirmed ? TotalPrice : 0m;
        var fee = paid > 0 && !waiveFee && now > freeUntil ? PriceBreakdown[0].Price : 0m;
        return new CancellationTerms(paid, fee, Math.Max(paid - fee, 0m), freeUntil);
    }

    // waiveFee is recorded even when the fee would have been 0, so the audit shows what staff chose.
    public void Cancel(DateTimeOffset now, DateTimeOffset checkInInstant, bool waiveFee)
    {
        var terms = CancellationTermsAt(now, checkInInstant, waiveFee);
        Status = BookingStatus.Cancelled;
        CancelledAt = now;
        CancellationFee = terms.Fee;
        RefundAmount = terms.Refund;
        FeeWaived = waiveFee;
    }

    // BOOK-3: an unpaid hold past ExpiresAt is cancelled, by the create transaction (lazily) or
    // by the expiry job. Nothing was paid, so fee and refund are 0.
    public void Expire(DateTimeOffset now)
    {
        EnsureStatus("expired", BookingStatus.Pending);
        if (now < ExpiresAt)
        {
            throw new InvalidStatusTransitionException($"Booking {Reference} is still held until {ExpiresAt:u}.");
        }

        Status = BookingStatus.Cancelled;
        CancelledAt = now;
        CancellationFee = 0m;
        RefundAmount = 0m;
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

    // BO-2: staff-only text, any status. Blank clears it; the length limit is validated in Application.
    public void UpdateInternalNotes(string? notes) =>
        InternalNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    private void EnsureStatus(string action, params BookingStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidStatusTransitionException($"A {Status} booking can't be {action}.");
        }
    }
}
