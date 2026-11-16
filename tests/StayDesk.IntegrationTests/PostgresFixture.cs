using Microsoft.EntityFrameworkCore;
using StayDesk.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace StayDesk.IntegrationTests;

// One PostgreSQL container per test class, migrated once. Integration tests run against
// the real engine because the exclusion constraint and xmin tokens don't exist elsewhere.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16.16-alpine").Build();

    public StayDeskDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<StayDeskDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options);

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
