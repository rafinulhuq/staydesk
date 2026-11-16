using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayDesk.Domain.Auditing;

namespace StayDesk.Infrastructure.Persistence.Configurations;

internal class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.Property(a => a.UserDisplayName).HasMaxLength(200);
        builder.Property(a => a.EntityName).HasMaxLength(100);
        builder.Property(a => a.Changes).HasColumnType("jsonb");
        builder.Property(a => a.CorrelationId).HasMaxLength(100);
        builder.HasIndex(a => new { a.EntityName, a.EntityId });
        builder.HasIndex(a => a.Timestamp);
    }
}
