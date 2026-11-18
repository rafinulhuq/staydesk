using System.Globalization;
using System.Text;

namespace StayDesk.Application.Bookings;

// BO-3: UTF-8 with a BOM, so Excel detects the encoding and keeps umlauts. RFC 4180 quoting,
// comma-separated, invariant numbers and ISO dates.
public static class BookingCsv
{
    private static readonly string[] Header =
        ["Reference", "Guest", "Property", "Room", "Check-in", "Check-out", "Guests", "Status", "Total (EUR)"];

    public static byte[] Write(IEnumerable<BookingSummary> bookings)
    {
        var csv = new StringBuilder();
        AppendRow(csv, Header);
        foreach (var b in bookings)
        {
            AppendRow(csv, [
                b.Reference, b.GuestFullName, b.PropertyName, b.RoomNumber,
                b.CheckIn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                b.CheckOut.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                b.Guests.ToString(CultureInfo.InvariantCulture), b.Status.ToString(),
                b.TotalPrice.ToString("0.00", CultureInfo.InvariantCulture)]);
        }
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    private static void AppendRow(StringBuilder csv, IEnumerable<string> fields) =>
        csv.AppendJoin(',', fields.Select(Field)).Append("\r\n");

    // Guest names are typed by guests. Excel runs a cell that starts with = + - @ as a formula,
    // so such a value gets a leading apostrophe and opens as text (OWASP "CSV injection").
    private static string Field(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0]))
        {
            value = "'" + value;
        }
        return value.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
