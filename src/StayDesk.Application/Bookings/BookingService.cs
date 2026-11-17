using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using StayDesk.Application.Errors;
using StayDesk.Application.Persistence;
using StayDesk.Application.Pricing;
using StayDesk.Domain.Bookings;

namespace StayDesk.Application.Bookings;

public sealed class BookingService(IStayDeskDbContext db, PricingService pricing, TimeProvider time)
{
    // BOOK-1, BOOK-2, BOOK-8, PRICE-7: a guest books for themselves. Staff walk-ins are 4d.
    public async Task<BookingCreation> CreateAsync(Guid guestId, Guid? idempotencyKey, CreateBookingRequest request, CancellationToken ct)
    {
        if (idempotencyKey is not { } key || key == Guid.Empty)
        {
            throw Invalid("IdempotencyKey", "Send an Idempotency-Key header with a UUID.");
        }

        // A retry of a booking that already went through needs no price check and no lock.
        if (await FindRetryAsync(guestId, key, request, ct) is { } earlier)
        {
            return new BookingCreation(earlier, Created: false);
        }

        var priced = await pricing.PriceStayAsync(request.RoomId, request, ct);
        if (request.Guests > priced.Room.MaxGuests)
        {
            throw Invalid(nameof(request.Guests), $"This room sleeps at most {priced.Room.MaxGuests}.");
        }
        if (request.ExpectedTotal is { } expected && expected != priced.Quote.Total)
        {
            throw new ProblemException(ProblemKind.Conflict, "price_changed",
                $"The price for this stay is now {priced.Quote.Total:0.00} EUR.",
                new Dictionary<string, object?> { ["quote"] = PricingService.ToQuote(priced.Room.Id, priced.Stay, priced.Quote) });
        }

        await using var transaction = await db.BeginTransactionAsync(ct);
        await db.LockRoomAsync(priced.Room.Id, ct);

        // A double click's second request waited on the lock above; the first one has committed now.
        if (await FindRetryAsync(guestId, key, request, ct) is { } committed)
        {
            return new BookingCreation(committed, Created: false);
        }

        var now = time.GetUtcNow();

        // Expired holds no longer block the room (SRCH-3), but the exclusion constraint still
        // counts them as Pending, so they are cancelled here before the insert (lazy expiry).
        var expiredHolds = await db.Bookings
            .Where(b => b.RoomId == priced.Room.Id && b.Status == BookingStatus.Pending && b.ExpiresAt <= now)
            .Where(Booking.Overlaps(request.CheckIn, request.CheckOut))
            .ToListAsync(ct);
        foreach (var hold in expiredHolds)
        {
            hold.Cancel(now);
        }

        var taken = await db.Bookings
            .Where(b => b.RoomId == priced.Room.Id)
            .Where(Booking.IsActiveAt(now))
            .AnyAsync(Booking.Overlaps(request.CheckIn, request.CheckOut), ct);
        if (taken)
        {
            throw new ProblemException(ProblemKind.Conflict, "room_unavailable",
                $"The room is no longer available from {request.CheckIn:yyyy-MM-dd} to {request.CheckOut:yyyy-MM-dd}.");
        }

        var number = await db.NextBookingNumberAsync(ct);
        var booking = new Booking(
            Booking.FormatReference(now.UtcDateTime.Year, number), priced.Room.Id, guestId,
            request.CheckIn, request.CheckOut, request.Guests,
            request.GuestFullName.Trim(), request.GuestEmail.Trim(), Blank(request.GuestPhone), Blank(request.SpecialRequests),
            [.. priced.Quote.Nights], key, now);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new BookingCreation(await LoadDetailAsync(booking.Id, ct) ?? throw NotFound(), Created: true);
    }

    // BOOK-6: a guest asking for someone else's booking gets 404, so ids reveal nothing.
    public async Task<BookingDetail> GetAsync(Guid id, Guid userId, bool canSeeAll, CancellationToken ct)
    {
        var booking = await LoadDetailAsync(id, ct);
        return booking is not null && (canSeeAll || await IsGuestOfAsync(id, userId, ct)) ? booking : throw NotFound();
    }

    // BOOK-8: same key and same booking details returns the original; same key with different
    // details is a client bug, answered with 422. ExpectedTotal is not compared: it describes
    // what the guest saw, not what they booked.
    private async Task<BookingDetail?> FindRetryAsync(Guid userId, Guid key, CreateBookingRequest request, CancellationToken ct)
    {
        var earlier = await db.Bookings.AsNoTracking()
            .Where(b => b.CreatedById == userId && b.IdempotencyKey == key)
            .Select(b => new { b.Id, b.RoomId, b.CheckIn, b.CheckOut, b.Guests, b.GuestFullName, b.GuestEmail, b.GuestPhone, b.SpecialRequests })
            .SingleOrDefaultAsync(ct);
        if (earlier is null)
        {
            return null;
        }

        var same = earlier.RoomId == request.RoomId
            && earlier.CheckIn == request.CheckIn
            && earlier.CheckOut == request.CheckOut
            && earlier.Guests == request.Guests
            && earlier.GuestFullName == request.GuestFullName.Trim()
            && earlier.GuestEmail == request.GuestEmail.Trim()
            && earlier.GuestPhone == Blank(request.GuestPhone)
            && earlier.SpecialRequests == Blank(request.SpecialRequests);
        if (!same)
        {
            throw new ProblemException(ProblemKind.Unprocessable, "idempotency_key_reused",
                "This Idempotency-Key was already used for a different booking.");
        }
        return await LoadDetailAsync(earlier.Id, ct);
    }

    private async Task<bool> IsGuestOfAsync(Guid bookingId, Guid userId, CancellationToken ct) =>
        await db.Bookings.AnyAsync(b => b.Id == bookingId && b.GuestId == userId, ct);

    // Room and property are read past the soft-delete filter, so history survives deactivation (§5).
    private async Task<BookingDetail?> LoadDetailAsync(Guid id, CancellationToken ct)
    {
        var row = await (
            from b in db.Bookings.AsNoTracking()
            join r in db.Rooms.IgnoreQueryFilters() on b.RoomId equals r.Id
            join p in db.Properties.IgnoreQueryFilters() on r.PropertyId equals p.Id
            where b.Id == id
            select new { Booking = b, r.RoomNumber, PropertyId = p.Id, PropertyName = p.Name })
            .SingleOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }

        var booking = row.Booking;
        return new BookingDetail(
            booking.Id, booking.Reference, booking.RoomId, row.RoomNumber, row.PropertyId, row.PropertyName,
            booking.CheckIn, booking.CheckOut, booking.Guests, booking.GuestFullName, booking.GuestEmail, booking.GuestPhone, booking.SpecialRequests,
            booking.Status, booking.TotalPrice,
            [.. booking.PriceBreakdown.Select(n => new QuoteNight(
                n.Date, n.BasePrice, [.. n.Rules.Select(r => new AppliedRuleSummary(r.Name, r.Type, r.PercentChange))], n.Price))],
            booking.CancellationFee, booking.RefundAmount,
            booking.CreatedAt, booking.ExpiresAt, booking.ConfirmedAt, booking.CheckedInAt, booking.CheckedOutAt, booking.CancelledAt);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ValidationException Invalid(string field, string message) =>
        new([new ValidationFailure(field, message)]);

    private static ProblemException NotFound() =>
        new(ProblemKind.NotFound, "booking_not_found", "Booking not found.");
}
