namespace StayDesk.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";

    // HMAC-SHA256 key; at least 32 bytes. From user-secrets or Jwt__SigningKey, never from appsettings (NFR-1).
    public string SigningKey { get; set; } = "";
}
