using StayDesk.Domain.Bookings;

namespace StayDesk.UnitTests.Bookings;

// BOOK-3/BOOK-4: the status flow enforced by the Booking entity.
public class BookingStatusTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 12, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly CheckIn = new(2026, 12, 10);
    private static readonly DateOnly CheckOut = new(2026, 12, 13);
    private static readonly DateTimeOffset BeforeExpiry = CreatedAt + TimeSpan.FromMinutes(5);
    private static readonly DateTimeOffset DuringStay = new(2026, 12, 11, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CheckInInstant = new(2026, 12, 10, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AfterExpiry = CreatedAt + TimeSpan.FromHours(1);

    public enum Action
    {
        Confirm,
        CheckIn,
        CheckOut,
        Cancel,
        NoShow,
        Expire,
    }

    private static Booking Pending() => new(
        "SD-2026-000042", Guid.CreateVersion7(), Guid.CreateVersion7(), CheckIn, CheckOut, 2,
        "Lena Schmidt", "lena.schmidt@example.com", null, null,
        [
            new NightPrice { Date = new(2026, 12, 10), BasePrice = 129.00m, Price = 129.00m },
            new NightPrice { Date = new(2026, 12, 11), BasePrice = 129.00m, Price = 148.35m },
            new NightPrice { Date = new(2026, 12, 12), BasePrice = 129.00m, Price = 148.35m },
        ],
        null, CreatedAt);

    // Reaches each status only through the entity's own transitions.
    private static Booking InStatus(BookingStatus status)
    {
        var booking = Pending();
        switch (status)
        {
            case BookingStatus.Pending:
                break;
            case BookingStatus.Confirmed:
                booking.Confirm(BeforeExpiry);
                break;
            case BookingStatus.CheckedIn:
                booking.Confirm(BeforeExpiry);
                booking.MarkCheckedIn(CheckIn, DuringStay);
                break;
            case BookingStatus.CheckedOut:
                booking.Confirm(BeforeExpiry);
                booking.MarkCheckedIn(CheckIn, DuringStay);
                booking.MarkCheckedOut(DuringStay);
                break;
            case BookingStatus.Cancelled:
                booking.Cancel(BeforeExpiry, CheckInInstant, waiveFee: false);
                break;
            case BookingStatus.NoShow:
                booking.Confirm(BeforeExpiry);
                booking.MarkNoShow(CheckIn.AddDays(1));
                break;
        }
        Assert.Equal(status, booking.Status);
        return booking;
    }

    // Dates and times that satisfy every time window, so only the status decides.
    private static void Apply(Booking booking, Action action)
    {
        var dayAfterCheckIn = CheckIn.AddDays(1);
        switch (action)
        {
            case Action.Confirm: booking.Confirm(BeforeExpiry); break;
            case Action.CheckIn: booking.MarkCheckedIn(dayAfterCheckIn, DuringStay); break;
            case Action.CheckOut: booking.MarkCheckedOut(DuringStay); break;
            case Action.Cancel: booking.Cancel(BeforeExpiry, CheckInInstant, waiveFee: false); break;
            case Action.NoShow: booking.MarkNoShow(dayAfterCheckIn); break;
            case Action.Expire: booking.Expire(AfterExpiry); break;
        }
    }

    // BOOK-3, written out as the SRS lists it.
    private static readonly Dictionary<BookingStatus, (Action Action, BookingStatus To)[]> Allowed = new()
    {
        [BookingStatus.Pending] = [(Action.Confirm, BookingStatus.Confirmed), (Action.Cancel, BookingStatus.Cancelled), (Action.Expire, BookingStatus.Cancelled)],
        [BookingStatus.Confirmed] = [(Action.CheckIn, BookingStatus.CheckedIn), (Action.Cancel, BookingStatus.Cancelled), (Action.NoShow, BookingStatus.NoShow)],
        [BookingStatus.CheckedIn] = [(Action.CheckOut, BookingStatus.CheckedOut)],
    };

    public static TheoryData<BookingStatus, Action> EveryStatusAndAction()
    {
        var data = new TheoryData<BookingStatus, Action>();
        foreach (var status in Enum.GetValues<BookingStatus>())
        {
            foreach (var action in Enum.GetValues<Action>())
            {
                data.Add(status, action);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(EveryStatusAndAction))]
    public void Only_the_transitions_in_the_status_flow_are_allowed(BookingStatus from, Action action)
    {
        var booking = InStatus(from);
        var allowed = Allowed.GetValueOrDefault(from, []).Where(t => t.Action == action).ToList();

        if (allowed.Count == 1)
        {
            Apply(booking, action);
            Assert.Equal(allowed[0].To, booking.Status);
        }
        else
        {
            Assert.Throws<InvalidStatusTransitionException>(() => Apply(booking, action));
            Assert.Equal(from, booking.Status);
        }
    }

    [Fact]
    public void Paying_records_the_confirmation_time()
    {
        var booking = Pending();

        booking.Confirm(BeforeExpiry);

        Assert.Equal(BeforeExpiry, booking.ConfirmedAt);
    }

    [Fact]
    public void Paying_at_the_moment_the_hold_expires_fails_with_booking_expired()
    {
        var booking = Pending();

        Assert.Throws<BookingExpiredException>(() => booking.Confirm(CreatedAt + Booking.PendingHold));
        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Fact]
    public void Expired_hold_is_cancelled_with_no_fee_and_no_refund()
    {
        var booking = Pending();

        booking.Expire(AfterExpiry);

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(AfterExpiry, booking.CancelledAt);
        Assert.Equal(0m, booking.CancellationFee);
        Assert.Equal(0m, booking.RefundAmount);
    }

    [Fact]
    public void A_hold_cannot_expire_before_its_15_minutes_are_up()
    {
        var booking = Pending();

        Assert.Throws<InvalidStatusTransitionException>(() => booking.Expire(CreatedAt + Booking.PendingHold - TimeSpan.FromSeconds(1)));
        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Theory]
    [InlineData("2026-12-09", false)] // day before arrival
    [InlineData("2026-12-10", true)]  // arrival day
    [InlineData("2026-12-12", true)]  // last night
    [InlineData("2026-12-13", false)] // departure day
    public void Check_in_is_allowed_from_the_arrival_day_until_the_last_night(string today, bool allowed)
    {
        var booking = InStatus(BookingStatus.Confirmed);

        if (allowed)
        {
            booking.MarkCheckedIn(DateOnly.Parse(today), DuringStay);
            Assert.Equal(DuringStay, booking.CheckedInAt);
        }
        else
        {
            Assert.Throws<InvalidStatusTransitionException>(() => booking.MarkCheckedIn(DateOnly.Parse(today), DuringStay));
            Assert.Null(booking.CheckedInAt);
        }
    }

    [Fact]
    public void Early_check_out_keeps_the_full_stored_price()
    {
        var booking = InStatus(BookingStatus.CheckedIn);

        booking.MarkCheckedOut(DuringStay);

        Assert.Equal(DuringStay, booking.CheckedOutAt);
        Assert.Equal(425.70m, booking.TotalPrice);
    }

    [Theory]
    [InlineData("2026-12-10", false)] // arrival day: the guest may still arrive
    [InlineData("2026-12-11", true)]
    public void No_show_is_allowed_from_the_day_after_check_in(string today, bool allowed)
    {
        var booking = InStatus(BookingStatus.Confirmed);

        if (allowed)
        {
            booking.MarkNoShow(DateOnly.Parse(today));
            Assert.Equal(BookingStatus.NoShow, booking.Status);
        }
        else
        {
            Assert.Throws<InvalidStatusTransitionException>(() => booking.MarkNoShow(DateOnly.Parse(today)));
        }
    }

    [Fact]
    public void No_show_is_charged_the_first_night()
    {
        var booking = InStatus(BookingStatus.Confirmed);

        booking.MarkNoShow(CheckIn.AddDays(1));

        Assert.Equal(129.00m, booking.CancellationFee);
    }
}
