using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using StayDesk.Application.Auth;

namespace StayDesk.IntegrationTests;

// The real API (Program.cs) in-process, against its own PostgreSQL container, in the
// Development environment so startup migrates and seeds exactly as it does locally.
public sealed class ApiFixture : IAsyncLifetime
{
    public PostgresFixture Database { get; } = new();
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await Database.InitializeAsync();
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host => host
            .UseEnvironment("Development")
            .UseSetting("ConnectionStrings:Default", Database.ConnectionString)
            .UseSetting("Seed:DemoData", "true")
            .UseSetting("Seed:AdminPassword", "Admin123")
            .UseSetting("Seed:StaffPassword", "Staff123")
            .UseSetting("Seed:GuestPassword", "Guest123")
            .UseSetting("Jwt:SigningKey", "integration-tests-signing-key-0123456789abcdef")
            .UseSetting("RateLimiting:AuthPermitLimit", "1000"));

        // The factory starts lazily on first use; start it now so every test sees a seeded database.
        _ = Factory.Server;
    }

    // Matches the API's JSON: camelCase and enums as strings.
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public async Task<HttpClient> CreateClientAsync(string email, string password)
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var tokens = await response.Content.ReadFromJsonAsync<AuthTokens>(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Database.DisposeAsync();
    }
}
