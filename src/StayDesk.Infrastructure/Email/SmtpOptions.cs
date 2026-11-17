namespace StayDesk.Infrastructure.Email;

// Bound from the "Smtp" section (Smtp__Host, ...). Mailpit in development needs no credentials.
public class SmtpOptions
{
    public const string Section = "Smtp";

    public string? Host { get; set; }
    public int Port { get; set; } = 1025;
    public string From { get; set; } = "StayDesk <no-reply@staydesk.example>";
}
