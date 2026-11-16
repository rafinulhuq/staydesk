using Microsoft.EntityFrameworkCore;
using StayDesk.Application.Errors;
using StayDesk.Application.Persistence;
using StayDesk.Application.Rooms;
using StayDesk.Domain.Properties;

namespace StayDesk.Application.Properties;

public sealed class PropertyService(IStayDeskDbContext db, TimeProvider time)
{
    public async Task<IReadOnlyList<PropertySummary>> ListAsync(string? city, bool includeInactive, CancellationToken ct)
    {
        var properties = includeInactive ? db.Properties.IgnoreQueryFilters() : db.Properties;
        if (!string.IsNullOrWhiteSpace(city))
        {
            properties = properties.Where(p => p.City.ToLower() == city.Trim().ToLower());
        }

        return await properties.AsNoTracking()
            .OrderBy(p => p.City).ThenBy(p => p.Name)
            .Select(p => new PropertySummary(p.Id, p.Name, p.City, p.Address, p.ImageUrl, p.IsActive))
            .ToListAsync(ct);
    }

    public async Task<PropertyDetail> GetAsync(Guid id, bool includeInactive, CancellationToken ct)
    {
        var properties = includeInactive ? db.Properties.IgnoreQueryFilters() : db.Properties;
        var property = await properties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct)
            ?? throw NotFound();

        var rooms = includeInactive ? db.Rooms.IgnoreQueryFilters() : db.Rooms;
        var roomDetails = await rooms.AsNoTracking()
            .Where(r => r.PropertyId == id)
            .OrderBy(r => r.RoomNumber)
            .Select(RoomService.ToDetail)
            .ToListAsync(ct);

        return ToDetail(property, roomDetails);
    }

    public async Task<PropertyDetail> CreateAsync(PropertyRequest request, CancellationToken ct)
    {
        var property = new Property(
            request.Name, request.City, request.Address, request.Description, request.ImageUrl,
            request.CheckInTime, request.CheckOutTime, request.TimeZone, time.GetUtcNow());
        db.Properties.Add(property);
        await db.SaveChangesAsync(ct);
        return ToDetail(property, []);
    }

    public async Task<PropertyDetail> UpdateAsync(Guid id, PropertyRequest request, CancellationToken ct)
    {
        var property = await FindAsync(id, ct);
        property.Update(
            request.Name, request.City, request.Address, request.Description, request.ImageUrl,
            request.CheckInTime, request.CheckOutTime, request.TimeZone);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, includeInactive: true, ct);
    }

    // PROP-3: soft delete. Rooms keep their own IsActive; public queries hide them through
    // the property anyway, and reactivating the property brings them back unchanged.
    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        (await FindAsync(id, ct)).Deactivate();
        await db.SaveChangesAsync(ct);
    }

    public async Task ReactivateAsync(Guid id, CancellationToken ct)
    {
        (await FindAsync(id, ct)).Reactivate();
        await db.SaveChangesAsync(ct);
    }

    private async Task<Property> FindAsync(Guid id, CancellationToken ct) =>
        await db.Properties.IgnoreQueryFilters().SingleOrDefaultAsync(p => p.Id == id, ct)
        ?? throw NotFound();

    private static PropertyDetail ToDetail(Property p, IReadOnlyList<RoomDetail> rooms) => new(
        p.Id, p.Name, p.City, p.Address, p.Description, p.ImageUrl,
        p.CheckInTime, p.CheckOutTime, p.TimeZone, p.IsActive, rooms);

    private static ProblemException NotFound() =>
        new(ProblemKind.NotFound, "property_not_found", "Property not found.");
}
