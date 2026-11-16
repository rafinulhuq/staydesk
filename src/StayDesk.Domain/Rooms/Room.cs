namespace StayDesk.Domain.Rooms;

public class Room
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid PropertyId { get; private set; }
    public string RoomNumber { get; private set; }
    public RoomType Type { get; private set; }
    public int MaxGuests { get; private set; }
    public decimal BasePrice { get; private set; }
    public List<string> ImageUrls { get; private set; } = [];
    public bool IsActive { get; private set; } = true;
    public ICollection<Amenity> Amenities { get; private set; } = [];

    // Concurrency token for admin edits; mapped to PostgreSQL's xmin.
    public uint Version { get; private set; }

    public Room(Guid propertyId, string roomNumber, RoomType type, int maxGuests, decimal basePrice)
    {
        PropertyId = propertyId;
        RoomNumber = roomNumber;
        Type = type;
        MaxGuests = maxGuests;
        BasePrice = basePrice;
    }
}
