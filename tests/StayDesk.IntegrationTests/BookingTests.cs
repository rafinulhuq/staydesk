using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StayDesk.Application.Auth;
using StayDesk.Application.Bookings;
using StayDesk.Domain.Bookings;
using StayDesk.Domain.Payments;
using StayDesk.Domain.Properties;
using StayDesk.Domain.Rooms;
using StayDesk.Infrastructure.Persistence;

namespace StayDesk.IntegrationTests;

// BOOK-1..3, BOOK-5, BOOK-6, BOOK-8, BOOK-9, PRICE-7 and NFR-5. Each test books its own new room.
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

    [Fact]
    public async Task Paying_confirms_the_booking_and_records_a_mock_payment()
    {
        var guest = await GuestAsync();
        var booking = await BookAsync(guest);

        var response = await guest.PostAsync($"/api/v1/bookings/{booking.Id}/pay", null, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paid = (await response.Content.ReadFromJsonAsync<BookingDetail>(ApiFixture.Json, Ct))!;
        Assert.Equal(BookingStatus.Confirmed, paid.Status);
        Assert.NotNull(paid.ConfirmedAt);
        await using var db = api.Database.CreateContext();
        var payment = await db.Payments.SingleAsync(p => p.BookingId == booking.Id, Ct);
        Assert.Equal((240.00m, PaymentProvider.Mock, PaymentStatus.Succeeded), (payment.Amount, payment.Provider, payment.Status));
    }

    [Fact]
    public async Task Paying_after_the_hold_expired_returns_409_booking_expired()
    {
        var room = await NewRoomAsync();
        var checkIn = NextMonday();
        var hold = await SaveExpiredHoldAsync(room, checkIn, checkIn.AddDays(2));

        var response = await (await GuestAsync()).PostAsync($"/api/v1/bookings/{hold.Id}/pay", null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("booking_expired", await CodeAsync(response));
    }

    [Fact]
    public async Task Only_the_guest_who_booked_can_pay()
    {
        var booking = await BookAsync(await GuestAsync());

        var response = await (await NewGuestAsync()).PostAsync($"/api/v1/bookings/{booking.Id}/pay", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cancelling_a_paid_booking_two_weeks_ahead_is_free_and_refunds_the_payment()
    {
        var guest = await GuestAsync();
        var booking = await BookAsync(guest);
        await guest.PostAsync($"/api/v1/bookings/{booking.Id}/pay", null, Ct);

        var quote = await guest.GetFromJsonAsync<CancellationQuote>($"/api/v1/bookings/{booking.Id}/cancellation-quote", ApiFixture.Json, Ct);
        var response = await guest.PostAsJsonAsync($"/api/v1/bookings/{booking.Id}/cancel", new CancelBookingRequest(false), ApiFixture.Json, Ct);

        Assert.Equal((240.00m, 0m, 240.00m), (quote!.AmountPaid, quote.Fee, quote.Refund));
        var cancelled = (await response.Content.ReadFromJsonAsync<BookingDetail>(ApiFixture.Json, Ct))!;
        Assert.Equal((BookingStatus.Cancelled, 0m, 240.00m), (cancelled.Status, cancelled.CancellationFee, cancelled.RefundAmount));
        await using var db = api.Database.CreateContext();
        Assert.Equal(PaymentStatus.Refunded, (await db.Payments.SingleAsync(p => p.BookingId == booking.Id, Ct)).Status);
    }

    [Fact]
    public async Task A_guest_cannot_waive_the_fee_but_staff_can()
    {
        var booking = await BookAsync(await GuestAsync());
        var staff = await api.CreateClientAsync("staff@example.com", "Staff123");

        var asGuest = await (await GuestAsync()).PostAsJsonAsync($"/api/v1/bookings/{booking.Id}/cancel", new CancelBookingRequest(true), ApiFixture.Json, Ct);
        var asStaff = await staff.PostAsJsonAsync($"/api/v1/bookings/{booking.Id}/cancel", new CancelBookingRequest(true), ApiFixture.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, asGuest.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asStaff.StatusCode);
        Assert.True((await asStaff.Content.ReadFromJsonAsync<BookingDetail>(ApiFixture.Json, Ct))!.FeeWaived);
    }

    [Fact]
    public async Task A_cancelled_booking_cannot_be_cancelled_again()
    {
        var guest = await GuestAsync();
        var booking = await BookAsync(guest);
        await guest.PostAsJsonAsync($"/api/v1/bookings/{booking.Id}/cancel", new CancelBookingRequest(false), ApiFixture.Json, Ct);

        var again = await guest.PostAsJsonAsync($"/api/v1/bookings/{booking.Id}/cancel", new CancelBookingRequest(false), ApiFixture.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal("invalid_status_transition", await CodeAsync(again));
    }

    [Fact]
    public async Task Expiry_run_cancels_holds_past_their_15_minutes_and_leaves_live_ones()
    {
        var room = await NewRoomAsync();
        var checkIn = NextMonday();
        var expired = await SaveExpiredHoldAsync(room, checkIn, checkIn.AddDays(1));
        var live = await BookAsync(await GuestAsync());

        await using (var scope = api.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<BookingService>().ExpireHoldsAsync(Ct);
        }

        await using var db = api.Database.CreateContext();
        var stored = await db.Bookings.SingleAsync(b => b.Id == expired.Id, Ct);
        Assert.Equal((BookingStatus.Cancelled, 0m, 0m), (stored.Status, stored.CancellationFee, stored.RefundAmount));
        Assert.Equal(BookingStatus.Pending, (await db.Bookings.SingleAsync(b => b.Id == live.Id, Ct)).Status);
    }

    [Fact]
    public async Task Staff_walk_in_for_an_existing_guest_is_confirmed_and_paid_at_the_desk()
    {
        var room = await NewRoomAsync();
        var checkIn = NextMonday();
        var guestId = await UserIdAsync("guest@example.com");

        var response = await PostAsync(await StaffAsync(), Request(room, checkIn, checkIn.AddDays(2)) with { GuestId = guestId }, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = (await response.Content.ReadFromJsonAsync<BookingDetail>(ApiFixture.Json, Ct))!;
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Null(booking.ExpiresAt);
        await using var db = api.Database.CreateContext();
        var stored = await db.Bookings.SingleAsync(b => b.Id == booking.Id, Ct);
        Assert.Equal((guestId, await UserIdAsync("staff@example.com")), (stored.GuestId, stored.CreatedById));
        var payment = await db.Payments.SingleAsync(p => p.BookingId == booking.Id, Ct);
        Assert.Equal((240.00m, PaymentProvider.Desk), (payment.Amount, payment.Provider));
    }

    [Fact]
    public async Task Walk_in_with_new_guest_details_creates_a_guest_account_without_a_password()
    {
        var room = await NewRoomAsync();
        var checkIn = NextMonday();
        var email = NewEmail();
        var request = Request(room, checkIn, checkIn.AddDays(1)) with { NewGuest = new("Sophie", "Wagner", email, "+49 40 7654321") };
        var staff = await StaffAsync();
        var key = Guid.NewGuid();

        var response = await PostAsync(staff, request, key);
        var retry = await PostAsync(staff, request, key);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var booking = (await response.Content.ReadFromJsonAsync<BookingDetail>(ApiFixture.Json, Ct))!;
        await using var db = api.Database.CreateContext();
        var user = await db.Users.SingleAsync(u => u.Email == email, Ct);
        Assert.Null(user.PasswordHash);
        Assert.True(user.IsActive);
        Assert.Equal(user.Id, (await db.Bookings.SingleAsync(b => b.Id == booking.Id, Ct)).GuestId);
        var guestRole = await db.Roles.SingleAsync(r => r.Name == Roles.Guest, Ct);
        Assert.True(await db.UserRoles.AnyAsync(r => r.UserId == user.Id && r.RoleId == guestRole.Id, Ct));
    }

    [Fact]
    public async Task A_walk_in_that_hits_a_taken_room_leaves_no_new_account_behind()
    {
        var room = await NewRoomAsync();
        var checkIn = NextMonday();
        await PostAsync(await GuestAsync(), Request(room, checkIn, checkIn.AddDays(2)), Guid.NewGuid());
        var email = NewEmail();

        var response = await PostAsync(await StaffAsync(),
            Request(room, checkIn, checkIn.AddDays(1)) with { NewGuest = new("Paul", "Zimmermann", email, null) }, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var db = api.Database.CreateContext();
        Assert.False(await db.Users.AnyAsync(u => u.Email == email, Ct));
    }

    [Fact]
    public async Task A_guest_cannot_book_for_someone_else()
    {
        var room = await NewRoomAsync();
        var checkIn = NextMonday();

        var response = await PostAsync(await GuestAsync(),
            Request(room, checkIn, checkIn.AddDays(1)) with { GuestId = await UserIdAsync("staff@example.com") }, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Staff_must_say_who_the_walk_in_is_for()
    {
        var room = await NewRoomAsync();
        var checkIn = NextMonday();

        var noGuest = await PostAsync(await StaffAsync(), Request(room, checkIn, checkIn.AddDays(1)), Guid.NewGuid());
        var takenEmail = await PostAsync(await StaffAsync(),
            Request(room, checkIn, checkIn.AddDays(1)) with { NewGuest = new("Lena", "Schmidt", "guest@example.com", null) }, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.BadRequest, noGuest.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, takenEmail.StatusCode);
        using var body = JsonDocument.Parse(await takenEmail.Content.ReadAsStringAsync(Ct));
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("newGuest.email", out _));
    }

    [Fact]
    public async Task My_bookings_split_upcoming_holds_from_cancelled_ones_and_show_only_my_own()
    {
        var guest = await NewGuestAsync();
        var kept = await BookAsync(guest);
        var cancelled = await BookAsync(guest);
        await guest.PostAsJsonAsync($"/api/v1/bookings/{cancelled.Id}/cancel", new CancelBookingRequest(false), ApiFixture.Json, Ct);
        await BookAsync(await GuestAsync());

        var upcoming = await guest.GetFromJsonAsync<List<BookingSummary>>("/api/v1/bookings/my?scope=Upcoming", ApiFixture.Json, Ct);
        var past = await guest.GetFromJsonAsync<List<BookingSummary>>("/api/v1/bookings/my?scope=Past", ApiFixture.Json, Ct);

        Assert.Equal([kept.Id], upcoming!.Select(b => b.Id));
        Assert.Equal([cancelled.Id], past!.Select(b => b.Id));
    }

    private Task<HttpClient> StaffAsync() => api.CreateClientAsync("staff@example.com", "Staff123");

    private static string NewEmail() => $"walkin.{Guid.NewGuid():N}"[..20] + "@example.com";

    private async Task<Guid> UserIdAsync(string email)
    {
        await using var db = api.Database.CreateContext();
        return (await db.Users.SingleAsync(u => u.Email == email, Ct)).Id;
    }

    // Two nights in a new room at 120.00, held for the demo guest.
    private async Task<BookingDetail> BookAsync(HttpClient guest)
    {
        var room = await NewRoomAsync();
        var checkIn = NextMonday();
        var response = await PostAsync(guest, Request(room, checkIn, checkIn.AddDays(2)), Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookingDetail>(ApiFixture.Json, Ct))!;
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
