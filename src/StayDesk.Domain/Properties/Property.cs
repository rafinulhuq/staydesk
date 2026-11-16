namespace StayDesk.Domain.Properties;

public class Property
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public string Name { get; private set; }
    public string City { get; private set; }
    public string Address { get; private set; }
    public string Description { get; private set; }
    public string? ImageUrl { get; private set; }
    public TimeOnly CheckInTime { get; private set; }
    public TimeOnly CheckOutTime { get; private set; }

    // IANA zone id. "Today" and the check-in instant are computed in this zone (SRS §2).
    public string TimeZone { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private init; }

    public Property(
        string name,
        string city,
        string address,
        string description,
        string? imageUrl,
        TimeOnly checkInTime,
        TimeOnly checkOutTime,
        string timeZone,
        DateTimeOffset createdAt)
    {
        Name = name;
        City = city;
        Address = address;
        Description = description;
        ImageUrl = imageUrl;
        CheckInTime = checkInTime;
        CheckOutTime = checkOutTime;
        TimeZone = timeZone;
        CreatedAt = createdAt;
    }

    public void Update(
        string name,
        string city,
        string address,
        string description,
        string? imageUrl,
        TimeOnly checkInTime,
        TimeOnly checkOutTime,
        string timeZone)
    {
        Name = name;
        City = city;
        Address = address;
        Description = description;
        ImageUrl = imageUrl;
        CheckInTime = checkInTime;
        CheckOutTime = checkOutTime;
        TimeZone = timeZone;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    // The calendar date at the property, which is what "today" means for bookings there.
    public DateOnly TodayAt(DateTimeOffset now) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(TimeZone)).DateTime);
}
