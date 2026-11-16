using StayDesk.Domain.Properties;

namespace StayDesk.UnitTests.Properties;

// "Today" is the calendar date at the property (SRS §2), not the server's UTC date.
public class PropertyTodayTests
{
    private static Property InZone(string timeZone) => new(
        "Hotel Spreeblick", "Berlin", "Stralauer Allee 2, 10245 Berlin", "", null,
        new TimeOnly(15, 0), new TimeOnly(11, 0), timeZone, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Late_evening_utc_is_already_tomorrow_in_berlin()
    {
        var now = new DateTimeOffset(2026, 7, 14, 22, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 7, 15), InZone("Europe/Berlin").TodayAt(now));
    }

    [Fact]
    public void Same_instant_is_still_today_in_new_york()
    {
        var now = new DateTimeOffset(2026, 7, 14, 22, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 7, 14), InZone("America/New_York").TodayAt(now));
    }

    [Fact]
    public void Berlin_winter_offset_is_one_hour()
    {
        var now = new DateTimeOffset(2026, 1, 14, 22, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 1, 14), InZone("Europe/Berlin").TodayAt(now));
    }
}
