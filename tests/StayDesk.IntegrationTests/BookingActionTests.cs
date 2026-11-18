using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StayDesk.Application.Bookings;
using StayDesk.Domain.Bookings;
using StayDesk.Domain.Properties;
using StayDesk.Domain.Rooms;

namespace StayDesk.IntegrationTests;

// BO-2: staff status actions and internal notes on one booking. Each test adds its own
// Confirmed booking in its own room, with dates relative to today in Berlin.
public class BookingActionTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly TodayInBerlin() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin")).DateTime);

    [Fact]
    public async Task Staff_check_in_on_the_arrival_day_and_check_out_afterwards()
    {
        var booking = await SaveConfirmedAsync(TodayInBerlin(), nights: 2);
        var staff = await StaffAsync();

        var checkedIn = await PostAsync(staff, $"/api/v1/bookings/{booking}/check-in");
        var checkedOut = await PostAsync(staff, $"/api/v1/bookings/{booking}/check-out");

        Assert.Equal(HttpStatusCode.OK, checkedIn.StatusCode);
        var afterCheckIn = (await checkedIn.Content.ReadFromJsonAsync<BookingDetail>(ApiFixture.Json, Ct))!;
        Assert.Equal(BookingStatus.CheckedIn, afterCheckIn.Status);
        Assert.NotNull(afterCheckIn.CheckedInAt);
        var afterCheckOut = (await checkedOut.Content.ReadFromJsonAsync<BookingDetail>(ApiFixture.Json, Ct))!;
        Assert.Equal(BookingStatus.CheckedOut, afterCheckOut.Status);
        Assert.NotNull(afterCheckOut.CheckedOutAt);
    }

    [Fact]
    public async Task Check_in_before_the_arrival_day_is_an_invalid_status_transition()
    {
        var booking = await SaveConfirmedAsync(TodayInBerlin().AddDays(1), nights: 2);

        var response = await PostAsync(await StaffAsync(), $"/api/v1/bookings/{booking}/check-in");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_status_transition", await CodeAsync(response));
    }

    [Fact]
    public async Task No_show_after_the_arrival_day_charges_the_first_night()
    {
        var booking = await SaveConfirmedAsync(TodayInBerlin().AddDays(-1), nights: 3, firstNight: 150.00m);

        var response = await PostAsync(await StaffAsync(), $"/api/v1/bookings/{booking}/no-show");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var noShow = (await response.Content.ReadFromJsonAsync<BookingDetail>(ApiFixture.Json, Ct))!;
        Assert.Equal((BookingStatus.NoShow, 150.00m), (noShow.Status, noShow.CancellationFee));
    }

    [Fact]
    public async Task Staff_save_internal_notes_that_the_guest_never_sees()
    {
        var booking = await SaveConfirmedAsync(TodayInBerlin().AddDays(7), nights: 1);
        var staff = await StaffAsync();

        var saved = await staff.PatchAsJsonAsync($"/api/v1/bookings/{booking}/notes",
            new UpdateNotesRequest("  Late arrival around 23:00, key in the safe.  "), ApiFixture.Json, Ct);
        var asGuest = await (await GuestAsync()).GetFromJsonAsync<BookingDetail>($"/api/v1/bookings/{booking}", ApiFixture.Json, Ct);
        var asStaff = await staff.GetFromJsonAsync<BookingDetail>($"/api/v1/bookings/{booking}", ApiFixture.Json, Ct);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("Late arrival around 23:00, key in the safe.", asStaff!.InternalNotes);
        Assert.Null(asGuest!.InternalNotes);
    }

    [Fact]
    public async Task Notes_longer_than_2000_characters_are_rejected()
    {
        var booking = await SaveConfirmedAsync(TodayInBerlin().AddDays(7), nights: 1);

        var response = await (await StaffAsync()).PatchAsJsonAsync($"/api/v1/bookings/{booking}/notes",
            new UpdateNotesRequest(new string('x', 2001)), ApiFixture.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("internalNotes", out _));
    }

    [Fact]
    public async Task A_guest_cannot_check_in_mark_no_show_or_write_notes()
    {
        var booking = await SaveConfirmedAsync(TodayInBerlin(), nights: 1);
        var guest = await GuestAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(guest, $"/api/v1/bookings/{booking}/check-in")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(guest, $"/api/v1/bookings/{booking}/no-show")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.PatchAsJsonAsync($"/api/v1/bookings/{booking}/notes",
            new UpdateNotesRequest("Mine"), ApiFixture.Json, Ct)).StatusCode);
    }

    private Task<HttpClient> StaffAsync() => api.CreateClientAsync("staff@example.com", "Staff123");

    private Task<HttpClient> GuestAsync() => api.CreateClientAsync("guest@example.com", "Guest123");

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string url) => client.PostAsync(url, null, Ct);

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return body.RootElement.GetProperty("code").GetString();
    }

    // A walk-in for the demo guest, made by the demo staff member, in a new room at 100.00 a night.
    private async Task<Guid> SaveConfirmedAsync(DateOnly checkIn, int nights, decimal firstNight = 100.00m)
    {
        var property = new Property("Pension am Dom", "Bamberg-" + Guid.NewGuid().ToString("N")[..8], "Domplatz 3, 96049 Bamberg",
            "Small guesthouse below the cathedral.", null, new TimeOnly(15, 0), new TimeOnly(11, 0), "Europe/Berlin", DateTimeOffset.UtcNow);
        var room = new Room(property.Id, "1", RoomType.Double, 2, 100.00m);
        await using var db = api.Database.CreateContext();
        var guestId = (await db.Users.SingleAsync(u => u.Email == "guest@example.com", Ct)).Id;
        var staffId = (await db.Users.SingleAsync(u => u.Email == "staff@example.com", Ct)).Id;
        List<NightPrice> prices = [.. Enumerable.Range(0, nights).Select(i => new NightPrice
        {
            Date = checkIn.AddDays(i), BasePrice = room.BasePrice, Price = i == 0 ? firstNight : room.BasePrice,
        })];
        var booking = Booking.WalkIn($"SD-T{Guid.NewGuid():N}"[..16].ToUpperInvariant(), room.Id, guestId, staffId,
            checkIn, checkIn.AddDays(nights), 2, "Lena Schmidt", "guest@example.com", null, null, prices, Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.AddRange(property, room, booking);
        await db.SaveChangesAsync(Ct);
        return booking.Id;
    }
}
