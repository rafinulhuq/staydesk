using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayDesk.Domain.Properties;
using StayDesk.Domain.Rooms;

namespace StayDesk.Infrastructure.Persistence.Configurations;

internal class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.Property(r => r.RoomNumber).HasMaxLength(20);
        builder.HasIndex(r => new { r.PropertyId, r.RoomNumber }).IsUnique();
        builder.ToTable(t => t.HasCheckConstraint("ck_rooms_max_guests", "max_guests BETWEEN 1 AND 10"));
        builder.Property(r => r.Version).IsRowVersion();
        builder.HasQueryFilter(r => r.IsActive);

        builder.HasOne<Property>().WithMany().HasForeignKey(r => r.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.Amenities).WithMany().UsingEntity("room_amenities");
    }
}

internal class AmenityConfiguration : IEntityTypeConfiguration<Amenity>
{
    public void Configure(EntityTypeBuilder<Amenity> builder)
    {
        builder.Property(a => a.Name).HasMaxLength(100);
        builder.Property(a => a.Icon).HasMaxLength(50);
        builder.HasIndex(a => a.Name).IsUnique();
    }
}
