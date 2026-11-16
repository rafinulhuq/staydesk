namespace StayDesk.Application.Auth;

// Implemented in Infrastructure, where ASP.NET Identity and the DbContext live.
public interface IAuthService
{
    Task<AuthTokens> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthTokens> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<AuthTokens> RefreshAsync(RefreshRequest request, CancellationToken ct);
    Task LogoutAsync(Guid userId, RefreshRequest request, CancellationToken ct);
    Task<CurrentUser> GetCurrentUserAsync(Guid userId, CancellationToken ct);
}
