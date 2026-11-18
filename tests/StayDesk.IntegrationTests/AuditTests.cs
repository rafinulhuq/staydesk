using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StayDesk.Application.Auditing;
using StayDesk.Application.Auth;
using StayDesk.Application.Bookings;
using StayDesk.Application.Paging;
using StayDesk.Domain.Auditing;
using StayDesk.Domain.Bookings;
using StayDesk.Domain.Properties;
using StayDesk.Domain.Rooms;

namespace StayDesk.IntegrationTests;

// AUD-1..4 and BO-4: the EF interceptor writes the entries; Staff read one booking's history,
// Admin reads the whole log.
public class AuditTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Staff_cancelling_a_booking_is_audited_with_old_and_new_values_and_their_name_and_staff_read_it_as_history()
    {
        var room = await NewRoomAsync();
        var guest = await api.CreateClientAsync("guest@example.com", "Guest123");
        var booking = await BookAsync(guest, room);
        var staff = await StaffAsync();
        await staff.PostAsJsonAsync($"/api/v1/bookings/{booking.Id}/cancel", new CancelBookingRequest(WaiveFee: true), ApiFixture.Json, Ct);

        var history = await staff.GetFromJsonAsync<List<AuditEntrySummary>>($"/api/v1/bookings/{booking.Id}/history", ApiFixture.Json, Ct);

        Assert.NotNull(history);
        Assert.Equal([AuditAction.Created, AuditAction.Updated], history.Select(e => e.Action));
        Assert.Equal((await NameOfAsync("guest@example.com"), await NameOfAsync("staff@example.com")),
            (history[0].UserDisplayName, history[1].UserDisplayName));
        var changes = history[1].Changes;
        Assert.Equal("Pending", changes.GetProperty("Status").GetProperty("old").GetString());
        Assert.Equal("Cancelled", changes.GetProperty("Status").GetProperty("new").GetString());
        Assert.True(changes.GetProperty("FeeWaived").GetProperty("new").GetBoolean());
        Assert.False(changes.TryGetProperty("GuestEmail", out _));
        Assert.NotNull(history[1].CorrelationId);
    }

    [Fact]
    public async Task Deactivating_and_reactivating_a_room_are_recorded_as_such()
    {
        var room = await NewRoomAsync();
        var admin = await api.CreateClientAsync("admin@example.com", "Admin123");
        await admin.DeleteAsync($"/api/v1/rooms/{room.Id}", Ct);
        await admin.PostAsync($"/api/v1/rooms/{room.Id}/reactivate", null, Ct);

        var log = await admin.GetFromJsonAsync<Paged<AuditEntrySummary>>($"/api/v1/audit?entity=Room&entityId={room.Id}", ApiFixture.Json, Ct);

        Assert.Equal([AuditAction.Reactivated, AuditAction.Deactivated], log!.Items.Select(e => e.Action));
    }

    [Fact]
    public async Task The_expiry_job_is_recorded_as_system()
    {
        var room = await NewRoomAsync();
        var hold = await SaveExpiredHoldAsync(room);

        await using (var scope = api.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<BookingService>().ExpireHoldsAsync(Ct);
        }

        await using var db = api.Database.CreateContext();
        var entry = await db.AuditEntries.SingleAsync(a => a.EntityId == hold.Id, Ct);
        Assert.Equal((null, "system", AuditAction.Updated), (entry.UserId, entry.UserDisplayName, entry.Action));
    }

    [Fact]
    public async Task Registration_records_the_guest_role_and_no_entry_ever_holds_a_password_hash_or_token()
    {
        var email = $"jonas.vogel.{Guid.NewGuid():N}"[..20] + "@example.com";
        (await api.Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Jonas", "Vogel", email, "Reise2026"), Ct)).EnsureSuccessStatusCode();

        await using var db = api.Database.CreateContext();
        var userId = (await db.Users.SingleAsync(u => u.Email == email, Ct)).Id;
        var roleGiven = await db.AuditEntries.SingleAsync(a => a.EntityName == "UserRole" && a.EntityId == userId, Ct);
        Assert.Equal((AuditAction.Created, "anonymous"), (roleGiven.Action, roleGiven.UserDisplayName));
        Assert.Contains("\"Guest\"", roleGiven.Changes);
        var everything = await db.AuditEntries.Select(a => a.Changes).ToListAsync(Ct);
        Assert.DoesNotContain(everything, c => c.Contains("PasswordHash") || c.Contains("SecurityStamp") || c.Contains("TokenHash"));
    }

    [Fact]
    public async Task Twenty_racing_bookings_leave_one_created_entry_not_twenty()
    {
        var room = await NewRoomAsync();
        var guest = await api.CreateClientAsync("guest@example.com", "Guest123");
        var request = Request(room);

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => PostAsync(guest, request, Guid.NewGuid())));

        await using var db = api.Database.CreateContext();
        var created = await db.AuditEntries.Where(a => a.EntityName == "Booking" && a.Action == AuditAction.Created)
            .Select(a => a.Changes).ToListAsync(Ct);
        Assert.Single(created, c => c.Contains(room.Id.ToString()));
    }

    [Fact]
    public async Task Only_staff_read_a_booking_history_and_only_admin_reads_the_audit_log()
    {
        var booking = await BookAsync(await api.CreateClientAsync("guest@example.com", "Guest123"), await NewRoomAsync());
        var guest = await api.CreateClientAsync("guest@example.com", "Guest123");
        var staff = await StaffAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await guest.GetAsync($"/api/v1/bookings/{booking.Id}/history", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/audit", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/api/v1/bookings/{Guid.NewGuid()}/history", Ct)).StatusCode);
    }

    private Task<HttpClient> StaffAsync() => api.CreateClientAsync("staff@example.com", "Staff123");

    private async Task<string> NameOfAsync(string email)
    {
        await using var db = api.Database.CreateContext();
        var user = await db.Users.SingleAsync(u => u.Email == email, Ct);
        return $"{user.FirstName} {user.LastName}";
    }

    // Two weeks out, so the booking is free to cancel and no last-minute rule applies.
    private static CreateBookingRequest Request(Room room)
    {
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);
        return new(room.Id, checkIn, checkIn.AddDays(2), 2, "Lena Schmidt", "guest@example.com", null, null, null);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, CreateBookingRequest request, Guid key)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/bookings") { Content = JsonContent.Create(request, options: ApiFixture.Json) };
        message.Headers.Add("Idempotency-Key", key.ToString());
        return await client.SendAsync(message, Ct);
    }

    private static async Task<BookingDetail> BookAsync(HttpClient guest, Room room)
    {
        var response = await PostAsync(guest, Request(room), Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookingDetail>(ApiFixture.Json, Ct))!;
    }

    // Added straight to the database, so it has no audit entry of its own.
    private async Task<Room> NewRoomAsync()
    {
        var property = new Property("Pension am Dom", "Bamberg-" + Guid.NewGuid().ToString("N")[..8], "Domplatz 3, 96049 Bamberg",
            "Small guesthouse below the cathedral.", null, new TimeOnly(15, 0), new TimeOnly(11, 0), "Europe/Berlin", DateTimeOffset.UtcNow);
        var room = new Room(property.Id, "1", RoomType.Double, 2, 120.00m);
        await using var db = api.Database.CreateContext();
        db.AddRange(property, room);
        await db.SaveChangesAsync(Ct);
        return room;
    }

    private async Task<Booking> SaveExpiredHoldAsync(Room room)
    {
        await using var db = api.Database.CreateContext();
        var guestId = (await db.Users.SingleAsync(u => u.Email == "guest@example.com", Ct)).Id;
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);
        var hold = new Booking($"SD-T{Guid.NewGuid():N}"[..16].ToUpperInvariant(), room.Id, guestId, checkIn, checkIn.AddDays(1), 2,
            "Lena Schmidt", "guest@example.com", null, null,
            [new NightPrice { Date = checkIn, BasePrice = room.BasePrice, Price = room.BasePrice }], null, DateTimeOffset.UtcNow - TimeSpan.FromHours(1));
        db.Bookings.Add(hold);
        await db.SaveChangesAsync(Ct);
        return hold;
    }
}
