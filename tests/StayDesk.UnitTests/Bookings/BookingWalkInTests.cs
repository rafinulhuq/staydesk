using StayDesk.Domain.Bookings;

namespace StayDesk.UnitTests.Bookings;

// BOOK-9: walk-ins skip Pending and record the staff member who created them.
public class BookingWalkInTests
{
    private static readonly DateTimeOffset Now = new(2026, 12, 10, 16, 30, 0, TimeSpan.Zero);
    private static readonly Guid GuestId = Guid.CreateVersion7();
    private static readonly Guid StaffId = Guid.CreateVersion7();

    private static Booking WalkIn() => Booking.WalkIn(
        "SD-2026-000090", Guid.CreateVersion7(), GuestId, StaffId, new(2026, 12, 10), new(2026, 12, 12), 2,
        "Max Hoffmann", "max.hoffmann@example.com", null, null,
        [
            new NightPrice { Date = new(2026, 12, 10), BasePrice = 129.00m, Price = 129.00m },
            new NightPrice { Date = new(2026, 12, 11), BasePrice = 129.00m, Price = 129.00m },
        ],
        Guid.NewGuid(), Now);

    [Fact]
    public void Walk_in_is_confirmed_at_creation_and_never_expires()
    {
        var booking = WalkIn();

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(Now, booking.ConfirmedAt);
        Assert.Null(booking.ExpiresAt);
        Assert.True(Booking.IsActiveAt(Now.AddDays(1)).Compile()(booking));
    }

    [Fact]
    public void Walk_in_records_the_staff_member_as_creator()
    {
        var booking = WalkIn();

        Assert.Equal(GuestId, booking.GuestId);
        Assert.Equal(StaffId, booking.CreatedById);
    }

    [Fact]
    public void Walk_in_guest_can_check_in_on_the_same_day()
    {
        var booking = WalkIn();

        booking.MarkCheckedIn(new(2026, 12, 10), Now.AddMinutes(5));

        Assert.Equal(BookingStatus.CheckedIn, booking.Status);
    }
}
