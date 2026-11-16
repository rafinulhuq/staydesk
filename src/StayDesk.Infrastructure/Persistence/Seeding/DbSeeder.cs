using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StayDesk.Application.Auth;
using StayDesk.Domain.Pricing;
using StayDesk.Domain.Properties;
using StayDesk.Domain.Rooms;
using StayDesk.Infrastructure.Identity;

namespace StayDesk.Infrastructure.Persistence.Seeding;

// Idempotent: every step checks what already exists, so running it on each startup is safe.
// Bookings are not seeded yet. They need the pricing engine (3a) and the status machine (4a)
// to produce real price snapshots and statuses.
public class DbSeeder(
    StayDeskDbContext db,
    UserManager<User> users,
    RoleManager<IdentityRole<Guid>> roles,
    IOptions<SeedOptions> options,
    TimeProvider time,
    ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedRolesAsync();
        await SeedDemoUsersAsync();
        await SeedInventoryAsync(ct);
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                Check(await roles.CreateAsync(new IdentityRole<Guid>(role)), $"create role {role}");
            }
        }
    }

    private async Task SeedDemoUsersAsync()
    {
        var o = options.Value;
        await EnsureDemoUserAsync("admin@example.com", "Katrin", "Weber", Required(o.AdminPassword, nameof(o.AdminPassword)), Roles.Admin, Roles.Staff);
        await EnsureDemoUserAsync("staff@example.com", "Jonas", "Becker", Required(o.StaffPassword, nameof(o.StaffPassword)), Roles.Staff);
        await EnsureDemoUserAsync("guest@example.com", "Lena", "Schmidt", Required(o.GuestPassword, nameof(o.GuestPassword)), Roles.Guest);
    }

    private async Task EnsureDemoUserAsync(string email, string firstName, string lastName, string password, params string[] userRoles)
    {
        if (await users.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new User
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            CreatedAt = time.GetUtcNow(),
            IsDemo = true,
        };
        Check(await users.CreateAsync(user, password), $"create demo user {email}");
        Check(await users.AddToRolesAsync(user, userRoles), $"assign roles to {email}");
        logger.LogInformation("Seeded demo user {Email}", email);
    }

    private async Task SeedInventoryAsync(CancellationToken ct)
    {
        if (await db.Properties.IgnoreQueryFilters().AnyAsync(ct))
        {
            return;
        }

        var amenities = new Dictionary<string, Amenity>
        {
            ["wifi"] = new("Wi-Fi", "wifi"),
            ["breakfast"] = new("Breakfast", "free_breakfast"),
            ["ac"] = new("Air conditioning", "ac_unit"),
            ["balcony"] = new("Balcony", "balcony"),
            ["kitchen"] = new("Kitchenette", "kitchen"),
            ["bathtub"] = new("Bathtub", "bathtub"),
            ["desk"] = new("Work desk", "desk"),
            ["parking"] = new("Parking", "local_parking"),
            ["pets"] = new("Pets allowed", "pets"),
            ["view"] = new("City view", "location_city"),
        };
        db.Amenities.AddRange(amenities.Values);

        var now = time.GetUtcNow();
        var year = now.Year;

        var berlin = new Property(
            "Hotel Spreeblick", "Berlin", "Am Kupfergraben 4, 10117 Berlin",
            "Quiet rooms on the Spree, a five-minute walk from Museum Island.",
            null, new TimeOnly(15, 0), new TimeOnly(11, 0), "Europe/Berlin", now);
        var munich = new Property(
            "Isarhaus", "Munich", "Erhardtstraße 12, 80469 München",
            "Small hotel by the Isar, close to the Viktualienmarkt and the old town.",
            null, new TimeOnly(14, 0), new TimeOnly(11, 0), "Europe/Berlin", now);
        var hamburg = new Property(
            "Elbkai Apartments", "Hamburg", "Große Elbstraße 145, 22767 Hamburg",
            "Serviced apartments on the harbour front, next to the fish market.",
            null, new TimeOnly(16, 0), new TimeOnly(10, 0), "Europe/Berlin", now);
        db.Properties.AddRange(berlin, munich, hamburg);

        Room R(Property p, string number, RoomType type, int maxGuests, decimal price, params string[] amenityKeys)
        {
            var room = new Room(p.Id, number, type, maxGuests, price);
            foreach (var key in amenityKeys)
            {
                room.Amenities.Add(amenities[key]);
            }
            return room;
        }

        db.Rooms.AddRange(
            R(berlin, "101", RoomType.Single, 1, 89m, "wifi", "desk"),
            R(berlin, "102", RoomType.Single, 1, 89m, "wifi", "desk"),
            R(berlin, "103", RoomType.Double, 2, 129m, "wifi", "breakfast"),
            R(berlin, "104", RoomType.Twin, 2, 129m, "wifi", "breakfast"),
            R(berlin, "201", RoomType.Double, 2, 149m, "wifi", "breakfast", "view"),
            R(berlin, "202", RoomType.Double, 2, 149m, "wifi", "breakfast", "view", "bathtub"),
            R(berlin, "203", RoomType.Twin, 3, 159m, "wifi", "breakfast"),
            R(berlin, "301", RoomType.Suite, 4, 259m, "wifi", "breakfast", "view", "bathtub", "ac"),
            R(munich, "1", RoomType.Single, 1, 99m, "wifi"),
            R(munich, "2", RoomType.Double, 2, 145m, "wifi", "breakfast"),
            R(munich, "3", RoomType.Double, 2, 145m, "wifi", "breakfast", "balcony"),
            R(munich, "4", RoomType.Twin, 2, 139m, "wifi", "breakfast"),
            R(munich, "5", RoomType.Double, 3, 165m, "wifi", "breakfast", "balcony", "ac"),
            R(munich, "6", RoomType.Suite, 4, 289m, "wifi", "breakfast", "balcony", "bathtub", "ac", "parking"),
            R(hamburg, "A1", RoomType.Apartment, 2, 139m, "wifi", "kitchen", "pets"),
            R(hamburg, "A2", RoomType.Apartment, 2, 139m, "wifi", "kitchen", "pets"),
            R(hamburg, "A3", RoomType.Apartment, 4, 189m, "wifi", "kitchen", "balcony"),
            R(hamburg, "B1", RoomType.Apartment, 3, 169m, "wifi", "kitchen", "view"),
            R(hamburg, "B2", RoomType.Apartment, 4, 199m, "wifi", "kitchen", "view", "balcony"),
            R(hamburg, "B3", RoomType.Apartment, 6, 249m, "wifi", "kitchen", "view", "balcony", "parking"),
            R(hamburg, "C1", RoomType.Double, 2, 119m, "wifi", "desk"));

        db.PriceRules.AddRange(
            Rule(berlin, "Weekend", PriceRuleType.Weekend, 15m),
            Rule(berlin, "Christmas markets", PriceRuleType.Seasonal, 20m, new(year, 11, 24), new(year, 12, 23)),
            Rule(berlin, "Week-long stay", PriceRuleType.LengthOfStay, -10m, minNights: 7),
            Rule(munich, "Weekend", PriceRuleType.Weekend, 20m),
            Rule(munich, "Oktoberfest", PriceRuleType.Seasonal, 60m, new(year, 9, 19), new(year, 10, 4)),
            Rule(hamburg, "Weekend", PriceRuleType.Weekend, 10m),
            Rule(hamburg, "Last minute", PriceRuleType.LastMinute, -15m, daysBeforeCheckIn: 3),
            Rule(hamburg, "Long stay", PriceRuleType.LengthOfStay, -12m, minNights: 5));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded 3 properties, 21 rooms, 10 amenities and 8 price rules");
    }

    private static PriceRule Rule(
        Property property, string name, PriceRuleType type, decimal percent,
        DateOnly? start = null, DateOnly? end = null, int? minNights = null, int? daysBeforeCheckIn = null) =>
        new(property.Id, null, name, type, percent, start, end, minNights, daysBeforeCheckIn);

    private static string Required(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Seed:{name} must be set when Seed:DemoData is true.")
            : value;

    private static void Check(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Seeding failed to {action}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }
}
