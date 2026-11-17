using StayDesk.Domain.Bookings;
using StayDesk.Domain.Properties;

namespace StayDesk.UnitTests.Bookings;

// BOOK-5: free until 48 h before the check-in instant, then the first night is kept.
public class BookingCancellationTests
{
    // Berlin leaves summer time on 25 Oct 2026, so the 48 h before a 26 Oct check-in span the change.
    private static readonly Property Berlin = new(
        "Hotel Spreeblick", "Berlin", "Am Kupfergraben 4, 10117 Berlin", "", null,
        new TimeOnly(15, 0), new TimeOnly(11, 0), "Europe/Berlin", DateTimeOffset.UnixEpoch);
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    // 15:00 in Berlin on 26 Oct is winter time (UTC+1).
    private static readonly DateTimeOffset CheckInInstant = new(2026, 10, 26, 14, 0, 0, TimeSpan.Zero);

    private static Booking Booking(bool paid = true)
    {
        var booking = new Booking(
            "SD-2026-000077", Guid.CreateVersion7(), Guid.CreateVersion7(), new(2026, 10, 26), new(2026, 10, 28), 2,
            "Lena Schmidt", "lena.schmidt@example.com", null, null,
            [
                new NightPrice { Date = new(2026, 10, 26), BasePrice = 100.00m, Price = 100.00m },
                new NightPrice { Date = new(2026, 10, 27), BasePrice = 100.00m, Price = 120.00m },
            ],
            null, CreatedAt);
        if (paid)
        {
            booking.Confirm(CreatedAt.AddMinutes(5));
        }
        return booking;
    }

    [Fact]
    public void Check_in_instant_uses_the_property_time_and_zone()
    {
        Assert.Equal(CheckInInstant, Berlin.InstantAt(new(2026, 10, 26), Berlin.CheckInTime));
    }

    [Theory]
    [InlineData(-1, 0, 220)]  // 48 h 1 min before: free
    [InlineData(0, 0, 220)]   // exactly 48 h before: still free
    [InlineData(1, 100, 120)] // 47 h 59 min before: first night kept
    public void Cancelling_is_free_until_exactly_48_hours_before_the_check_in_instant(int minutesAfterDeadline, decimal fee, decimal refund)
    {
        var now = CheckInInstant - TimeSpan.FromHours(48) + TimeSpan.FromMinutes(minutesAfterDeadline);

        var terms = Booking().CancellationTermsAt(now, CheckInInstant, waiveFee: false);

        Assert.Equal(220.00m, terms.AmountPaid);
        Assert.Equal(fee, terms.Fee);
        Assert.Equal(refund, terms.Refund);
    }

    // 48 real hours before 15:00 winter time is 16:00 summer time on 24 Oct, not 15:00.
    // Counting wall-clock hours would start charging an hour too early.
    [Fact]
    public void The_48_hours_are_real_hours_across_the_summer_time_change()
    {
        var terms = Booking().CancellationTermsAt(new(2026, 10, 24, 13, 30, 0, TimeSpan.Zero), CheckInInstant, waiveFee: false);

        Assert.Equal(new DateTimeOffset(2026, 10, 24, 14, 0, 0, TimeSpan.Zero), terms.FreeUntil);
        Assert.Equal(0m, terms.Fee);
    }

    [Fact]
    public void An_unpaid_pending_booking_costs_nothing_and_refunds_nothing()
    {
        var terms = Booking(paid: false).CancellationTermsAt(CreatedAt.AddMinutes(10), CreatedAt.AddHours(1), waiveFee: false);

        Assert.Equal((0m, 0m, 0m), (terms.AmountPaid, terms.Fee, terms.Refund));
    }

    [Fact]
    public void Staff_can_waive_the_fee_inside_the_48_hours()
    {
        var booking = Booking();
        var now = CheckInInstant - TimeSpan.FromHours(2);

        booking.Cancel(now, CheckInInstant, waiveFee: true);

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(0m, booking.CancellationFee);
        Assert.Equal(220.00m, booking.RefundAmount);
        Assert.True(booking.FeeWaived);
        Assert.Equal(now, booking.CancelledAt);
    }

    [Fact]
    public void A_late_cancellation_stores_the_fee_and_refund()
    {
        var booking = Booking();

        booking.Cancel(CheckInInstant - TimeSpan.FromHours(10), CheckInInstant, waiveFee: false);

        Assert.Equal(100.00m, booking.CancellationFee);
        Assert.Equal(120.00m, booking.RefundAmount);
        Assert.False(booking.FeeWaived);
    }
}
