namespace StayDesk.Domain.Rooms;

public class Amenity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public string Name { get; private set; }
    public string Icon { get; private set; }

    public Amenity(string name, string icon)
    {
        Name = name;
        Icon = icon;
    }
}
