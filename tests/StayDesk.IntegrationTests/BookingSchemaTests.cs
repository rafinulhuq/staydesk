using Microsoft.EntityFrameworkCore;
using Npgsql;
using StayDesk.Domain.Bookings;
using StayDesk.Domain.Properties;
using StayDesk.Domain.Rooms;
using StayDesk.Infrastructure.Identity;

namespace StayDesk.IntegrationTests;

// BOOK-2 database safety net and the price snapshot column (PRICE-5).
public class BookingSchemaTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Overlapping_active_bookings_for_one_room_are_rejected_with_exclusion_violation()
    {
        var (room, guest) = await SeedRoomAndGuest();
        await using var db = fixture.CreateContext();
        db.Bookings.Add(NewBooking(room, guest, new(2026, 12, 10), new(2026, 12, 13)));
        await db.SaveChangesAsync();

        db.Bookings.Add(NewBooking(room, guest, new(2026, 12, 12), new(2026, 12, 14)));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, pg.SqlState);
        Assert.Equal("ex_bookings_no_overlap", pg.ConstraintName);
    }

    [Fact]
    public async Task Back_to_back_bookings_are_allowed_because_check_out_is_exclusive()
    {
        var (room, guest) = await SeedRoomAndGuest();
        await using var db = fixture.CreateContext();
        db.Bookings.Add(NewBooking(room, guest, new(2026, 12, 10), new(2026, 12, 12)));
        db.Bookings.Add(NewBooking(room, guest, new(2026, 12, 12), new(2026, 12, 14)));

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Price_breakdown_round_trips_through_jsonb()
    {
        var (room, guest) = await SeedRoomAndGuest();
        var booking = NewBooking(room, guest, new(2026, 12, 18), new(2026, 12, 20));
        await using (var db = fixture.CreateContext())
        {
            db.Bookings.Add(booking);
            await db.SaveChangesAsync();
        }

        await using var read = fixture.CreateContext();
        var loaded = await read.Bookings.SingleAsync(b => b.Id == booking.Id);

        Assert.Equal(2, loaded.PriceBreakdown.Count);
        Assert.Equal(new DateOnly(2026, 12, 18), loaded.PriceBreakdown[0].Date);
        Assert.Equal("Weekend", loaded.PriceBreakdown[1].Rules.Single().Name);
        Assert.Equal(240.00m, loaded.TotalPrice);
    }

    private async Task<(Room Room, User Guest)> SeedRoomAndGuest()
    {
        await using var db = fixture.CreateContext();
        var property = new Property(
            "Hotel Spreeblick", "Berlin", "Am Kupfergraben 4, 10117 Berlin", "Near Museum Island.",
            null, new TimeOnly(15, 0), new TimeOnly(11, 0), "Europe/Berlin", Now);
        var room = new Room(property.Id, "101", RoomType.Double, 2, 100.00m);
        var email = $"guest-{Guid.NewGuid():N}@example.de";
        var guest = new User
        {
            FirstName = "Lena",
            LastName = "Schmidt",
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            CreatedAt = Now,
        };
        db.AddRange(property, room, guest);
        await db.SaveChangesAsync();
        return (room, guest);
    }

    private static Booking NewBooking(Room room, User guest, DateOnly checkIn, DateOnly checkOut)
    {
        List<NightPrice> nights =
        [
            new() { Date = checkIn, BasePrice = 100.00m, Price = 100.00m },
            new()
            {
                Date = checkIn.AddDays(1),
                BasePrice = 100.00m,
                Rules = [new AppliedRule { Name = "Weekend", Type = "Weekend", PercentChange = 40m }],
                Price = 140.00m,
            },
        ];

        return new Booking(
            $"SD-TEST-{Guid.NewGuid():N}"[..20], room.Id, guest.Id, checkIn, checkOut, 2,
            "Lena Schmidt", guest.Email!, null, null, nights, Guid.NewGuid(), Now);
    }
}
