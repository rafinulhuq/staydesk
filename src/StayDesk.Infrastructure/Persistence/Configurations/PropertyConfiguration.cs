using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayDesk.Domain.Properties;

namespace StayDesk.Infrastructure.Persistence.Configurations;

internal class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(200);
        builder.Property(p => p.City).HasMaxLength(100);
        builder.Property(p => p.Address).HasMaxLength(300);
        builder.Property(p => p.Description).HasMaxLength(4000);
        builder.Property(p => p.ImageUrl).HasMaxLength(1000);
        builder.Property(p => p.TimeZone).HasMaxLength(64);
        builder.HasIndex(p => p.City);
        builder.HasQueryFilter(p => p.IsActive);
    }
}
