using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StayDesk.Infrastructure.Identity;
using StayDesk.Infrastructure.Persistence;
using StayDesk.Infrastructure.Persistence.Seeding;

namespace StayDesk.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default is not set. Use user-secrets or the ConnectionStrings__Default environment variable.");

        services.AddDbContext<StayDeskDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        // AddIdentityCore registers users, roles and password hashing without cookie auth;
        // the API authenticates with JWTs instead (AUTH-2).
        services.AddIdentityCore<User>(options =>
            {
                // AUTH-1: 8+ characters, one digit, one uppercase letter.
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;

                // AUTH-5: lock for 5 minutes after 5 failed logins.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<StayDeskDbContext>();

        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.Section));
        services.AddScoped<DbSeeder>();

        return services;
    }
}
