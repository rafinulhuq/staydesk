using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using StayDesk.Application.Errors;
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

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct) => Database.BeginTransactionAsync(ct);

    // BOOK-2: holds the room row until the transaction ends, so booking attempts for one room
    // run one after another and each sees the bookings the previous one committed.
    public async Task LockRoomAsync(Guid roomId, CancellationToken ct) =>
        await Database.ExecuteSqlAsync($"SELECT 1 FROM rooms WHERE id = {roomId} FOR UPDATE", ct);

    // BOOK-1: the number part of the booking reference.
    public async Task<long> NextBookingNumberAsync(CancellationToken ct) =>
        await Database.SqlQueryRaw<long>("SELECT nextval('" + BookingConfiguration.ReferenceSequence + "') AS \"Value\"")
            .SingleAsync(ct);

    // The constraints are the last line of defence behind the room lock (BOOK-2) and the
    // idempotency lookup (BOOK-8). Their violations are expected outcomes, not server errors.
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation, ConstraintName: BookingConfiguration.NoOverlapConstraint })
        {
            throw new ProblemException(ProblemKind.Conflict, "room_unavailable", "The room is no longer available for these dates.");
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: BookingConfiguration.IdempotencyIndex })
        {
            throw new ProblemException(ProblemKind.Unprocessable, "idempotency_key_reused", "This Idempotency-Key was already used for a different booking.");
        }
    }

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
