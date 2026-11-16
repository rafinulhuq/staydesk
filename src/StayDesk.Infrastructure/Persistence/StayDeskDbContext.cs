using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StayDesk.Application.Persistence;
using StayDesk.Domain.Auditing;
using StayDesk.Domain.Bookings;
using StayDesk.Domain.Payments;
using StayDesk.Domain.Pricing;
using StayDesk.Domain.Properties;
using StayDesk.Domain.Rooms;
using StayDesk.Infrastructure.Identity;
using StayDesk.Infrastructure.Persistence.Configurations;

namespace StayDesk.Infrastructure.Persistence;

public class StayDeskDbContext(DbContextOptions<StayDeskDbContext> options)
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options), IStayDeskDbContext
{
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<PriceRule> PriceRules => Set<PriceRule>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasPostgresExtension("btree_gist");
        builder.HasSequence<long>(BookingConfiguration.ReferenceSequence);
        builder.ApplyConfigurationsFromAssembly(typeof(StayDeskDbContext).Assembly);

        // Identity names its tables explicitly ("AspNetUsers"), which the snake_case convention
        // doesn't override. Renaming keeps one naming style across the schema.
        builder.Entity<User>().ToTable("users");
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Money is numeric(10,2) everywhere; enums are stored by name so the data stays readable.
        configurationBuilder.Properties<decimal>().HavePrecision(10, 2);
        configurationBuilder.Properties<Enum>().HaveConversion<string>();
    }
}
