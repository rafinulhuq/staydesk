namespace StayDesk.Application.Auth;

public sealed record RegisterRequest(string FirstName, string LastName, string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);
