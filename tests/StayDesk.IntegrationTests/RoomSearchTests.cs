using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StayDesk.Application.Paging;
using StayDesk.Application.Rooms;
using StayDesk.Domain.Bookings;
using StayDesk.Domain.Pricing;
using StayDesk.Domain.Properties;
using StayDesk.Domain.Rooms;
using StayDesk.Infrastructure.Persistence;

namespace StayDesk.IntegrationTests;

// SRCH-1..5. Each test uses its own city, so seed data and other tests never show up in the results.
public class RoomSearchTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Two weeks out, so neither "today" at the property nor a last-minute rule interferes.
    private static DateOnly NextThursday()
    {
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);
        while (day.DayOfWeek != DayOfWeek.Thursday)
        {
            day = day.AddDays(1);
        }
        return day;
    }

    [Fact]
    public async Task Search_excludes_booked_rooms_but_includes_rooms_held_by_an_expired_pending_booking()
    {
        var checkIn = NextThursday();
        var (city, property) = await NewPropertyAsync();
        var free = new Room(property.Id, "1", RoomType.Double, 2, 100.00m);
        var held = new Room(property.Id, "2", RoomType.Double, 2, 100.00m);
        var expired = new Room(property.Id, "3", RoomType.Double, 2, 100.00m);
        var confirmed = new Room(property.Id, "4", RoomType.Double, 2, 100.00m);
        var bookedOnLaterDates = new Room(property.Id, "5", RoomType.Double, 2, 100.00m);
        await SaveAsync(async db =>
        {
            db.Rooms.AddRange(free, held, expired, confirmed, bookedOnLaterDates);
            var guestId = (await db.Users.SingleAsync(u => u.Email == "guest@example.com", Ct)).Id;
            var now = DateTimeOffset.UtcNow;
            db.Bookings.Add(NewBooking(held, guestId, checkIn.AddDays(1), checkIn.AddDays(4), now));
            db.Bookings.Add(NewBooking(expired, guestId, checkIn, checkIn.AddDays(2), now - TimeSpan.FromHours(1)));
            var confirmedBooking = NewBooking(confirmed, guestId, checkIn.AddDays(-2), checkIn.AddDays(1), now - TimeSpan.FromHours(1));
            db.Bookings.Add(confirmedBooking);
            // The status machine arrives in 4a; until then the test sets Confirmed directly.
            db.Entry(confirmedBooking).Property(b => b.Status).CurrentValue = BookingStatus.Confirmed;
            db.Bookings.Add(NewBooking(bookedOnLaterDates, guestId, checkIn.AddDays(3), checkIn.AddDays(5), now));
        });

        var result = await SearchAsync($"city={city}&checkIn={checkIn:yyyy-MM-dd}&checkOut={checkIn.AddDays(3):yyyy-MM-dd}&guests=2");

        Assert.Equal(["1", "3", "5"], result.Items.Select(r => r.RoomNumber).Order());
    }

    [Fact]
    public async Task Search_leaves_out_rooms_too_small_for_the_guests_and_deactivated_rooms()
    {
        var (city, property) = await NewPropertyAsync();
        var family = new Room(property.Id, "1", RoomType.Suite, 4, 180.00m);
        var single = new Room(property.Id, "2", RoomType.Single, 1, 70.00m);
        var closed = new Room(property.Id, "3", RoomType.Suite, 4, 180.00m);
        closed.Deactivate();
        await SaveAsync(db => { db.Rooms.AddRange(family, single, closed); return Task.CompletedTask; });
        var checkIn = NextThursday();

        var result = await SearchAsync($"city={city}&checkIn={checkIn:yyyy-MM-dd}&checkOut={checkIn.AddDays(1):yyyy-MM-dd}&guests=3");

        Assert.Equal([family.Id], result.Items.Select(r => r.RoomId));
    }

    [Fact]
    public async Task Max_price_filters_on_the_total_for_the_stay_with_price_rules_applied()
    {
        var (city, property) = await NewPropertyAsync();
        var standard = new Room(property.Id, "1", RoomType.Double, 2, 100.00m);
        var superior = new Room(property.Id, "2", RoomType.Double, 2, 150.00m);
        await SaveAsync(db =>
        {
            db.Rooms.AddRange(standard, superior);
            db.PriceRules.Add(new PriceRule(property.Id, null, "Weekend", PriceRuleType.Weekend, 15m));
            return Task.CompletedTask;
        });
        var checkIn = NextThursday();

        // Thursday 100 + Friday and Saturday 115 = 330.00; the superior room is 150 + 172.50 × 2 = 495.00.
        var result = await SearchAsync(
            $"city={city}&checkIn={checkIn:yyyy-MM-dd}&checkOut={checkIn.AddDays(3):yyyy-MM-dd}&guests=2&maxPrice=400");

        var room = Assert.Single(result.Items);
        Assert.Equal(standard.Id, room.RoomId);
        Assert.Equal(330.00m, room.Total);
    }

    [Fact]
    public async Task Amenity_filter_returns_rooms_that_have_all_selected_amenities()
    {
        var (city, property) = await NewPropertyAsync();
        var both = new Room(property.Id, "1", RoomType.Double, 2, 100.00m);
        var one = new Room(property.Id, "2", RoomType.Double, 2, 100.00m);
        Guid[] selected = [];
        await SaveAsync(async db =>
        {
            var amenities = await db.Amenities.OrderBy(a => a.Name).Take(2).ToListAsync(Ct);
            selected = [.. amenities.Select(a => a.Id)];
            both.Update("1", RoomType.Double, 2, 100.00m, [], amenities);
            one.Update("2", RoomType.Double, 2, 100.00m, [], amenities.Take(1));
            db.Rooms.AddRange(both, one);
        });
        var checkIn = NextThursday();

        var result = await SearchAsync($"city={city}&checkIn={checkIn:yyyy-MM-dd}&checkOut={checkIn.AddDays(1):yyyy-MM-dd}&guests=2"
            + string.Concat(selected.Select(id => $"&amenities={id}")));

        var room = Assert.Single(result.Items);
        Assert.Equal(both.Id, room.RoomId);
        Assert.Equal(2, room.Amenities.Count);
    }

    [Fact]
    public async Task Results_are_sorted_and_paged_on_the_server()
    {
        var (city, property) = await NewPropertyAsync();
        await SaveAsync(db =>
        {
            db.Rooms.AddRange(
                new Room(property.Id, "1", RoomType.Double, 2, 100.00m),
                new Room(property.Id, "2", RoomType.Apartment, 4, 120.00m),
                new Room(property.Id, "3", RoomType.Twin, 3, 140.00m));
            return Task.CompletedTask;
        });
        var checkIn = NextThursday();
        var query = $"city={city}&checkIn={checkIn:yyyy-MM-dd}&checkOut={checkIn.AddDays(1):yyyy-MM-dd}&guests=1";

        var firstPage = await SearchAsync(query + "&sort=price&dir=desc&page=1&pageSize=2");
        var secondPage = await SearchAsync(query + "&sort=price&dir=desc&page=2&pageSize=2");
        var byCapacity = await SearchAsync(query + "&sort=capacity&dir=desc");

        Assert.Equal([140.00m, 120.00m], firstPage.Items.Select(r => r.Total));
        Assert.Equal((3, 2), (firstPage.TotalCount, firstPage.TotalPages));
        Assert.Equal([100.00m], secondPage.Items.Select(r => r.Total));
        Assert.Equal([4, 3, 2], byCapacity.Items.Select(r => r.MaxGuests));
    }

    [Fact]
    public async Task Search_rejects_a_check_in_in_the_past_at_the_property()
    {
        var (city, _) = await NewPropertyAsync();
        var past = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2);

        var response = await api.Factory.CreateClient().GetAsync(
            $"/api/v1/rooms/search?city={city}&checkIn={past:yyyy-MM-dd}&checkOut={past.AddDays(3):yyyy-MM-dd}&guests=2", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("checkIn", out _));
    }

    [Fact]
    public async Task Availability_lists_the_booked_nights_inside_the_range_and_ignores_expired_holds()
    {
        var from = NextThursday();
        var (_, property) = await NewPropertyAsync();
        var room = new Room(property.Id, "1", RoomType.Double, 2, 100.00m);
        await SaveAsync(async db =>
        {
            db.Rooms.Add(room);
            var guestId = (await db.Users.SingleAsync(u => u.Email == "guest@example.com", Ct)).Id;
            var now = DateTimeOffset.UtcNow;
            // Starts before the range, so only its last night is inside it.
            db.Bookings.Add(NewBooking(room, guestId, from.AddDays(-2), from.AddDays(1), now));
            db.Bookings.Add(NewBooking(room, guestId, from.AddDays(3), from.AddDays(5), now));
            db.Bookings.Add(NewBooking(room, guestId, from.AddDays(6), from.AddDays(8), now - TimeSpan.FromHours(1)));
        });

        var response = await api.Factory.CreateClient().GetAsync(
            $"/api/v1/rooms/{room.Id}/availability?from={from:yyyy-MM-dd}&to={from.AddDays(10):yyyy-MM-dd}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var availability = (await response.Content.ReadFromJsonAsync<RoomAvailability>(ApiFixture.Json, Ct))!;
        Assert.Equal([from, from.AddDays(3), from.AddDays(4)], availability.BookedDates);
    }

    [Fact]
    public async Task Availability_allows_at_most_90_days()
    {
        var from = NextThursday();
        var (_, property) = await NewPropertyAsync();
        var room = new Room(property.Id, "1", RoomType.Double, 2, 100.00m);
        await SaveAsync(db => { db.Rooms.Add(room); return Task.CompletedTask; });
        var client = api.Factory.CreateClient();

        var ninety = await client.GetAsync($"/api/v1/rooms/{room.Id}/availability?from={from:yyyy-MM-dd}&to={from.AddDays(90):yyyy-MM-dd}", Ct);
        var ninetyOne = await client.GetAsync($"/api/v1/rooms/{room.Id}/availability?from={from:yyyy-MM-dd}&to={from.AddDays(91):yyyy-MM-dd}", Ct);

        Assert.Equal(HttpStatusCode.OK, ninety.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, ninetyOne.StatusCode);
    }

    [Fact]
    public async Task Availability_of_a_room_in_a_deactivated_property_returns_404()
    {
        var (_, property) = await NewPropertyAsync();
        var room = new Room(property.Id, "1", RoomType.Double, 2, 100.00m);
        await SaveAsync(async db =>
        {
            db.Rooms.Add(room);
            (await db.Properties.SingleAsync(p => p.Id == property.Id, Ct)).Deactivate();
        });
        var from = NextThursday();

        var response = await api.Factory.CreateClient().GetAsync(
            $"/api/v1/rooms/{room.Id}/availability?from={from:yyyy-MM-dd}&to={from.AddDays(60):yyyy-MM-dd}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Paged<RoomSearchResult>> SearchAsync(string query)
    {
        var response = await api.Factory.CreateClient().GetAsync($"/api/v1/rooms/search?{query}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Paged<RoomSearchResult>>(ApiFixture.Json, Ct))!;
    }

    private async Task<(string City, Property Property)> NewPropertyAsync()
    {
        var city = "Bamberg-" + Guid.NewGuid().ToString("N")[..8];
        var property = new Property("Gasthof Zur Linde", city, "Lange Straße 12, 96047 Bamberg", "Fachwerkhaus in der Altstadt.",
            null, new TimeOnly(15, 0), new TimeOnly(11, 0), "Europe/Berlin", DateTimeOffset.UtcNow);
        await SaveAsync(db => { db.Properties.Add(property); return Task.CompletedTask; });
        return (city, property);
    }

    private async Task SaveAsync(Func<StayDeskDbContext, Task> change)
    {
        await using var scope = api.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StayDeskDbContext>();
        await change(db);
        await db.SaveChangesAsync(Ct);
    }

    private static Booking NewBooking(Room room, Guid guestId, DateOnly checkIn, DateOnly checkOut, DateTimeOffset createdAt)
    {
        List<NightPrice> nights = [];
        for (var night = checkIn; night < checkOut; night = night.AddDays(1))
        {
            nights.Add(new NightPrice { Date = night, BasePrice = room.BasePrice, Price = room.BasePrice });
        }
        return new Booking($"SD-TEST-{Guid.NewGuid():N}"[..20], room.Id, guestId, checkIn, checkOut, 2,
            "Lena Schmidt", "guest@example.com", null, null, nights, null, createdAt);
    }
}
