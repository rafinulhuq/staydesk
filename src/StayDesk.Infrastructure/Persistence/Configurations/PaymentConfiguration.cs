using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayDesk.Domain.Bookings;
using StayDesk.Domain.Payments;

namespace StayDesk.Infrastructure.Persistence.Configurations;

internal class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasIndex(p => p.BookingId).IsUnique();
        builder.HasOne<Booking>().WithOne().HasForeignKey<Payment>(p => p.BookingId).OnDelete(DeleteBehavior.Restrict);
    }
}
