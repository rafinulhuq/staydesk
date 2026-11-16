using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StayDesk.Application.Pricing;
using StayDesk.Application.Properties;
using StayDesk.Application.Rooms;
using StayDesk.Domain.Pricing;
using StayDesk.Domain.Rooms;
using StayDesk.Infrastructure.Persistence;

namespace StayDesk.IntegrationTests;

// PRICE-4 (quote), PRICE-8 (preview), SRCH-2 date rules.
public class PricingTests(ApiFixture api) : IClassFixture<ApiFixture>
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
    public async Task Quote_returns_each_night_with_the_rules_that_applied()
    {
        var admin = await AdminAsync();
        var (propertyId, room) = await NewRoomAsync(admin, 120.00m);
        await AddRulesAsync(
            new PriceRule(propertyId, null, "Weekend", PriceRuleType.Weekend, 15m),
            new PriceRule(propertyId, room.Id, "Three nights or more", PriceRuleType.LengthOfStay, -10m, minNights: 3));
        var thursday = NextThursday();

        var response = await api.Factory.CreateClient().PostAsJsonAsync("/api/v1/pricing/quote",
            new QuoteRequest(room.Id, thursday, thursday.AddDays(3)), ApiFixture.Json, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var quote = (await response.Content.ReadFromJsonAsync<StayQuote>(ApiFixture.Json, Ct))!;
        // Thursday 120 × 0.9 = 108.00; Friday and Saturday 120 × 1.15 × 0.9 = 124.20.
        Assert.Equal([108.00m, 124.20m, 124.20m], quote.Nights.Select(n => n.Price));
        Assert.Equal(356.40m, quote.Total);
        Assert.Equal(["Three nights or more", "Weekend"], quote.Nights[1].Rules.Select(r => r.Name).Order());
        Assert.Equal("Weekend", quote.Nights[1].Rules.Single(r => r.Name == "Weekend").Type);
    }

    [Fact]
    public async Task Quote_rejects_a_check_in_in_the_past()
    {
        var (_, room) = await NewRoomAsync(await AdminAsync(), 120.00m);
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2);

        var response = await api.Factory.CreateClient().PostAsJsonAsync("/api/v1/pricing/quote",
            new QuoteRequest(room.Id, yesterday, yesterday.AddDays(3)), ApiFixture.Json, Ct);

        Assert.True((await ReadErrorsAsync(response)).TryGetProperty("checkIn", out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(31)]
    public async Task Quote_rejects_stays_outside_1_to_30_nights(int nights)
    {
        var (_, room) = await NewRoomAsync(await AdminAsync(), 120.00m);
        var checkIn = NextThursday();

        var response = await api.Factory.CreateClient().PostAsJsonAsync("/api/v1/pricing/quote",
            new QuoteRequest(room.Id, checkIn, checkIn.AddDays(nights)), ApiFixture.Json, Ct);

        Assert.True((await ReadErrorsAsync(response)).TryGetProperty("checkOut", out _));
    }

    [Fact]
    public async Task Quote_for_a_deactivated_room_returns_404()
    {
        var admin = await AdminAsync();
        var (_, room) = await NewRoomAsync(admin, 120.00m);
        await admin.DeleteAsync($"/api/v1/rooms/{room.Id}", Ct);

        var response = await api.Factory.CreateClient().PostAsJsonAsync("/api/v1/pricing/quote",
            new QuoteRequest(room.Id, NextThursday(), NextThursday().AddDays(1)), ApiFixture.Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("room_not_found", (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Preview_shows_the_price_with_and_without_the_draft_rule_and_saves_nothing()
    {
        var admin = await AdminAsync();
        var (propertyId, room) = await NewRoomAsync(admin, 100.00m);
        var checkIn = NextThursday();
        var draft = new PriceRuleRequest(propertyId, null, "Trade fair", PriceRuleType.Seasonal, 25m,
            checkIn.AddDays(1), checkIn.AddDays(1), null, null);

        var response = await admin.PostAsJsonAsync("/api/v1/pricing/preview",
            new PreviewRequest(room.Id, checkIn, checkIn.AddDays(2), draft), ApiFixture.Json, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = (await response.Content.ReadFromJsonAsync<PricePreview>(ApiFixture.Json, Ct))!;
        Assert.Equal(200.00m, preview.Current.Total);
        Assert.Equal([100.00m, 125.00m], preview.WithRule.Nights.Select(n => n.Price));
        await using var scope = api.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StayDeskDbContext>();
        Assert.False(await db.PriceRules.IgnoreQueryFilters().AnyAsync(r => r.PropertyId == propertyId, Ct));
    }

    [Fact]
    public async Task Preview_reports_invalid_rule_fields_by_their_nested_name()
    {
        var admin = await AdminAsync();
        var (propertyId, room) = await NewRoomAsync(admin, 100.00m);
        var draft = new PriceRuleRequest(propertyId, null, "Christmas", PriceRuleType.Seasonal, 400m, null, null, null, null);

        var response = await admin.PostAsJsonAsync("/api/v1/pricing/preview",
            new PreviewRequest(room.Id, NextThursday(), NextThursday().AddDays(2), draft), ApiFixture.Json, Ct);

        var errors = await ReadErrorsAsync(response);
        Assert.True(errors.TryGetProperty("rule.percentChange", out _));
        Assert.True(errors.TryGetProperty("rule.startDate", out _));
    }

    [Fact]
    public async Task Preview_rejects_a_rule_for_another_property()
    {
        var admin = await AdminAsync();
        var (_, room) = await NewRoomAsync(admin, 100.00m);
        var draft = new PriceRuleRequest(Guid.CreateVersion7(), null, "Weekend", PriceRuleType.Weekend, 10m, null, null, null, null);

        var response = await admin.PostAsJsonAsync("/api/v1/pricing/preview",
            new PreviewRequest(room.Id, NextThursday(), NextThursday().AddDays(2), draft), ApiFixture.Json, Ct);

        Assert.True((await ReadErrorsAsync(response)).TryGetProperty("rule.propertyId", out _));
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("staff@example.com", HttpStatusCode.Forbidden)]
    public async Task Preview_is_admin_only(string? email, HttpStatusCode expected)
    {
        var (propertyId, room) = await NewRoomAsync(await AdminAsync(), 100.00m);
        var client = email is null ? api.Factory.CreateClient() : await api.CreateClientAsync(email, "Staff123");
        var draft = new PriceRuleRequest(propertyId, null, "Weekend", PriceRuleType.Weekend, 10m, null, null, null, null);

        var response = await client.PostAsJsonAsync("/api/v1/pricing/preview",
            new PreviewRequest(room.Id, NextThursday(), NextThursday().AddDays(2), draft), ApiFixture.Json, Ct);

        Assert.Equal(expected, response.StatusCode);
    }

    private Task<HttpClient> AdminAsync() => api.CreateClientAsync("admin@example.com", "Admin123");

    private static async Task<(Guid PropertyId, RoomDetail Room)> NewRoomAsync(HttpClient admin, decimal basePrice)
    {
        var property = await admin.PostAsJsonAsync("/api/v1/properties", new PropertyRequest(
            "Gasthof Zur Linde", "Bamberg", "Lange Straße 12, 96047 Bamberg", "Fachwerkhaus in der Altstadt.", null,
            new TimeOnly(15, 0), new TimeOnly(11, 0), "Europe/Berlin"), Ct);
        property.EnsureSuccessStatusCode();
        var propertyId = (await property.Content.ReadFromJsonAsync<PropertyDetail>(ApiFixture.Json, Ct))!.Id;

        var room = await admin.PostAsJsonAsync("/api/v1/rooms",
            new CreateRoomRequest(propertyId, "1", RoomType.Double, 2, basePrice, [], []), ApiFixture.Json, Ct);
        room.EnsureSuccessStatusCode();
        return (propertyId, (await room.Content.ReadFromJsonAsync<RoomDetail>(ApiFixture.Json, Ct))!);
    }

    // Rules go straight into the database; the price rule API arrives in phase 3e.
    private async Task AddRulesAsync(params PriceRule[] rules)
    {
        await using var scope = api.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StayDeskDbContext>();
        db.PriceRules.AddRange(rules);
        await db.SaveChangesAsync(Ct);
    }

    private static async Task<JsonElement> ReadErrorsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
    }
}
