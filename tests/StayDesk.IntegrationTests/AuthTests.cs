using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StayDesk.Application.Auth;
using StayDesk.Infrastructure.Identity;
using StayDesk.Infrastructure.Persistence;

namespace StayDesk.IntegrationTests;

// AUTH-1..6 and NFR-3 through the real HTTP pipeline.
public class AuthTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly HttpClient client = api.Factory.CreateClient();

    [Fact]
    public async Task Register_creates_a_guest_and_logs_them_in()
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Anna", "Keller", "anna.keller@example.de", "Sommer2026"), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/v1/auth/me", response.Headers.Location?.ToString());
        var me = await GetMeAsync(await ReadTokensAsync(response));
        Assert.Equal("anna.keller@example.de", me.Email);
        Assert.Equal([Roles.Guest], me.Roles);
    }

    [Fact]
    public async Task Register_rejects_an_email_that_differs_only_in_case()
    {
        await RegisterAsync("Moritz.Braun@example.de");

        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Moritz", "Braun", "moritz.braun@example.de", "Sommer2026"), Ct);

        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("validation_failed", problem.GetProperty("code").GetString());
        Assert.Equal("This email is already registered.", problem.GetProperty("errors").GetProperty("email")[0].GetString());
    }

    [Fact]
    public async Task Register_rejects_a_password_without_digit_or_uppercase_as_a_field_error()
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Lukas", "Hoffmann", "lukas.hoffmann@example.de", "sommerzeit"), Ct);

        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(2, problem.GetProperty("errors").GetProperty("password").GetArrayLength());
    }

    [Fact]
    public async Task Register_with_missing_fields_returns_field_errors_for_each()
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email = "not-an-email" }, Ct);

        var errors = (await ReadProblemAsync(response, HttpStatusCode.BadRequest)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("firstName", out _));
        Assert.True(errors.TryGetProperty("lastName", out _));
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Demo_admin_logs_in_with_admin_and_staff_roles()
    {
        var tokens = await LoginAsync("admin@example.com", "Admin123");

        var me = await GetMeAsync(tokens);
        Assert.Equal([Roles.Admin, Roles.Staff], me.Roles);
    }

    [Fact]
    public async Task Login_with_a_wrong_password_returns_401_invalid_credentials()
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("staff@example.com", "Wrong1234"), Ct);

        var problem = await ReadProblemAsync(response, HttpStatusCode.Unauthorized);
        Assert.Equal("invalid_credentials", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Me_without_an_access_token_returns_401()
    {
        var response = await client.GetAsync("/api/v1/auth/me", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_revokes_the_old_token_and_links_it_to_the_new_one()
    {
        var first = await RegisterAsync("jana.wolf@example.de");

        var second = await RefreshAsync(first.RefreshToken);

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        await using var scope = api.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StayDeskDbContext>();
        var userId = await db.Users.Where(u => u.Email == "jana.wolf@example.de").Select(u => u.Id).SingleAsync(Ct);
        var old = await db.RefreshTokens.SingleAsync(t => t.UserId == userId && t.ReplacedByTokenHash != null, Ct);
        Assert.NotNull(old.RevokedAt);
        Assert.True(await db.RefreshTokens.AnyAsync(t => t.TokenHash == old.ReplacedByTokenHash && t.RevokedAt == null, Ct));
        await GetMeAsync(second);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_every_token_after_it()
    {
        var first = await RegisterAsync("felix.richter@example.de");
        var second = await RefreshAsync(first.RefreshToken);
        var third = await RefreshAsync(second.RefreshToken);

        var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(first.RefreshToken), Ct);
        Assert.Equal("invalid_refresh_token",
            (await ReadProblemAsync(reuse, HttpStatusCode.Unauthorized)).GetProperty("code").GetString());

        var newest = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(third.RefreshToken), Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, newest.StatusCode);
    }

    [Fact]
    public async Task Parallel_refreshes_with_one_token_let_exactly_one_succeed()
    {
        var tokens = await RegisterAsync("clara.neumann@example.de");

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.RefreshToken), Ct)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(4, responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var tokens = await RegisterAsync("paul.zimmermann@example.de");
        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout")
        {
            Content = JsonContent.Create(new RefreshRequest(tokens.RefreshToken)),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken) },
        };

        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(logout, Ct)).StatusCode);

        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.RefreshToken), Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Five_failed_logins_lock_the_account()
    {
        await RegisterAsync("sophie.krause@example.de");
        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("sophie.krause@example.de", "Wrong1234"), Ct);
        }

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("sophie.krause@example.de", "Sommer2026"), Ct);

        var problem = await ReadProblemAsync(response, HttpStatusCode.Unauthorized);
        Assert.Equal("account_locked", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Demo_accounts_are_never_locked_out()
    {
        for (var i = 0; i < 6; i++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("guest@example.com", "Wrong1234"), Ct);
        }

        await LoginAsync("guest@example.com", "Guest123");
    }

    [Fact]
    public async Task Deactivated_user_gets_401_on_login_and_refresh()
    {
        var tokens = await RegisterAsync("max.schulz@example.de");
        await using (var scope = api.Factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = (await users.FindByEmailAsync("max.schulz@example.de"))!;
            user.IsActive = false;
            await users.UpdateAsync(user);
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("max.schulz@example.de", "Sommer2026"), Ct);
        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.RefreshToken), Ct);

        Assert.Equal("account_inactive", (await ReadProblemAsync(login, HttpStatusCode.Unauthorized)).GetProperty("code").GetString());
        Assert.Equal("account_inactive", (await ReadProblemAsync(refresh, HttpStatusCode.Unauthorized)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Eleventh_login_in_a_minute_from_one_ip_returns_429()
    {
        await using var limited = api.Factory.WithWebHostBuilder(host => host.UseSetting("RateLimiting:AuthPermitLimit", "10"));
        var limitedClient = limited.CreateClient();
        var request = new LoginRequest("staff@example.com", "Staff123");

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await limitedClient.PostAsJsonAsync("/api/v1/auth/login", request, Ct)).StatusCode);
        }

        var eleventh = await limitedClient.PostAsJsonAsync("/api/v1/auth/login", request, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, eleventh.StatusCode);
    }

    private async Task<AuthTokens> RegisterAsync(string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Test", "Gast", email, "Sommer2026"), Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadTokensAsync(response);
    }

    private async Task<AuthTokens> LoginAsync(string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadTokensAsync(response);
    }

    private async Task<AuthTokens> RefreshAsync(string refreshToken)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(refreshToken), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadTokensAsync(response);
    }

    private async Task<CurrentUser> GetMeAsync(AuthTokens tokens)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken) },
        };
        var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CurrentUser>(Ct))!;
    }

    private static async Task<AuthTokens> ReadTokensAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<AuthTokens>(Ct))!;

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }
}
