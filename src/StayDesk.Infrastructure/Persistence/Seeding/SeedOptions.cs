namespace StayDesk.Infrastructure.Persistence.Seeding;

// Bound from the "Seed" configuration section (Seed__DemoData, Seed__AdminPassword, ...).
public class SeedOptions
{
    public const string Section = "Seed";

    public bool DemoData { get; set; }
    public string? AdminPassword { get; set; }
    public string? StaffPassword { get; set; }
    public string? GuestPassword { get; set; }
}
