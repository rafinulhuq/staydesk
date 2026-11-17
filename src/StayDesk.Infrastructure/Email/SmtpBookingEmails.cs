using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StayDesk.Application.Bookings;

namespace StayDesk.Infrastructure.Email;

// BOOK-7 over plain SMTP. System.Net.Mail is enough for a development catcher (Mailpit) and
// needs no extra package; a real provider would replace this class, not the interface.
public class SmtpBookingEmails(IOptions<SmtpOptions> options, ILogger<SmtpBookingEmails> logger) : IBookingEmails
{
    // Payment waits for the email, so a dead mail server must not hold the request for long.
    private const int TimeoutMilliseconds = 5000;

    public async Task SendConfirmationAsync(BookingDetail booking, CancellationToken ct)
    {
        var smtp = options.Value;
        if (string.IsNullOrWhiteSpace(smtp.Host))
        {
            logger.LogWarning("Smtp:Host is not set; confirmation for {Reference} not sent", booking.Reference);
            return;
        }

        try
        {
            using var message = new MailMessage(smtp.From, booking.GuestEmail, $"Booking {booking.Reference} confirmed", Body(booking));
            using var client = new SmtpClient(smtp.Host, smtp.Port) { Timeout = TimeoutMilliseconds };
            await client.SendMailAsync(message, ct);
            logger.LogInformation("Sent confirmation for {Reference}", booking.Reference);
        }
        // BOOK-7: the booking is already paid and saved; a failed email is logged, never an error response.
        catch (Exception e) when (e is SmtpException or FormatException)
        {
            logger.LogError(e, "Confirmation email for {Reference} failed", booking.Reference);
        }
    }

    private static string Body(BookingDetail b) => $"""
        Hello {b.GuestFullName},

        your booking {b.Reference} is confirmed.

        {b.PropertyName}, room {b.RoomNumber}
        Check-in:  {b.CheckIn:yyyy-MM-dd}
        Check-out: {b.CheckOut:yyyy-MM-dd}
        Guests:    {b.Guests}
        Total:     {b.TotalPrice:0.00} EUR

        Cancelling is free until 48 hours before check-in. After that, the first night is charged.
        """;
}
