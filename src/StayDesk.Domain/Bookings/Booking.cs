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
}
