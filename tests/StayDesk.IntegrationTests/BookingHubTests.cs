using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;
using StayDesk.Application.Auth;
using StayDesk.Application.Bookings;
using StayDesk.Domain.Bookings;
using StayDesk.Domain.Properties;
using StayDesk.Domain.Rooms;

namespace StayDesk.IntegrationTests;

// CAL-2 and NFR-8: booking changes reach the staff who watch that property or the dashboard,
// and the hub's query-string token never reaches the logs.
public class BookingHubTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private sealed record Payload(Guid BookingId, Guid PropertyId, Guid RoomId, DateOnly CheckIn, DateOnly CheckOut, BookingStatus Status);

    [Fact]
    public async Task Staff_watching_a_property_get_BookingCreated_within_2_seconds_and_watchers_of_another_property_do_not()
    {
        var room = await NewRoomAsync();
        var otherRoom = await NewRoomAsync();
        await using var watcher = await ConnectAsync();
        await using var otherWatcher = await ConnectAsync();
        var created = Expect(watcher, "BookingCreated");
        var elsewhere = Expect(otherWatcher, "BookingCreated");
        await watcher.InvokeAsync("WatchProperty", room.PropertyId, Ct);
        await otherWatcher.InvokeAsync("WatchProperty", otherRoom.PropertyId, Ct);

        var clock = Stopwatch.StartNew();
        var booking = await BookAsync(room);
        var payload = await created.WaitAsync(Wait, Ct);

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"took {clock.Elapsed}");
        Assert.Equal(new Payload(booking.Id, room.PropertyId, room.Id, booking.CheckIn, booking.CheckOut, BookingStatus.Pending), payload);
        await Task.Delay(500, Ct);
        Assert.False(elsewhere.IsCompleted);
    }

    [Fact]
    public async Task The_expiry_job_tells_the_dashboard_a_hold_was_cancelled()
    {
        var room = await NewRoomAsync();
        var hold = await SaveExpiredHoldAsync(room);
        await using var dashboard = await ConnectAsync();
        var cancelled = Expect(dashboard, "BookingCancelled", hold.Id);
        await dashboard.InvokeAsync("WatchDashboard", Ct);

        await using (var scope = api.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<BookingService>().ExpireHoldsAsync(Ct);
        }

        var payload = await cancelled.WaitAsync(Wait, Ct);
        Assert.Equal((room.PropertyId, BookingStatus.Cancelled), (payload.PropertyId, payload.Status));
    }

    [Fact]
    public async Task A_guest_cannot_connect_to_the_bookings_hub()
    {
        var guest = await api.CreateClientAsync("guest@example.com", "Guest123");

        var response = await guest.PostAsync("/hubs/bookings/negotiate?negotiateVersion=1", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_token_in_the_query_string_opens_the_hub_and_is_never_logged()
    {
        var sink = new CapturingSink();
        await using var factory = api.Factory.WithWebHostBuilder(host =>
            host.ConfigureTestServices(services => services.AddSingleton<ILogEventSink>(sink)));
        var token = await TokenAsync(factory.CreateClient(), "staff@example.com", "Staff123");

        var response = await factory.CreateClient().PostAsync($"/hubs/bookings/negotiate?negotiateVersion=1&access_token={token}", null, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // The login above shows the sink receives this host's request logs. (Serilog's request
        // logging writes no line for the negotiate request itself; seen in this test.)
        Assert.Contains(sink.Events, e => e.Properties.TryGetValue("RequestPath", out var path) && path.ToString().Contains("/api/v1/auth/login"));
        Assert.DoesNotContain(sink.Events, e => e.RenderMessage().Contains(token) || e.Properties.Values.Any(v => v.ToString().Contains(token)));
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }

    // Long polling through the in-memory test server; the browser uses WebSockets.
    private async Task<HubConnection> ConnectAsync()
    {
        var token = await TokenAsync(api.Factory.CreateClient(), "staff@example.com", "Staff123");
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(api.Factory.Server.BaseAddress, "hubs/bookings"), options =>
            {
                options.HttpMessageHandlerFactory = _ => api.Factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();
        await connection.StartAsync(Ct);
        return connection;
    }

    private static Task<Payload> Expect(HubConnection connection, string eventName, Guid? bookingId = null)
    {
        var received = new TaskCompletionSource<Payload>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<Payload>(eventName, payload =>
        {
            if (bookingId is null || payload.BookingId == bookingId)
            {
                received.TrySetResult(payload);
            }
        });
        return received.Task;
    }

    private static async Task<string> TokenAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password), Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthTokens>(Ct))!.AccessToken;
    }

    private async Task<BookingDetail> BookAsync(Room room)
    {
        var guest = await api.CreateClientAsync("guest@example.com", "Guest123");
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/bookings")
        {
            Content = JsonContent.Create(new CreateBookingRequest(room.Id, checkIn, checkIn.AddDays(2), 2, "Lena Schmidt",
                "guest@example.com", null, null, null), options: ApiFixture.Json),
        };
        message.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await guest.SendAsync(message, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookingDetail>(ApiFixture.Json, Ct))!;
    }

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
