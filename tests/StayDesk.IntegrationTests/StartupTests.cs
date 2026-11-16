using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StayDesk.Api.Middleware;
using StayDesk.Infrastructure.Identity;
using StayDesk.Infrastructure.Persistence;
using StayDesk.Infrastructure.Persistence.Seeding;

namespace StayDesk.IntegrationTests;

// Startup wiring: health check (NFR-7), correlation IDs (NFR-8), demo seed (AUTH-4, PROP-4).
public class StartupTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Api_uses_the_test_container_database()
    {
        await using var scope = api.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StayDeskDbContext>();

        Assert.Equal(api.Database.ConnectionString, db.Database.GetConnectionString());
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy_when_the_database_is_up()
    {
        var response = await api.Factory.CreateClient().GetAsync("/health", Ct);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task A_valid_incoming_correlation_id_is_echoed()
    {
        var client = api.Factory.CreateClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "booking-flow-42");

        var response = await client.GetAsync("/health", Ct);

        Assert.Equal("booking-flow-42", response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }

    [Fact]
    public async Task An_unsafe_correlation_id_is_replaced()
    {
        var client = api.Factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, "x<script>");

        var response = await client.GetAsync("/health", Ct);

        var id = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();
        Assert.NotEqual("x<script>", id);
        Assert.Equal(32, id.Length);
    }

    [Fact]
    public async Task Startup_seeds_three_cities_with_six_to_ten_rooms_each()
    {
        await using var db = api.Database.CreateContext();

        var roomsPerCity = await db.Properties
            .Select(p => new { p.City, Rooms = db.Rooms.Count(r => r.PropertyId == p.Id) })
            .ToListAsync(Ct);

        Assert.Equal(["Berlin", "Hamburg", "Munich"], roomsPerCity.Select(p => p.City).Order());
        Assert.All(roomsPerCity, p => Assert.InRange(p.Rooms, 6, 10));
    }

    [Fact]
    public async Task Demo_admin_holds_admin_and_staff_roles_and_is_flagged_as_demo()
    {
        await using var scope = api.Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var admin = await users.FindByEmailAsync("admin@example.com");

        Assert.NotNull(admin);
        Assert.True(admin.IsDemo);
        Assert.Equal(["Admin", "Staff"], (await users.GetRolesAsync(admin)).Order());
        Assert.True(await users.CheckPasswordAsync(admin, "Admin123"));
    }

    [Fact]
    public async Task Running_the_seeder_again_adds_nothing()
    {
        await using var before = api.Database.CreateContext();
        var counts = await CountsAsync(before);

        await using (var scope = api.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync(Ct);
        }

        await using var after = api.Database.CreateContext();
        Assert.Equal(counts, await CountsAsync(after));
    }

    private static async Task<(int, int, int, int, int)> CountsAsync(StayDeskDbContext db) => (
        await db.Properties.CountAsync(Ct),
        await db.Rooms.CountAsync(Ct),
        await db.Amenities.CountAsync(Ct),
        await db.PriceRules.CountAsync(Ct),
        await db.Users.CountAsync(Ct));
}
