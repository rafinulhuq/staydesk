using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StayDesk.Application.Auth;
using StayDesk.Domain.Bookings;
using StayDesk.Domain.Pricing;
using StayDesk.Domain.Properties;
using StayDesk.Domain.Rooms;
using StayDesk.Infrastructure.Identity;
using StayDesk.Infrastructure.Persistence.Configurations;

namespace StayDesk.Infrastructure.Persistence.Seeding;

// Idempotent: every step checks what already exists, so running it on each startup is safe.
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
        await SeedDemoBookingsAsync(ct);
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

    // A null password creates an account that can't log in, like a walk-in guest (BOOK-9).
    private async Task<User> EnsureDemoUserAsync(string email, string firstName, string lastName, string? password, params string[] userRoles)
    {
        if (await users.FindByEmailAsync(email) is { } existing)
        {
            return existing;
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
        Check(password is null ? await users.CreateAsync(user) : await users.CreateAsync(user, password), $"create demo user {email}");
        Check(await users.AddToRolesAsync(user, userRoles), $"assign roles to {email}");
        logger.LogInformation("Seeded demo user {Email}", email);
        return user;
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

    // SRS §5: about 40 bookings across the last and next 30 days. Each one is priced by the
    // pricing engine as of its creation date and moved through the status machine, so the
    // demo data follows the same rules as real bookings.
    private async Task SeedDemoBookingsAsync(CancellationToken ct)
    {
        if (await db.Bookings.AnyAsync(ct))
        {
            return;
        }

        List<User> guests = [await users.FindByEmailAsync("guest@example.com") ?? throw new InvalidOperationException("Seed the demo guest first.")];
        foreach (var (first, last, email) in DemoGuests)
        {
            guests.Add(await EnsureDemoUserAsync(email, first, last, null, Roles.Guest));
        }

        var properties = await db.Properties.ToDictionaryAsync(p => p.Id, ct);
        // Sorted by name, not id: v7 ids created in the same millisecond have no fixed order,
        // and the same order is what makes the layout repeatable.
        var rooms = (await db.Rooms.Where(r => properties.Keys.Contains(r.PropertyId)).ToListAsync(ct))
            .OrderBy(r => properties[r.PropertyId].Name).ThenBy(r => r.RoomNumber).ToList();
        var rules = (await db.PriceRules.ToListAsync(ct)).ToLookup(r => r.PropertyId);
        var now = time.GetUtcNow();

        // A fixed seed gives the same layout on every reset, relative to the day it runs.
        var random = new Random(2026);
        var count = 0;
        for (var i = 0; i < rooms.Count; i++)
        {
            var room = rooms[i];
            var property = properties[room.PropertyId];
            var today = property.TodayAt(now);

            // Every third room has a guest in house or arriving today, the others a stay in the
            // past month; every room has one upcoming stay. The windows never overlap per room
            // (BOOK-2 exclusion constraint).
            List<(DateOnly CheckIn, DateOnly CheckOut)> stays = [];
            if (i % 3 == 0)
            {
                var arrival = today.AddDays(-random.Next(0, 3));
                stays.Add((arrival, today.AddDays(random.Next(1, 4))));
            }
            else
            {
                var arrival = today.AddDays(-random.Next(10, 31));
                stays.Add((arrival, arrival.AddDays(random.Next(1, 5))));
            }
            var upcoming = today.AddDays(random.Next(4, 27));
            stays.Add((upcoming, upcoming.AddDays(random.Next(1, 5))));

            foreach (var (checkIn, checkOut) in stays)
            {
                var guest = guests[random.Next(guests.Count)];
                var createdAt = Earlier(
                    LocalInstant(property, checkIn.AddDays(-random.Next(1, 22)), new TimeOnly(random.Next(8, 22), 0)),
                    now.AddHours(-random.Next(1, 48)));
                var quote = PricingEngine.Quote(room, rules[property.Id], new Stay(checkIn, checkOut, property.TodayAt(createdAt)));
                var number = await db.Database.SqlQueryRaw<long>(NextReferenceSql).SingleAsync(ct);

                var booking = new Booking(
                    Booking.FormatReference(createdAt.UtcDateTime.Year, number), room.Id, guest.Id, checkIn, checkOut,
                    random.Next(1, room.MaxGuests + 1), $"{guest.FirstName} {guest.LastName}",
                    guest.Email!, // every demo user is created with an email
                    null, null, [.. quote.Nights], null, createdAt);
                MoveToDemoStatus(booking, property, today, random.Next(10), random);
                db.Bookings.Add(booking);
                count++;
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} demo bookings", count);
    }

    // Past stays end CheckedOut (sometimes NoShow), stays in progress are CheckedIn, arrivals
    // today and later are Confirmed. One in ten is a hold that was never paid and expired.
    private static void MoveToDemoStatus(Booking booking, Property property, DateOnly today, int roll, Random random)
    {
        if (roll == 0)
        {
            booking.Cancel(booking.CreatedAt + Booking.PendingHold);
            return;
        }

        booking.Confirm(booking.CreatedAt + TimeSpan.FromMinutes(random.Next(1, 10)));
        if (booking.CheckIn >= today)
        {
            return;
        }

        if (booking.CheckOut < today && roll == 1)
        {
            booking.MarkNoShow(booking.CheckIn.AddDays(1));
            return;
        }

        booking.MarkCheckedIn(booking.CheckIn, LocalInstant(property, booking.CheckIn, property.CheckInTime).AddMinutes(random.Next(0, 300)));
        if (booking.CheckOut < today)
        {
            booking.MarkCheckedOut(LocalInstant(property, booking.CheckOut, property.CheckOutTime).AddMinutes(-random.Next(0, 120)));
        }
    }

    private const string NextReferenceSql = "SELECT nextval('" + BookingConfiguration.ReferenceSequence + "') AS \"Value\"";

    private static readonly (string First, string Last, string Email)[] DemoGuests =
    [
        ("Max", "Hoffmann", "max.hoffmann@example.com"),
        ("Sophie", "Wagner", "sophie.wagner@example.com"),
        ("Felix", "Krüger", "felix.krueger@example.com"),
        ("Anna", "Neumann", "anna.neumann@example.com"),
        ("Paul", "Zimmermann", "paul.zimmermann@example.com"),
        ("Marie", "Schulz", "marie.schulz@example.com"),
    ];

    private static DateTimeOffset LocalInstant(Property property, DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time);
        // Npgsql only writes UTC offsets to timestamptz.
        return new DateTimeOffset(local, TimeZoneInfo.FindSystemTimeZoneById(property.TimeZone).GetUtcOffset(local)).ToUniversalTime();
    }

    private static DateTimeOffset Earlier(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

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
