using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using StayDesk.Application.Pricing;
using StayDesk.Domain.Pricing;
using StayDesk.Domain.Properties;
using StayDesk.Domain.Rooms;
using StayDesk.Infrastructure.Persistence;

namespace StayDesk.IntegrationTests;

// PRICE-2/3 rule management through /price-rules (Admin only, DELETE = deactivate).
public class PriceRuleTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly NextThursday()
    {
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);
        while (day.DayOfWeek != DayOfWeek.Thursday)
        {
            day = day.AddDays(1);
        }
        return day;
    }

    private static PriceRuleRequest Weekend(Guid propertyId, Guid? roomId = null) =>
        new(propertyId, roomId, "Weekend", PriceRuleType.Weekend, 20m, null, null, null, null);

    [Fact]
    public async Task A_saved_rule_prices_quotes_until_it_is_deactivated()
    {
        var admin = await AdminAsync();
        var (property, room) = await NewRoomAsync();
        var thursday = NextThursday();
        var quote = new QuoteRequest(room.Id, thursday, thursday.AddDays(2));

        var created = await admin.PostAsJsonAsync("/api/v1/price-rules", Weekend(property.Id), ApiFixture.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var rule = (await created.Content.ReadFromJsonAsync<PriceRuleDetail>(ApiFixture.Json, Ct))!;
        var listed = await admin.GetFromJsonAsync<PriceRuleDetail[]>($"/api/v1/price-rules?propertyId={property.Id}", ApiFixture.Json, Ct);
        var withRule = await QuoteAsync(quote);

        var deleted = await admin.DeleteAsync($"/api/v1/price-rules/{rule.Id}", Ct);
        var withoutRule = await QuoteAsync(quote);

        Assert.Equal([rule.Id], listed!.Select(r => r.Id));
        // Thursday 100.00 + Friday 120.00.
        Assert.Equal(220.00m, withRule.Total);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(200.00m, withoutRule.Total);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/price-rules/{rule.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Update_can_change_the_rule_type_and_its_fields()
    {
        var admin = await AdminAsync();
        var (property, room) = await NewRoomAsync();
        var created = await admin.PostAsJsonAsync("/api/v1/price-rules", Weekend(property.Id), ApiFixture.Json, Ct);
        var rule = (await created.Content.ReadFromJsonAsync<PriceRuleDetail>(ApiFixture.Json, Ct))!;

        var response = await admin.PutAsJsonAsync($"/api/v1/price-rules/{rule.Id}",
            new PriceRuleRequest(property.Id, room.Id, "Week or longer", PriceRuleType.LengthOfStay, -15m, null, null, 7, null),
            ApiFixture.Json, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<PriceRuleDetail>(ApiFixture.Json, Ct))!;
        Assert.Equal((room.Id, PriceRuleType.LengthOfStay, -15m, 7), (updated.RoomId!.Value, updated.Type, updated.PercentChange, updated.MinNights!.Value));
    }

    [Fact]
    public async Task A_room_rule_must_name_a_room_of_the_same_property()
    {
        var admin = await AdminAsync();
        var (property, _) = await NewRoomAsync();
        var (_, otherRoom) = await NewRoomAsync();

        var response = await admin.PostAsJsonAsync("/api/v1/price-rules", Weekend(property.Id, otherRoom.Id), ApiFixture.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("roomId", out _));
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("staff@example.com", HttpStatusCode.Forbidden)]
    public async Task Price_rules_are_admin_only(string? email, HttpStatusCode expected)
    {
        var (property, _) = await NewRoomAsync();
        var client = email is null ? api.Factory.CreateClient() : await api.CreateClientAsync(email, "Staff123");

        var response = await client.PostAsJsonAsync("/api/v1/price-rules", Weekend(property.Id), ApiFixture.Json, Ct);

        Assert.Equal(expected, response.StatusCode);
    }

    private Task<HttpClient> AdminAsync() => api.CreateClientAsync("admin@example.com", "Admin123");

    private async Task<StayQuote> QuoteAsync(QuoteRequest request)
    {
        var response = await api.Factory.CreateClient().PostAsJsonAsync("/api/v1/pricing/quote", request, ApiFixture.Json, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StayQuote>(ApiFixture.Json, Ct))!;
    }

    private async Task<(Property Property, Room Room)> NewRoomAsync()
    {
        var property = new Property("Gasthof Zur Linde", "Bamberg", "Lange Straße 12, 96047 Bamberg", "Fachwerkhaus in der Altstadt.",
            null, new TimeOnly(15, 0), new TimeOnly(11, 0), "Europe/Berlin", DateTimeOffset.UtcNow);
        var room = new Room(property.Id, "1", RoomType.Double, 2, 100.00m);
        await using var scope = api.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StayDeskDbContext>();
        db.AddRange(property, room);
        await db.SaveChangesAsync(Ct);
        return (property, room);
    }
}
