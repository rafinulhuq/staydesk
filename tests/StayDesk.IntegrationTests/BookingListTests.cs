using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StayDesk.Application.Bookings;
using StayDesk.Application.Paging;
using StayDesk.Domain.Bookings;
using StayDesk.Domain.Properties;
using StayDesk.Domain.Rooms;

namespace StayDesk.IntegrationTests;

// BO-1 and BO-3: the back-office bookings list and its CSV export. Each test adds its own
// property, so every filter is checked against a known set of bookings.
public class BookingListTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Staff_filter_by_property_status_and_stay_dates_and_page_through_the_rest()
    {
        var (property, roomA, roomB) = await NewPropertyAsync();
        var becker = await SaveAsync(roomA, "Anna Becker", new(2027, 3, 10), new(2027, 3, 12));
        var braun = await SaveAsync(roomB, "Felix Braun", new(2027, 3, 12), new(2027, 3, 15));
        var hoffmann = await SaveAsync(roomA, "Clara Hoffmann", new(2027, 3, 20), new(2027, 3, 21), pending: true);
        var staff = await api.CreateClientAsync("staff@example.com", "Staff123");
        var all = $"/api/v1/bookings?propertyId={property.Id}";

        Assert.Equal([becker, braun, hoffmann], await IdsAsync(staff, all));
        // Becker checks out on the 12th, so her last night is the 11th and she isn't in a range starting on the 12th.
        Assert.Equal([braun], await IdsAsync(staff, $"{all}&from=2027-03-12&to=2027-03-12"));
        Assert.Equal([becker], await IdsAsync(staff, $"{all}&to=2027-03-10"));
        Assert.Equal([hoffmann], await IdsAsync(staff, $"{all}&status=Pending"));
        Assert.Equal([braun, becker, hoffmann], await IdsAsync(staff, $"{all}&sort=TotalPrice&dir=Desc"));

        var secondPage = await staff.GetFromJsonAsync<Paged<BookingSummary>>($"{all}&pageSize=2&page=2", ApiFixture.Json, Ct);
        Assert.Equal([hoffmann], secondPage!.Items.Select(b => b.Id));
        Assert.Equal((3, 2), (secondPage.TotalCount, secondPage.TotalPages));
    }

    [Fact]
    public async Task Search_matches_the_guest_name_or_the_reference_ignoring_case()
    {
        var (property, room, _) = await NewPropertyAsync();
        var becker = await SaveAsync(room, "Anna Becker", new(2027, 4, 5), new(2027, 4, 7));
        var braun = await SaveAsync(room, "Felix Braun", new(2027, 4, 7), new(2027, 4, 9));
        var staff = await api.CreateClientAsync("staff@example.com", "Staff123");
        var all = $"/api/v1/bookings?propertyId={property.Id}";
        var reference = await ReferenceAsync(braun);

        Assert.Equal([becker], await IdsAsync(staff, $"{all}&search=BECK"));
        Assert.Equal([braun], await IdsAsync(staff, $"{all}&search={reference.ToLowerInvariant()}"));
    }

    [Fact]
    public async Task Export_is_utf8_csv_with_a_bom_and_only_the_filtered_bookings()
    {
        var (property, room, _) = await NewPropertyAsync();
        await SaveAsync(room, "Jürgen Müller", new(2027, 5, 3), new(2027, 5, 5));
        await SaveAsync(room, "Felix Braun", new(2027, 6, 1), new(2027, 6, 2));
        var staff = await api.CreateClientAsync("staff@example.com", "Staff123");

        var response = await staff.GetAsync($"/api/v1/bookings/export?propertyId={property.Id}&to=2027-05-31", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Reference,Guest,", lines[0]);
        Assert.Contains(",Jürgen Müller,Pension am Dom,1,2027-05-03,2027-05-05,2,Confirmed,200.00", lines[1]);
    }

    [Fact]
    public async Task A_guest_cannot_list_or_export_bookings()
    {
        var guest = await api.CreateClientAsync("guest@example.com", "Guest123");

        Assert.Equal(HttpStatusCode.Forbidden, (await guest.GetAsync("/api/v1/bookings", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.GetAsync("/api/v1/bookings/export", Ct)).StatusCode);
    }

    [Fact]
    public async Task A_date_range_ending_before_it_starts_is_a_validation_error()
    {
        var staff = await api.CreateClientAsync("staff@example.com", "Staff123");

        var response = await staff.GetAsync("/api/v1/bookings?from=2027-03-12&to=2027-03-11", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("to", out _));
    }

    private static async Task<List<Guid>> IdsAsync(HttpClient client, string url) =>
        [.. (await client.GetFromJsonAsync<Paged<BookingSummary>>(url, ApiFixture.Json, Ct))!.Items.Select(b => b.Id)];

    private async Task<(Property Property, Room A, Room B)> NewPropertyAsync()
    {
        var property = new Property("Pension am Dom", "Bamberg-" + Guid.NewGuid().ToString("N")[..8], "Domplatz 3, 96049 Bamberg",
            "Small guesthouse below the cathedral.", null, new TimeOnly(15, 0), new TimeOnly(11, 0), "Europe/Berlin", DateTimeOffset.UtcNow);
        var a = new Room(property.Id, "1", RoomType.Double, 2, 100.00m);
        var b = new Room(property.Id, "2", RoomType.Double, 2, 100.00m);
        await using var db = api.Database.CreateContext();
        db.AddRange(property, a, b);
        await db.SaveChangesAsync(Ct);
        return (property, a, b);
    }

    // 100.00 a night. Confirmed as a walk-in by the demo staff member, or a Pending hold.
    private async Task<Guid> SaveAsync(Room room, string guestName, DateOnly checkIn, DateOnly checkOut, bool pending = false)
    {
        await using var db = api.Database.CreateContext();
        var guestId = (await db.Users.SingleAsync(u => u.Email == "guest@example.com", Ct)).Id;
        var staffId = (await db.Users.SingleAsync(u => u.Email == "staff@example.com", Ct)).Id;
        List<NightPrice> nights = [];
        for (var night = checkIn; night < checkOut; night = night.AddDays(1))
        {
            nights.Add(new NightPrice { Date = night, BasePrice = room.BasePrice, Price = room.BasePrice });
        }
        var reference = $"SD-T{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var booking = pending
            ? new Booking(reference, room.Id, guestId, checkIn, checkOut, 2, guestName, "guest@example.com", null, null, nights, null, DateTimeOffset.UtcNow)
            : Booking.WalkIn(reference, room.Id, guestId, staffId, checkIn, checkOut, 2, guestName, "guest@example.com", null, null,
                nights, Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(Ct);
        return booking.Id;
    }

    private async Task<string> ReferenceAsync(Guid bookingId)
    {
        await using var db = api.Database.CreateContext();
        return (await db.Bookings.SingleAsync(b => b.Id == bookingId, Ct)).Reference;
    }
}
