using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StayDesk.Application.Properties;

namespace StayDesk.IntegrationTests;

// PROP-1 (property management) and PROP-3 (soft delete hides from public, keeps for Admin).
public class PropertyTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Public_list_filters_by_city_ignoring_case()
    {
        var properties = await api.Factory.CreateClient()
            .GetFromJsonAsync<List<PropertySummary>>("/api/v1/properties?city=berlin", ApiFixture.Json, Ct);

        var property = Assert.Single(properties!);
        Assert.Equal("Hotel Spreeblick", property.Name);
    }

    [Fact]
    public async Task Property_detail_lists_its_active_rooms()
    {
        var anonymous = api.Factory.CreateClient();
        var berlin = (await anonymous.GetFromJsonAsync<List<PropertySummary>>("/api/v1/properties?city=Berlin", ApiFixture.Json, Ct))!.Single();

        var detail = await anonymous.GetFromJsonAsync<PropertyDetail>($"/api/v1/properties/{berlin.Id}", ApiFixture.Json, Ct);

        Assert.InRange(detail!.Rooms.Count, 6, 10);
        Assert.All(detail.Rooms, room => Assert.True(room.IsActive));
    }

    [Fact]
    public async Task Deactivated_property_is_hidden_from_the_public_until_reactivated()
    {
        var admin = await api.CreateClientAsync("admin@example.com", "Admin123");
        var anonymous = api.Factory.CreateClient();
        var created = await admin.PostAsJsonAsync("/api/v1/properties", NewProperty("Haus am Dom") with { City = "Würzburg" }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var property = (await created.Content.ReadFromJsonAsync<PropertyDetail>(ApiFixture.Json, Ct))!;

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/properties/{property.Id}", Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/properties/{property.Id}", Ct)).StatusCode);
        var publicList = await anonymous.GetFromJsonAsync<List<PropertySummary>>("/api/v1/properties?city=Würzburg", ApiFixture.Json, Ct);
        Assert.Empty(publicList!);
        var adminList = await admin.GetFromJsonAsync<List<PropertySummary>>("/api/v1/properties?city=Würzburg&includeInactive=true", ApiFixture.Json, Ct);
        Assert.False(Assert.Single(adminList!).IsActive);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/properties/{property.Id}/reactivate", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/v1/properties/{property.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Admin_edits_every_property_field()
    {
        var admin = await api.CreateClientAsync("admin@example.com", "Admin123");
        var created = await admin.PostAsJsonAsync("/api/v1/properties", NewProperty("Gasthof Lindenhof"), Ct);
        var id = (await created.Content.ReadFromJsonAsync<PropertyDetail>(ApiFixture.Json, Ct))!.Id;
        var edit = new PropertyRequest("Gasthof Linde", "Bonn", "Markt 3, 53111 Bonn", "Am Marktplatz.", null,
            new TimeOnly(14, 0), new TimeOnly(10, 30), "Europe/Berlin");

        var response = await admin.PutAsJsonAsync($"/api/v1/properties/{id}", edit, Ct);

        var updated = (await response.Content.ReadFromJsonAsync<PropertyDetail>(ApiFixture.Json, Ct))!;
        Assert.Equal(("Gasthof Linde", "Bonn", "Markt 3, 53111 Bonn", new TimeOnly(14, 0), new TimeOnly(10, 30)),
            (updated.Name, updated.City, updated.Address, updated.CheckInTime, updated.CheckOutTime));
    }

    [Fact]
    public async Task Include_inactive_is_forbidden_for_non_admins()
    {
        var guest = await api.CreateClientAsync("guest@example.com", "Guest123");
        var staff = await api.CreateClientAsync("staff@example.com", "Staff123");

        Assert.Equal(HttpStatusCode.Forbidden, (await guest.GetAsync("/api/v1/properties?includeInactive=true", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/properties?includeInactive=true", Ct)).StatusCode);
    }

    [Fact]
    public async Task Only_admin_can_create_properties()
    {
        var staff = await api.CreateClientAsync("staff@example.com", "Staff123");

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await api.Factory.CreateClient().PostAsJsonAsync("/api/v1/properties", NewProperty("Pension Elster"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await staff.PostAsJsonAsync("/api/v1/properties", NewProperty("Pension Elster"), Ct)).StatusCode);
    }

    [Theory]
    [InlineData("W. Europe Standard Time")]
    [InlineData("Europe/Atlantis")]
    public async Task Time_zone_must_be_an_iana_id(string timeZone)
    {
        var admin = await api.CreateClientAsync("admin@example.com", "Admin123");

        var response = await admin.PostAsJsonAsync("/api/v1/properties", NewProperty("Hotel Rheinblick") with { TimeZone = timeZone }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty("timeZone", out _));
    }

    private static PropertyRequest NewProperty(string name) => new(
        name, "Köln", "Domkloster 4, 50667 Köln", "Zwei Minuten vom Dom.", "https://images.example.com/koeln.jpg",
        new TimeOnly(15, 0), new TimeOnly(11, 0), "Europe/Berlin");
}
