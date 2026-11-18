using System.Linq.Expressions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using StayDesk.Application.Errors;
using StayDesk.Application.Paging;
using StayDesk.Application.Persistence;
using StayDesk.Application.Pricing;
using StayDesk.Domain.Bookings;
using StayDesk.Domain.Payments;
using StayDesk.Domain.Properties;

namespace StayDesk.Application.Bookings;

public sealed class BookingService(
    IStayDeskDbContext db, PricingService pricing, IGuestAccounts accounts, IBookingEmails emails, TimeProvider time)
{
    // BOOK-1, BOOK-2, BOOK-8, PRICE-7. A guest books for themselves and gets a Pending hold;
    // staff book a walk-in (BOOK-9) for an existing or new guest, Confirmed and paid at the desk.
    public async Task<BookingCreation> CreateAsync(
        Guid callerId, bool isStaff, Guid? idempotencyKey, CreateBookingRequest request, CancellationToken ct)
    {
        if (idempotencyKey is not { } key || key == Guid.Empty)
        {
            throw Invalid("IdempotencyKey", "Send an Idempotency-Key header with a UUID.");
        }
        if (!isStaff && (request.GuestId is not null || request.NewGuest is not null))
        {
            throw Invalid(nameof(request.GuestId), "Only staff can book for another guest.");
        }
        if (isStaff && request.GuestId is null && request.NewGuest is null)
        {
            throw Invalid(nameof(request.GuestId), "Choose a guest or enter new guest details.");
        }

        // A retry of a booking that already went through needs no price check and no lock.
        if (await FindRetryAsync(callerId, key, request, ct) is { } earlier)
        {
            return new BookingCreation(earlier, Created: false);
        }

        if (request.GuestId is { } existingGuest && !await accounts.IsActiveGuestAsync(existingGuest, ct))
        {
            throw Invalid(nameof(request.GuestId), "No active guest with this id.");
        }
        if (request.NewGuest is { } newGuest && await accounts.FindIdByEmailAsync(newGuest.Email, ct) is not null)
        {
            throw Invalid("NewGuest.Email", "A guest with this email already exists. Choose them instead.");
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
        if (await FindRetryAsync(callerId, key, request, ct) is { } committed)
        {
            return new BookingCreation(committed, Created: false);
        }

        var now = time.GetUtcNow();

        // Expired holds no longer block the room (SRCH-3), but the exclusion constraint still
        // counts them as Pending, so they are expired here before the insert (lazy expiry).
        var expiredHolds = await db.Bookings
            .Where(b => b.RoomId == priced.Room.Id && b.Status == BookingStatus.Pending && b.ExpiresAt <= now)
            .Where(Booking.Overlaps(request.CheckIn, request.CheckOut))
            .ToListAsync(ct);
        foreach (var hold in expiredHolds)
        {
            hold.Expire(now);
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

        // Created inside the transaction, so a 409 leaves no orphan account behind.
        var guestId = request.NewGuest is { } walkInGuest
            ? await accounts.CreateWithoutPasswordAsync(walkInGuest, now, ct)
            : request.GuestId ?? callerId;

        var reference = Booking.FormatReference(now.UtcDateTime.Year, await db.NextBookingNumberAsync(ct));
        var (fullName, email, phone, specialRequests) =
            (request.GuestFullName.Trim(), request.GuestEmail.Trim(), Blank(request.GuestPhone), Blank(request.SpecialRequests));
        var booking = isStaff
            ? Booking.WalkIn(reference, priced.Room.Id, guestId, callerId, request.CheckIn, request.CheckOut, request.Guests,
                fullName, email, phone, specialRequests, [.. priced.Quote.Nights], key, now)
            : new Booking(reference, priced.Room.Id, guestId, request.CheckIn, request.CheckOut, request.Guests,
                fullName, email, phone, specialRequests, [.. priced.Quote.Nights], key, now);
        db.Bookings.Add(booking);
        if (isStaff)
        {
            db.Payments.Add(new Payment(booking.Id, booking.TotalPrice, PaymentProvider.Desk, now));
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var detail = await LoadDetailAsync(booking.Id, ct) ?? throw NotFound();
        if (isStaff)
        {
            await emails.SendConfirmationAsync(detail, ct);
        }
        return new BookingCreation(detail, Created: true);
    }

    // BOOK-6: the guest's own bookings. Upcoming = still holding the room and not over yet
    // (an expired hold counts as past, like a cancellation). "Today" is the UTC date: a list
    // split doesn't need each property's zone, and a stay ending today stays upcoming.
    public async Task<IReadOnlyList<BookingSummary>> ListMineAsync(Guid guestId, MyBookingsScope scope, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var upcoming = db.Bookings.Where(Booking.IsActiveAt(now)).Where(b => b.CheckOut >= today);
        var mine = (scope == MyBookingsScope.Upcoming
                ? upcoming
                : db.Bookings.Where(b => !upcoming.Any(u => u.Id == b.Id)))
            .Where(b => b.GuestId == guestId);
        var ordered = scope == MyBookingsScope.Upcoming
            ? mine.OrderBy(b => b.CheckIn)
            : mine.OrderByDescending(b => b.CheckIn);

        return await (
            from b in ordered.AsNoTracking()
            join r in db.Rooms.IgnoreQueryFilters() on b.RoomId equals r.Id
            join p in db.Properties.IgnoreQueryFilters() on r.PropertyId equals p.Id
            select new BookingSummary(b.Id, b.Reference, r.Id, r.RoomNumber, r.Type, p.Id, p.Name,
                b.CheckIn, b.CheckOut, b.Guests, b.GuestFullName, b.Status, b.TotalPrice, b.ExpiresAt))
            .ToListAsync(ct);
    }

    // BO-1. Staff see every booking, including those of deactivated rooms and properties.
    public async Task<Paged<BookingSummary>> ListAsync(BookingListRequest request, CancellationToken ct)
    {
        var bookings = BackOfficeQuery(request);
        var total = await bookings.CountAsync(ct);
        var page = await bookings
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);
        return new Paged<BookingSummary>(page, request.Page, request.PageSize, total);
    }

    public const int MaxExportRows = 10_000;

    // BO-3: the table's filters and order. One row past the limit is read to tell "exactly
    // 10,000" from "more", so a cut-off file is never handed out as if it were complete.
    public async Task<byte[]> ExportCsvAsync(BookingListRequest request, CancellationToken ct)
    {
        var bookings = await BackOfficeQuery(request).Take(MaxExportRows + 1).ToListAsync(ct);
        if (bookings.Count > MaxExportRows)
        {
            throw Invalid("", $"More than {MaxExportRows:N0} bookings match. Narrow the filters to export them.");
        }
        return BookingCsv.Write(bookings);
    }

    // Filters and sorts before projecting: EF can't translate a sort on a record built through
    // its constructor. Ties break on the unique reference, so paging is stable between requests.
    private IQueryable<BookingSummary> BackOfficeQuery(BookingListRequest request)
    {
        var rows =
            from b in db.Bookings.AsNoTracking()
            join r in db.Rooms.IgnoreQueryFilters() on b.RoomId equals r.Id
            join p in db.Properties.IgnoreQueryFilters() on r.PropertyId equals p.Id
            select new { Booking = b, Room = r, Property = p };

        if (request.PropertyId is { } propertyId)
        {
            rows = rows.Where(x => x.Property.Id == propertyId);
        }
        if (request.Status is { } status)
        {
            rows = rows.Where(x => x.Booking.Status == status);
        }
        if (request.From is { } from)
        {
            rows = rows.Where(x => x.Booking.CheckOut > from);
        }
        if (request.To is { } to)
        {
            rows = rows.Where(x => x.Booking.CheckIn <= to);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            rows = rows.Where(x => x.Booking.GuestFullName.ToLower().Contains(search) || x.Booking.Reference.ToLower().Contains(search));
        }

        var desc = request.Dir == SortDirection.Desc;
        var ordered = request.Sort switch
        {
            BookingListSort.TotalPrice => OrderBy(rows, x => x.Booking.TotalPrice, desc),
            BookingListSort.GuestName => OrderBy(rows, x => x.Booking.GuestFullName, desc),
            _ => OrderBy(rows, x => x.Booking.CheckIn, desc),
        };

        return ordered.ThenBy(x => x.Booking.Reference).Select(x => new BookingSummary(
            x.Booking.Id, x.Booking.Reference, x.Room.Id, x.Room.RoomNumber, x.Room.Type, x.Property.Id, x.Property.Name,
            x.Booking.CheckIn, x.Booking.CheckOut, x.Booking.Guests, x.Booking.GuestFullName, x.Booking.Status,
            x.Booking.TotalPrice, x.Booking.ExpiresAt));
    }

    private static IOrderedQueryable<T> OrderBy<T, TKey>(IQueryable<T> rows, Expression<Func<T, TKey>> key, bool desc) =>
        desc ? rows.OrderByDescending(key) : rows.OrderBy(key);

    // BOOK-3 mock payment by the guest who booked. Paying at or after ExpiresAt throws
    // BookingExpiredException (409). If the expiry job cancels the hold at the same moment, the
    // Booking concurrency token lets only one of them save; the other gets 409.
    public async Task<BookingDetail> PayAsync(Guid id, Guid guestId, CancellationToken ct)
    {
        var booking = await LoadForChangeAsync(id, guestId, canSeeAll: false, ct);
        var now = time.GetUtcNow();
        booking.Confirm(now);
        db.Payments.Add(new Payment(booking.Id, booking.TotalPrice, PaymentProvider.Mock, now));
        await db.SaveChangesAsync(ct);

        var detail = await LoadDetailAsync(id, ct) ?? throw NotFound();
        await emails.SendConfirmationAsync(detail, ct);
        return detail;
    }

    public async Task<CancellationQuote> GetCancellationQuoteAsync(Guid id, Guid userId, bool canSeeAll, CancellationToken ct)
    {
        var booking = await LoadForChangeAsync(id, userId, canSeeAll, ct);
        var property = await LoadPropertyAsync(booking, ct);
        var terms = booking.CancellationTermsAt(time.GetUtcNow(), property.InstantAt(booking.CheckIn, property.CheckInTime), waiveFee: false);
        return new CancellationQuote(terms.AmountPaid, terms.Fee, terms.Refund, terms.FreeUntil);
    }

    // BOOK-5. A refund marks the payment Refunded; the amount refunded is on the booking.
    public async Task<BookingDetail> CancelAsync(Guid id, Guid userId, bool isStaff, CancelBookingRequest request, CancellationToken ct)
    {
        if (request.WaiveFee && !isStaff)
        {
            throw Invalid(nameof(request.WaiveFee), "Only staff can waive the cancellation fee.");
        }

        var booking = await LoadForChangeAsync(id, userId, isStaff, ct);
        var property = await LoadPropertyAsync(booking, ct);
        booking.Cancel(time.GetUtcNow(), property.InstantAt(booking.CheckIn, property.CheckInTime), request.WaiveFee);
        if (booking.RefundAmount > 0 && await db.Payments.SingleOrDefaultAsync(p => p.BookingId == booking.Id, ct) is { } payment)
        {
            payment.MarkRefunded();
        }
        await db.SaveChangesAsync(ct);
        return await LoadDetailAsync(id, ct) ?? throw NotFound();
    }

    // BOOK-3 tidy-up, run by the expiry job every minute. Saves one hold at a time, so a hold
    // that was paid or cancelled after it was read (concurrency conflict) doesn't stop the rest.
    public async Task<int> ExpireHoldsAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var holds = await db.Bookings
            .Where(b => b.Status == BookingStatus.Pending && b.ExpiresAt <= now)
            .OrderBy(b => b.ExpiresAt)
            .Take(ExpiryBatchSize)
            .ToListAsync(ct);

        var expired = 0;
        foreach (var hold in holds)
        {
            hold.Expire(now);
            try
            {
                await db.SaveChangesAsync(ct);
                expired++;
            }
            catch (DbUpdateConcurrencyException)
            {
                // The other change won. Reloading drops this one, so the next save doesn't retry it.
                await db.Entry(hold).ReloadAsync(ct);
            }
        }
        return expired;
    }

    // Holds expire at most a few per minute in practice; the rest wait for the next run.
    private const int ExpiryBatchSize = 100;

    // BOOK-6: a guest asking for someone else's booking gets 404, so ids reveal nothing.
    public async Task<BookingDetail> GetAsync(Guid id, Guid userId, bool canSeeAll, CancellationToken ct)
    {
        var booking = await LoadDetailAsync(id, ct);
        return booking is not null && (canSeeAll || await IsGuestOfAsync(id, userId, ct)) ? booking : throw NotFound();
    }

    // BOOK-8: same key and same booking details returns the original; same key with different
    // details is a client bug, answered with 422. ExpectedTotal is not compared: it describes
    // what the guest saw, not what they booked. Keys are per creator, so for walk-ins per staff member.
    private async Task<BookingDetail?> FindRetryAsync(Guid callerId, Guid key, CreateBookingRequest request, CancellationToken ct)
    {
        var earlier = await db.Bookings.AsNoTracking()
            .Where(b => b.CreatedById == callerId && b.IdempotencyKey == key)
            .Select(b => new { b.Id, b.GuestId, b.RoomId, b.CheckIn, b.CheckOut, b.Guests, b.GuestFullName, b.GuestEmail, b.GuestPhone, b.SpecialRequests })
            .SingleOrDefaultAsync(ct);
        if (earlier is null)
        {
            return null;
        }

        // A new-guest walk-in's retry finds the account the first request created, by email.
        var guestId = request.GuestId
            ?? (request.NewGuest is { } newGuest ? await accounts.FindIdByEmailAsync(newGuest.Email, ct) : callerId);
        var same = earlier.GuestId == guestId
            && earlier.RoomId == request.RoomId
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

    // Tracked, for status changes. A guest can only change their own bookings; others get 404 (BOOK-6).
    private async Task<Booking> LoadForChangeAsync(Guid id, Guid userId, bool canSeeAll, CancellationToken ct) =>
        await db.Bookings.SingleOrDefaultAsync(b => b.Id == id && (canSeeAll || b.GuestId == userId), ct) ?? throw NotFound();

    private async Task<Property> LoadPropertyAsync(Booking booking, CancellationToken ct) =>
        await db.Properties.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(p => db.Rooms.IgnoreQueryFilters().Any(r => r.Id == booking.RoomId && r.PropertyId == p.Id), ct);

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
            booking.CancellationFee, booking.RefundAmount, booking.FeeWaived,
            booking.CreatedAt, booking.ExpiresAt, booking.ConfirmedAt, booking.CheckedInAt, booking.CheckedOutAt, booking.CancelledAt);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ValidationException Invalid(string field, string message) =>
        new([new ValidationFailure(field, message)]);

    private static ProblemException NotFound() =>
        new(ProblemKind.NotFound, "booking_not_found", "Booking not found.");
}
