using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StayDesk.Application.Auth;
using StayDesk.Application.Bookings;
using StayDesk.Domain.Bookings;
using StayDesk.Domain.Properties;
using StayDesk.Domain.Rooms;
using StayDesk.Infrastructure.Persistence;

namespace StayDesk.IntegrationTests;

// BOOK-1, BOOK-2, BOOK-6, BOOK-8, PRICE-7 and NFR-5. Each test books its own new room.
public class BookingTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Two weeks out and Monday to Wednesday, so no last-minute or weekend rule applies.
    private static DateOnly NextMonday()
    {
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);
        while (day.DayOfWeek != DayOfWeek.Monday)
        {
            day = day.AddDays(1);
        }
        return day;
    }

    [Fact]
    public async Task New_booking_is_pending_for_15_minutes_with_the_server_price_and_a_location()
    {
        var room = await NewRoomAsync();
        var guest = await GuestAsync();
        var checkIn = NextMonday();

        var response = await PostAsync(guest, Request(room, checkIn, checkIn.AddDays(2), expectedTotal: 240.00m), Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = (await response.Content.ReadFromJsonAsync<BookingDetail>(ApiFixture.Json, Ct))!;
        Assert.Equal($"/api/v1/bookings/{booking.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Matches(@"^SD-\d{4}-\d{6}$", booking.Reference);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal(booking.CreatedAt + Booking.PendingHold, booking.ExpiresAt);
        Assert.Equal(240.00m, booking.TotalPrice);
        Assert.Equal([120.00m, 120.00m], booking.PriceBreakdown.Select(n => n.Price));
    }

    [Fact]
    public async Task Retry_with_the_same_key_and_body_returns_the_original_booking()
    {
        var room = await NewRoomAsync();
        var guest = await GuestAsync();
        var checkIn = NextMonday();
        var key = Guid.NewGuid();
        var request = Request(room, checkIn, checkIn.AddDays(1));

        var first = await PostAsync(guest, request, key);
        var retry = await PostAsync(guest, request, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(
            (await first.Content.ReadFromJsonAsync<BookingDetail>(ApiFixture.Json, Ct))!.Id,
            (await retry.Content.ReadFromJsonAsync<BookingDetail>(ApiFixture.Json, Ct))!.Id);
        Assert.Equal(1, await CountBookingsAsync(room));
    }

    [Fact]
    public async Task Reusing_a_key_for_a_different_booking_returns_422()
    {
        var room = await NewRoomAsync();
        var guest = await GuestAsync();
        var checkIn = NextMonday();
        var key = Guid.NewGuid();
        await PostAsync(guest, Request(room, checkIn, checkIn.AddDays(1)), key);

        var response = await PostAsync(guest, Request(room, checkIn.AddDays(3), checkIn.AddDays(4)), key);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("idempotency_key_reused", await CodeAsync(response));
        Assert.Equal(1, await CountBookingsAsync(room));
    }

    [Fact]
    public async Task Booking_without_an_idempotency_key_is_rejected()
    {
        var room = await NewRoomAsync();
        var checkIn = NextMonday();

        var response = await PostAsync(await GuestAsync(), Request(room, checkIn, checkIn.AddDays(1)), key: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", await CodeAsync(response));
    }

    [Fact]
    public async Task Booked_dates_return_409_room_unavailable()
    {
        var room = await NewRoomAsync();
        var guest = await GuestAsync();
        var checkIn = NextMonday();
        await PostAsync(guest, Request(room, checkIn, checkIn.AddDays(3)), Guid.NewGuid());

        var response = await PostAsync(guest, Request(room, checkIn.AddDays(2), checkIn.AddDays(4)), Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("room_unavailable", await CodeAsync(response));
    }

    [Fact]
    public async Task An_expired_hold_is_cancelled_and_the_room_booked_again()
    {
        var room = await NewRoomAsync();
        var checkIn = NextMonday();
        var expired = await SaveExpiredHoldAsync(room, checkIn, checkIn.AddDays(2));

        var response = await PostAsync(await GuestAsync(), Request(room, checkIn, checkIn.AddDays(2)), Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var db = api.Database.CreateContext();
        Assert.Equal(BookingStatus.Cancelled, (await db.Bookings.SingleAsync(b => b.Id == expired.Id, Ct)).Status);
    }

    [Fact]
    public async Task A_changed_price_returns_409_with_the_new_quote_and_books_nothing()
    {
        var room = await NewRoomAsync();
        var checkIn = NextMonday();

        var response = await PostAsync(await GuestAsync(), Request(room, checkIn, checkIn.AddDays(2), expectedTotal: 200.00m), Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("price_changed", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(240.00m, body.RootElement.GetProperty("quote").GetProperty("total").GetDecimal());
        Assert.Equal(0, await CountBookingsAsync(room));
    }

    [Fact]
    public async Task More_guests_than_the_room_sleeps_is_a_validation_error()
    {
        var room = await NewRoomAsync();
        var checkIn = NextMonday();

        var response = await PostAsync(await GuestAsync(), Request(room, checkIn, checkIn.AddDays(1), guests: 3), Guid.NewGuid());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("guests", out _));
    }

    [Fact]
    public async Task A_guest_gets_404_for_another_guests_booking_while_staff_can_read_it()
    {
        var room = await NewRoomAsync();
        var checkIn = NextMonday();
        var response = await PostAsync(await GuestAsync(), Request(room, checkIn, checkIn.AddDays(1)), Guid.NewGuid());
        var booking = (await response.Content.ReadFromJsonAsync<BookingDetail>(ApiFixture.Json, Ct))!;

        var otherGuest = await NewGuestAsync();
        var staff = await api.CreateClientAsync("staff@example.com", "Staff123");

        Assert.Equal(HttpStatusCode.NotFound, (await otherGuest.GetAsync($"/api/v1/bookings/{booking.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/api/v1/bookings/{booking.Id}", Ct)).StatusCode);
    }

    // NFR-5: the room row lock lets exactly one of 20 simultaneous requests through, and every
    // other one gets 409, never 500.
    [Fact]
    public async Task Twenty_parallel_bookings_of_the_same_room_and_dates_give_one_201_and_nineteen_409()
    {
        var room = await NewRoomAsync();
        var guest = await GuestAsync();
        var checkIn = NextMonday();
        var request = Request(room, checkIn, checkIn.AddDays(2));

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => PostAsync(guest, request, Guid.NewGuid())));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(19, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, await CountBookingsAsync(room));
    }

    private static CreateBookingRequest Request(Room room, DateOnly checkIn, DateOnly checkOut, int guests = 2, decimal? expectedTotal = null) =>
        new(room.Id, checkIn, checkOut, guests, "Lena Schmidt", "guest@example.com", "+49 30 1234567", null, expectedTotal);

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, CreateBookingRequest request, Guid? key)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/bookings") { Content = JsonContent.Create(request, options: ApiFixture.Json) };
        if (key is { } value)
        {
            message.Headers.Add("Idempotency-Key", value.ToString());
        }
        return await client.SendAsync(message, Ct);
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return body.RootElement.GetProperty("code").GetString();
    }

    private Task<HttpClient> GuestAsync() => api.CreateClientAsync("guest@example.com", "Guest123");

    private async Task<HttpClient> NewGuestAsync()
    {
        var email = $"jonas.vogel.{Guid.NewGuid():N}"[..20] + "@example.com";
        var register = await api.Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Jonas", "Vogel", email, "Reise2026"), Ct);
        register.EnsureSuccessStatusCode();
        return await api.CreateClientAsync(email, "Reise2026");
    }

    // A double room at 120.00 in its own property, with no price rules.
    private async Task<Room> NewRoomAsync()
    {
        var property = new Property("Pension am Dom", "Bamberg-" + Guid.NewGuid().ToString("N")[..8], "Domplatz 3, 96049 Bamberg",
            "Small guesthouse below the cathedral.", null, new TimeOnly(15, 0), new TimeOnly(11, 0), "Europe/Berlin", DateTimeOffset.UtcNow);
        var room = new Room(property.Id, "1", RoomType.Double, 2, 120.00m);
        await using var scope = api.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StayDeskDbContext>();
        db.AddRange(property, room);
        await db.SaveChangesAsync(Ct);
        return room;
    }

    private async Task<Booking> SaveExpiredHoldAsync(Room room, DateOnly checkIn, DateOnly checkOut)
    {
        await using var db = api.Database.CreateContext();
        var guestId = (await db.Users.SingleAsync(u => u.Email == "guest@example.com", Ct)).Id;
        List<NightPrice> nights = [];
        for (var night = checkIn; night < checkOut; night = night.AddDays(1))
        {
            nights.Add(new NightPrice { Date = night, BasePrice = room.BasePrice, Price = room.BasePrice });
        }
        var hold = new Booking($"SD-TEST-{Guid.NewGuid():N}"[..20], room.Id, guestId, checkIn, checkOut, 2,
            "Lena Schmidt", "guest@example.com", null, null, nights, null, DateTimeOffset.UtcNow - TimeSpan.FromHours(1));
        db.Bookings.Add(hold);
        await db.SaveChangesAsync(Ct);
        return hold;
    }

    private async Task<int> CountBookingsAsync(Room room)
    {
        await using var db = api.Database.CreateContext();
        return await db.Bookings.CountAsync(b => b.RoomId == room.Id, Ct);
    }
}
