using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using StayDesk.Domain.Bookings;
using StayDesk.Domain.Payments;
using StayDesk.Domain.Pricing;
using StayDesk.Domain.Properties;
using StayDesk.Domain.Rooms;

namespace StayDesk.Application.Persistence;

// Application's view of the database, implemented by the Infrastructure DbContext.
// Services query EF Core directly through it; there is no repository layer on top.
public interface IStayDeskDbContext
{
    DbSet<Property> Properties { get; }
    DbSet<Room> Rooms { get; }
    DbSet<Amenity> Amenities { get; }
    DbSet<Booking> Bookings { get; }
    DbSet<PriceRule> PriceRules { get; }
    DbSet<Payment> Payments { get; }

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    // BOOK-2 steps that need raw SQL, kept in Infrastructure so Application stays provider-neutral.
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct);
    Task LockRoomAsync(Guid roomId, CancellationToken ct);
    Task<long> NextBookingNumberAsync(CancellationToken ct);
}
