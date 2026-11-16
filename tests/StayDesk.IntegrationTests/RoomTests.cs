using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using StayDesk.Application.Properties;
using StayDesk.Application.Rooms;
using StayDesk.Domain.Bookings;
using StayDesk.Domain.Rooms;
using StayDesk.Infrastructure.Persistence;

namespace StayDesk.IntegrationTests;

// PROP-2 (room management), PROP-3 (soft delete) and PROP-5 (deactivation warning).
public class RoomTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Admin_creates_a_room_with_amenities()
    {
        var admin = await AdminAsync();
        var amenities = (await admin.GetFromJsonAsync<List<AmenitySummary>>("/api/v1/amenities", ApiFixture.Json, Ct))!;

        var response = await admin.PostAsJsonAsync("/api/v1/rooms",
            NewRoom(await NewPropertyIdAsync(admin), "101", [amenities[1].Id, amenities[0].Id]), ApiFixture.Json, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var room = (await response.Content.ReadFromJsonAsync<RoomDetail>(ApiFixture.Json, Ct))!;
        Assert.Equal([amenities[0].Name, amenities[1].Name], room.Amenities.Select(a => a.Name));
        Assert.Equal(RoomType.Double, room.Type);
    }

    [Fact]
    public async Task Room_number_must_be_unique_within_a_property()
    {
        var admin = await AdminAsync();
        var propertyId = await NewPropertyIdAsync(admin);
        await CreateRoomAsync(admin, propertyId, "201");

        var duplicate = await admin.PostAsJsonAsync("/api/v1/rooms", NewRoom(propertyId, "201", []), ApiFixture.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        var problem = await duplicate.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty("roomNumber", out _));
    }

    [Fact]
    public async Task Max_guests_outside_1_to_10_is_rejected()
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsJsonAsync("/api/v1/rooms",
            NewRoom(await NewPropertyIdAsync(admin), "301", []) with { MaxGuests = 11 }, ApiFixture.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Editing_with_a_stale_version_returns_409()
    {
        var admin = await AdminAsync();
        var room = await CreateRoomAsync(admin, await NewPropertyIdAsync(admin), "401");
        var edit = new UpdateRoomRequest("401", RoomType.Suite, 4, 210.00m, [], [], room.Version);

        var first = await admin.PutAsJsonAsync($"/api/v1/rooms/{room.Id}", edit, ApiFixture.Json, Ct);
        var second = await admin.PutAsJsonAsync($"/api/v1/rooms/{room.Id}", edit with { BasePrice = 199.00m }, ApiFixture.Json, Ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("concurrency_conflict", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_amenities_only_edit_changes_the_version()
    {
        var admin = await AdminAsync();
        var amenityId = (await admin.GetFromJsonAsync<List<AmenitySummary>>("/api/v1/amenities", ApiFixture.Json, Ct))![0].Id;
        var room = await CreateRoomAsync(admin, await NewPropertyIdAsync(admin), "501");
        var edit = new UpdateRoomRequest(room.RoomNumber, room.Type, room.MaxGuests, room.BasePrice, room.ImageUrls, [amenityId], room.Version);

        var response = await admin.PutAsJsonAsync($"/api/v1/rooms/{room.Id}", edit, ApiFixture.Json, Ct);

        var updated = (await response.Content.ReadFromJsonAsync<RoomDetail>(ApiFixture.Json, Ct))!;
        Assert.NotEqual(room.Version, updated.Version);
        Assert.Single(updated.Amenities);
    }

    [Fact]
    public async Task Deactivating_a_room_lists_its_active_bookings_and_hides_it()
    {
        var admin = await AdminAsync();
        var room = await CreateRoomAsync(admin, await NewPropertyIdAsync(admin), "601");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTimeOffset.UtcNow;
        var held = await AddBookingAsync(room.Id, today.AddDays(10), today.AddDays(12), createdAt: now);
        await AddBookingAsync(room.Id, today.AddDays(20), today.AddDays(22), createdAt: now.AddHours(-1));

        var response = await admin.DeleteAsync($"/api/v1/rooms/{room.Id}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<RoomDeactivation>(ApiFixture.Json, Ct))!;
        Assert.Equal(held.Reference, Assert.Single(result.ActiveBookings).Reference);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Factory.CreateClient().GetAsync($"/api/v1/rooms/{room.Id}", Ct)).StatusCode);
        Assert.False((await admin.GetFromJsonAsync<RoomDetail>($"/api/v1/rooms/{room.Id}", ApiFixture.Json, Ct))!.IsActive);
    }

    [Fact]
    public async Task Reactivated_room_is_public_again()
    {
        var admin = await AdminAsync();
        var room = await CreateRoomAsync(admin, await NewPropertyIdAsync(admin), "701");
        await admin.DeleteAsync($"/api/v1/rooms/{room.Id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/rooms/{room.Id}/reactivate", null, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await api.Factory.CreateClient().GetAsync($"/api/v1/rooms/{room.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Active_room_in_a_deactivated_property_is_hidden_from_the_public()
    {
        var admin = await AdminAsync();
        var propertyId = await NewPropertyIdAsync(admin);
        var room = await CreateRoomAsync(admin, propertyId, "801");

        await admin.DeleteAsync($"/api/v1/properties/{propertyId}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, (await api.Factory.CreateClient().GetAsync($"/api/v1/rooms/{room.Id}", Ct)).StatusCode);
    }

    private Task<HttpClient> AdminAsync() => api.CreateClientAsync("admin@example.com", "Admin123");

    private static async Task<Guid> NewPropertyIdAsync(HttpClient admin)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/properties", new PropertyRequest(
            "Hotel Am Stadtpark", "Leipzig", "Parkstraße 7, 04103 Leipzig", "Ruhige Lage am Park.", null,
            new TimeOnly(15, 0), new TimeOnly(11, 0), "Europe/Berlin"), Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PropertyDetail>(ApiFixture.Json, Ct))!.Id;
    }

    private static async Task<RoomDetail> CreateRoomAsync(HttpClient admin, Guid propertyId, string roomNumber)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/rooms", NewRoom(propertyId, roomNumber, []), ApiFixture.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RoomDetail>(ApiFixture.Json, Ct))!;
    }

    private static CreateRoomRequest NewRoom(Guid propertyId, string roomNumber, IReadOnlyList<Guid> amenityIds) =>
        new(propertyId, roomNumber, RoomType.Double, 2, 129.00m, ["https://images.example.com/room.jpg"], amenityIds);

    // Pending bookings straight in the database; the booking API arrives in phase 4.
    // One created now is still held (active); one created an hour ago has expired (SRCH-3).
    private async Task<Booking> AddBookingAsync(Guid roomId, DateOnly checkIn, DateOnly checkOut, DateTimeOffset createdAt)
    {
        await using var scope = api.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StayDeskDbContext>();
        var guestId = db.Users.Single(u => u.Email == "guest@example.com").Id;
        var nights = Enumerable.Range(0, checkOut.DayNumber - checkIn.DayNumber)
            .Select(i => new NightPrice { Date = checkIn.AddDays(i), BasePrice = 129.00m, Price = 129.00m })
            .ToList();
        var booking = new Booking($"SD-T-{Guid.NewGuid():N}"[..16], roomId, guestId, checkIn, checkOut, 2,
            "Lena Schmidt", "guest@example.com", null, null, nights, Guid.NewGuid(), createdAt);
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(Ct);
        return booking;
    }
}
