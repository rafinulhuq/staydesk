using System.Linq.Expressions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using StayDesk.Application.Errors;
using StayDesk.Application.Persistence;
using StayDesk.Domain.Bookings;
using StayDesk.Domain.Rooms;

namespace StayDesk.Application.Rooms;

public sealed class RoomService(IStayDeskDbContext db, TimeProvider time)
{
    // A projection, so EF selects only these columns and loads amenities in the same query.
    public static readonly Expression<Func<Room, RoomDetail>> ToDetail = r => new RoomDetail(
        r.Id, r.PropertyId, r.RoomNumber, r.Type, r.MaxGuests, r.BasePrice, r.ImageUrls,
        r.Amenities.OrderBy(a => a.Name).Select(a => new AmenitySummary(a.Id, a.Name, a.Icon)).ToList(),
        r.IsActive, r.Version);

    // Public callers only see a room while both it and its property are active (PROP-3).
    public async Task<RoomDetail> GetAsync(Guid id, bool includeInactive, CancellationToken ct)
    {
        var rooms = includeInactive
            ? db.Rooms.IgnoreQueryFilters()
            : db.Rooms.Where(r => db.Properties.Any(p => p.Id == r.PropertyId));

        return await rooms.AsNoTracking().Where(r => r.Id == id).Select(ToDetail).SingleOrDefaultAsync(ct)
            ?? throw NotFound();
    }

    // SRCH-5: the nights active bookings hold, for the room page's calendar. The endpoint is
    // public, so it returns dates only, never who booked.
    public async Task<RoomAvailability> GetAvailabilityAsync(Guid id, RoomAvailabilityRequest request, CancellationToken ct)
    {
        if (!await db.Rooms.AnyAsync(r => r.Id == id && db.Properties.Any(p => p.Id == r.PropertyId), ct))
        {
            throw NotFound();
        }

        var stays = await db.Bookings.AsNoTracking()
            .Where(b => b.RoomId == id)
            .Where(Booking.IsActiveAt(time.GetUtcNow()))
            .Where(Booking.Overlaps(request.From, request.To))
            .Select(b => new { b.CheckIn, b.CheckOut })
            .ToListAsync(ct);

        // Each booking's nights, clipped to the requested range; check-out day stays free.
        var bookedDates = stays
            .SelectMany(s =>
            {
                var first = s.CheckIn > request.From ? s.CheckIn : request.From;
                var end = s.CheckOut < request.To ? s.CheckOut : request.To;
                return Enumerable.Range(first.DayNumber, end.DayNumber - first.DayNumber).Select(DateOnly.FromDayNumber);
            })
            .Distinct()
            .Order()
            .ToList();
        return new RoomAvailability(id, request.From, request.To, bookedDates);
    }

    public async Task<IReadOnlyList<AmenitySummary>> ListAmenitiesAsync(CancellationToken ct) =>
        await db.Amenities.AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new AmenitySummary(a.Id, a.Name, a.Icon))
            .ToListAsync(ct);

    public async Task<RoomDetail> CreateAsync(CreateRoomRequest request, CancellationToken ct)
    {
        if (!await db.Properties.IgnoreQueryFilters().AnyAsync(p => p.Id == request.PropertyId, ct))
        {
            throw Invalid(nameof(request.PropertyId), "Property not found.");
        }
        await EnsureRoomNumberFreeAsync(request.PropertyId, request.RoomNumber, exceptRoomId: null, ct);

        var room = new Room(request.PropertyId, request.RoomNumber, request.Type, request.MaxGuests, request.BasePrice);
        room.Update(request.RoomNumber, request.Type, request.MaxGuests, request.BasePrice,
            request.ImageUrls, await LoadAmenitiesAsync(request.AmenityIds, ct));
        db.Rooms.Add(room);
        await db.SaveChangesAsync(ct);
        return await GetAsync(room.Id, includeInactive: true, ct);
    }

    public async Task<RoomDetail> UpdateAsync(Guid id, UpdateRoomRequest request, CancellationToken ct)
    {
        var room = await db.Rooms.IgnoreQueryFilters().Include(r => r.Amenities)
            .SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw NotFound();
        await EnsureRoomNumberFreeAsync(room.PropertyId, request.RoomNumber, exceptRoomId: id, ct);

        room.Update(request.RoomNumber, request.Type, request.MaxGuests, request.BasePrice,
            request.ImageUrls, await LoadAmenitiesAsync(request.AmenityIds, ct));

        // Compare against the version the admin edited, not the one just loaded, so two admins
        // editing the same room can't silently overwrite each other (409 concurrency_conflict).
        // Marking the row modified makes an amenities-only edit go through the same check.
        var entry = db.Entry(room);
        entry.Property(r => r.Version).OriginalValue = request.Version;
        entry.State = EntityState.Modified;
        await db.SaveChangesAsync(ct);

        return await GetAsync(id, includeInactive: true, ct);
    }

    // PROP-5: deactivation always succeeds; existing bookings stay valid, and Admin gets
    // the list of bookings that still use the room so they can be moved or honoured.
    public async Task<RoomDeactivation> DeactivateAsync(Guid id, CancellationToken ct)
    {
        var room = await db.Rooms.IgnoreQueryFilters().SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw NotFound();
        var property = await db.Properties.IgnoreQueryFilters().SingleAsync(p => p.Id == room.PropertyId, ct);
        var now = time.GetUtcNow();
        var today = property.TodayAt(now);

        var activeBookings = await db.Bookings.AsNoTracking()
            .Where(b => b.RoomId == id && b.CheckOut > today)
            .Where(Booking.IsActiveAt(now))
            .OrderBy(b => b.CheckIn)
            .Select(b => new ActiveBookingWarning(b.Id, b.Reference, b.CheckIn, b.CheckOut, b.GuestFullName))
            .ToListAsync(ct);

        room.Deactivate();
        await db.SaveChangesAsync(ct);
        return new RoomDeactivation(activeBookings);
    }

    public async Task ReactivateAsync(Guid id, CancellationToken ct)
    {
        var room = await db.Rooms.IgnoreQueryFilters().SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw NotFound();
        room.Reactivate();
        await db.SaveChangesAsync(ct);
    }

    // Checked here for a field-level message; the unique index still guards against races.
    private async Task EnsureRoomNumberFreeAsync(Guid propertyId, string roomNumber, Guid? exceptRoomId, CancellationToken ct)
    {
        var taken = await db.Rooms.IgnoreQueryFilters()
            .AnyAsync(r => r.PropertyId == propertyId && r.RoomNumber == roomNumber && r.Id != exceptRoomId, ct);
        if (taken)
        {
            throw Invalid(nameof(IRoomFields.RoomNumber), $"Room {roomNumber} already exists at this property.");
        }
    }

    private async Task<List<Amenity>> LoadAmenitiesAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        var amenities = await db.Amenities.Where(a => distinct.Contains(a.Id)).ToListAsync(ct);
        if (amenities.Count != distinct.Count)
        {
            throw Invalid(nameof(IRoomFields.AmenityIds), "Unknown amenity.");
        }
        return amenities;
    }

    private static ValidationException Invalid(string field, string message) =>
        new([new ValidationFailure(field, message)]);

    private static ProblemException NotFound() =>
        new(ProblemKind.NotFound, "room_not_found", "Room not found.");
}
