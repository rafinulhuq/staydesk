using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using StayDesk.Application.Auth;
using StayDesk.Application.Bookings;

namespace StayDesk.IntegrationTests;

// BOOK-9: staff find the existing guest a walk-in is for (GET /guests).
public class GuestSearchTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Staff_find_active_guests_by_name_or_email_but_not_staff_or_deactivated_guests()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var vogel = await RegisterAsync("Jonas", "Vogel" + tag, $"jonas.{tag}@example.com");
        var inactive = await RegisterAsync("Petra", "Vogel" + tag, $"petra.{tag}@example.com");
        await using (var db = api.Database.CreateContext())
        {
            (await db.Users.SingleAsync(u => u.Id == inactive, Ct)).IsActive = false;
            await db.SaveChangesAsync(Ct);
        }
        var staff = await api.CreateClientAsync("staff@example.com", "Staff123");

        var byName = await staff.GetFromJsonAsync<List<GuestSummary>>($"/api/v1/guests?search=jonas vogel{tag}", ApiFixture.Json, Ct);
        var byEmail = await staff.GetFromJsonAsync<List<GuestSummary>>($"/api/v1/guests?search=JONAS.{tag}", ApiFixture.Json, Ct);
        var staffAccount = await staff.GetFromJsonAsync<List<GuestSummary>>("/api/v1/guests?search=staff@example.com", ApiFixture.Json, Ct);
        var lastName = await staff.GetFromJsonAsync<List<GuestSummary>>($"/api/v1/guests?search=vogel{tag}", ApiFixture.Json, Ct);

        Assert.Equal([vogel], byName!.Select(g => g.Id));
        Assert.Equal([vogel], byEmail!.Select(g => g.Id));
        Assert.Empty(staffAccount!);
        Assert.Equal([vogel], lastName!.Select(g => g.Id));
    }

    [Fact]
    public async Task A_guest_cannot_search_guests()
    {
        var guest = await api.CreateClientAsync("guest@example.com", "Guest123");

        Assert.Equal(HttpStatusCode.Forbidden, (await guest.GetAsync("/api/v1/guests?search=schmidt", Ct)).StatusCode);
    }

    [Fact]
    public async Task A_one_character_search_is_a_validation_error()
    {
        var staff = await api.CreateClientAsync("staff@example.com", "Staff123");

        Assert.Equal(HttpStatusCode.BadRequest, (await staff.GetAsync("/api/v1/guests?search=s", Ct)).StatusCode);
    }

    private async Task<Guid> RegisterAsync(string firstName, string lastName, string email)
    {
        var response = await api.Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(firstName, lastName, email, "Reise2026"), Ct);
        response.EnsureSuccessStatusCode();
        await using var db = api.Database.CreateContext();
        return (await db.Users.SingleAsync(u => u.Email == email, Ct)).Id;
    }
}
