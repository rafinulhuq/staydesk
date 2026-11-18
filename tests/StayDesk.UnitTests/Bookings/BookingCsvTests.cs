using System.Text;
using StayDesk.Application.Bookings;
using StayDesk.Domain.Bookings;
using StayDesk.Domain.Rooms;

namespace StayDesk.UnitTests.Bookings;

// BO-3: the CSV export opens in Excel with umlauts intact and guest input never runs as a formula.
public class BookingCsvTests
{
    private static string Row(string guestName)
    {
        var booking = new BookingSummary(Guid.CreateVersion7(), "SD-2027-000042", Guid.CreateVersion7(), "12", RoomType.Double,
            Guid.CreateVersion7(), "Hotel Spreeblick", new(2027, 3, 10), new(2027, 3, 12), 2, guestName,
            BookingStatus.Confirmed, 1234.5m, null);
        var bytes = BookingCsv.Write([booking]);
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        return Encoding.UTF8.GetString(bytes[3..]).Split("\r\n")[1];
    }

    [Fact]
    public void Plain_values_are_written_as_is_with_invariant_numbers_and_iso_dates()
    {
        Assert.Equal("SD-2027-000042,Jürgen Müller,Hotel Spreeblick,12,2027-03-10,2027-03-12,2,Confirmed,1234.50", Row("Jürgen Müller"));
    }

    [Fact]
    public void Commas_and_quotes_are_quoted_per_rfc_4180()
    {
        Assert.StartsWith("SD-2027-000042,\"Müller, Jürgen \"\"Jojo\"\"\",", Row("Müller, Jürgen \"Jojo\""));
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://example.com\")", "\"'=HYPERLINK(\"\"http://example.com\"\")\"")]
    [InlineData("+49 30 1234567", "'+49 30 1234567")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    public void A_guest_name_that_starts_like_a_formula_is_written_as_text(string guestName, string written)
    {
        Assert.StartsWith($"SD-2027-000042,{written},", Row(guestName));
    }
}
