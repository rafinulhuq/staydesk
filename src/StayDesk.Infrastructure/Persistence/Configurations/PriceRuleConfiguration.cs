using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayDesk.Domain.Pricing;
using StayDesk.Domain.Properties;
using StayDesk.Domain.Rooms;

namespace StayDesk.Infrastructure.Persistence.Configurations;

internal class PriceRuleConfiguration : IEntityTypeConfiguration<PriceRule>
{
    public void Configure(EntityTypeBuilder<PriceRule> builder)
    {
        builder.Property(r => r.Name).HasMaxLength(100);
        builder.Property(r => r.PercentChange).HasPrecision(6, 2);
        builder.ToTable(t => t.HasCheckConstraint("ck_price_rules_percent", "percent_change BETWEEN -90 AND 300"));
        builder.HasQueryFilter(r => r.IsActive);

        builder.HasOne<Property>().WithMany().HasForeignKey(r => r.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Room>().WithMany().HasForeignKey(r => r.RoomId).OnDelete(DeleteBehavior.Restrict);
    }
}
