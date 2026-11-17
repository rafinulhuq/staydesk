using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StayDesk.Domain.Bookings;
using StayDesk.Domain.Rooms;
using StayDesk.Infrastructure.Identity;

namespace StayDesk.Infrastructure.Persistence.Configurations;

internal class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    // BOOK-1: the number part of "SD-2026-000123" comes from this sequence.
    public const string ReferenceSequence = "booking_reference_seq";

    // Names the database reports on violations; StayDeskDbContext maps them to 409 and 422.
    public const string NoOverlapConstraint = "ex_bookings_no_overlap";
    public const string IdempotencyIndex = "ix_bookings_created_by_id_idempotency_key";

    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.Property(b => b.Reference).HasMaxLength(20);
        builder.HasIndex(b => b.Reference).IsUnique();
        builder.Property(b => b.GuestFullName).HasMaxLength(200);
        builder.Property(b => b.GuestEmail).HasMaxLength(256);
        builder.Property(b => b.GuestPhone).HasMaxLength(30);
        builder.Property(b => b.SpecialRequests).HasMaxLength(500);
        builder.Property(b => b.InternalNotes).HasMaxLength(2000);
        builder.Property(b => b.Version).IsRowVersion();

        builder.HasIndex(b => new { b.CreatedById, b.IdempotencyKey }).IsUnique();
        builder.HasIndex(b => new { b.RoomId, b.CheckIn, b.CheckOut, b.Status });
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_bookings_dates", "check_out > check_in");
            t.HasCheckConstraint("ck_bookings_guests", "guests BETWEEN 1 AND 10");
        });

        builder.OwnsMany(b => b.PriceBreakdown, night =>
        {
            night.ToJson();
            night.OwnsMany(n => n.Rules);
        });

        // Bookings are never deleted, so every relationship restricts deletes.
        builder.HasOne<Room>().WithMany().HasForeignKey(b => b.RoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(b => b.GuestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(b => b.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
