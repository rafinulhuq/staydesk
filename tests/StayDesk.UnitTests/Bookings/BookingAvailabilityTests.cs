using StayDesk.Domain.Bookings;

namespace StayDesk.UnitTests.Bookings;

// SRCH-3: which bookings block a room, and when two stays overlap.
public class BookingAvailabilityTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    private static Booking Pending(DateOnly checkIn, DateOnly checkOut) => new(
        "SD-2026-000001", Guid.CreateVersion7(), Guid.CreateVersion7(), checkIn, checkOut, 2,
        "Lena Schmidt", "lena.schmidt@example.de", null, null,
        [new NightPrice { Date = checkIn, BasePrice = 100.00m, Price = 100.00m }], null, CreatedAt);

    private static bool IsActiveAt(Booking booking, DateTimeOffset now) => Booking.IsActiveAt(now).Compile()(booking);

    [Fact]
    public void Pending_booking_blocks_the_room_until_its_hold_expires()
    {
        var booking = Pending(new(2026, 12, 10), new(2026, 12, 12));

        Assert.True(IsActiveAt(booking, CreatedAt + Booking.PendingHold - TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Expired_pending_booking_no_longer_blocks_the_room()
    {
        var booking = Pending(new(2026, 12, 10), new(2026, 12, 12));

        Assert.False(IsActiveAt(booking, CreatedAt + Booking.PendingHold));
        Assert.False(IsActiveAt(booking, CreatedAt + TimeSpan.FromHours(1)));
    }

    [Theory]
    [InlineData("2026-12-08", "2026-12-11", true)]  // ends inside
    [InlineData("2026-12-11", "2026-12-14", true)]  // starts inside
    [InlineData("2026-12-09", "2026-12-15", true)]  // covers
    [InlineData("2026-12-11", "2026-12-12", true)]  // inside
    [InlineData("2026-12-08", "2026-12-10", false)] // checks out on the existing check-in day
    [InlineData("2026-12-13", "2026-12-15", false)] // checks in on the existing check-out day
    public void Stays_overlap_only_when_they_share_a_night(string checkIn, string checkOut, bool overlaps)
    {
        var existing = Pending(new(2026, 12, 10), new(2026, 12, 13));

        Assert.Equal(overlaps, Booking.Overlaps(DateOnly.Parse(checkIn), DateOnly.Parse(checkOut)).Compile()(existing));
    }
}
