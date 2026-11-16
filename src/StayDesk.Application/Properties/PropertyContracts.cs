using StayDesk.Application.Rooms;

namespace StayDesk.Application.Properties;

// Used for both create and update: PROP-1 lets Admin edit every field.
public sealed record PropertyRequest(
    string Name,
    string City,
    string Address,
    string Description,
    string? ImageUrl,
    TimeOnly CheckInTime,
    TimeOnly CheckOutTime,
    string TimeZone);

public sealed record PropertySummary(Guid Id, string Name, string City, string Address, string? ImageUrl, bool IsActive);

public sealed record PropertyDetail(
    Guid Id,
    string Name,
    string City,
    string Address,
    string Description,
    string? ImageUrl,
    TimeOnly CheckInTime,
    TimeOnly CheckOutTime,
    string TimeZone,
    bool IsActive,
    IReadOnlyList<RoomDetail> Rooms);
