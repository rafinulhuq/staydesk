using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using StayDesk.Application.Paging;
using StayDesk.Application.Persistence;
using StayDesk.Application.Pricing;
using StayDesk.Domain.Bookings;
using StayDesk.Domain.Pricing;

namespace StayDesk.Application.Rooms;

public sealed class RoomSearchService(IStayDeskDbContext db, TimeProvider time)
{
    // SRCH-1..4. Availability, capacity, type and amenities are filtered in SQL. Totals depend
    // on price rules, so maxPrice, sorting and paging run in memory on the available rooms
    // (the known limit SRCH-4 accepts).
    public async Task<Paged<RoomSearchResult>> SearchAsync(RoomSearchRequest request, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var city = request.City.Trim().ToLower();

        // Inactive properties and rooms are hidden by the soft-delete query filters (PROP-3).
        var properties = await db.Properties.AsNoTracking()
            .Where(p => p.City.ToLower() == city)
            .ToDictionaryAsync(p => p.Id, ct);

        // SRCH-2: today at each property. Checked against every property in the city, not only
        // the available ones, so a past date fails the same way whether or not rooms are free.
        if (properties.Values.Any(p => request.CheckIn < p.TodayAt(now)))
        {
            throw new ValidationException([new ValidationFailure(nameof(IStayDates.CheckIn), "Check-in can't be in the past.")]);
        }

        var propertyIds = properties.Keys.ToList();
        var blocking = db.Bookings
            .Where(Booking.IsActiveAt(now))
            .Where(Booking.Overlaps(request.CheckIn, request.CheckOut));
        var rooms = db.Rooms.AsNoTracking()
            .Where(r => propertyIds.Contains(r.PropertyId)
                && r.MaxGuests >= request.Guests
                && !blocking.Any(b => b.RoomId == r.Id));
        if (request.Type is { } type)
        {
            rooms = rooms.Where(r => r.Type == type);
        }
        if (request.Amenities.Length > 0)
        {
            var amenityIds = request.Amenities.Distinct().ToList();
            rooms = rooms.Where(r => r.Amenities.Count(a => amenityIds.Contains(a.Id)) == amenityIds.Count);
        }

        var candidates = await rooms
            .Select(r => new { Room = r, Amenities = r.Amenities.OrderBy(a => a.Name).Select(a => new AmenitySummary(a.Id, a.Name, a.Icon)).ToList() })
            .ToListAsync(ct);

        var rules = (await db.PriceRules.AsNoTracking()
            .Where(r => propertyIds.Contains(r.PropertyId))
            .ToListAsync(ct)).ToLookup(r => r.PropertyId);

        var results = new List<RoomSearchResult>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var room = candidate.Room;
            var property = properties[room.PropertyId];
            var stay = new Stay(request.CheckIn, request.CheckOut, property.TodayAt(now));
            var total = PricingEngine.Quote(room, rules[property.Id], stay).Total;
            if (request.MaxPrice is { } maxPrice && total > maxPrice)
            {
                continue;
            }

            results.Add(new RoomSearchResult(room.Id, property.Id, property.Name, property.City, room.RoomNumber, room.Type,
                room.MaxGuests, room.ImageUrls.FirstOrDefault(), candidate.Amenities, total));
        }

        Func<RoomSearchResult, decimal> key = request.Sort == RoomSearchSort.Capacity ? r => r.MaxGuests : r => r.Total;
        var ordered = request.Dir == SortDirection.Desc ? results.OrderByDescending(key) : results.OrderBy(key);

        // Ties break on price, then id, so paging is stable between requests.
        var page = ordered.ThenBy(r => r.Total).ThenBy(r => r.RoomId)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new Paged<RoomSearchResult>(page, request.Page, request.PageSize, results.Count);
    }
}
