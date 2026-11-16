using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StayDesk.Application.Auth;

namespace StayDesk.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<AuthTokens>> Register(RegisterRequest request, CancellationToken ct) =>
        Created("/api/v1/auth/me", await auth.RegisterAsync(request, ct));

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<AuthTokens> Login(LoginRequest request, CancellationToken ct) =>
        await auth.LoginAsync(request, ct);

    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<AuthTokens> Refresh(RefreshRequest request, CancellationToken ct) =>
        await auth.RefreshAsync(request, ct);

    [Authorize]
    [HttpPost("logout")]
    public async Task<NoContentResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(CurrentUserId, request, ct);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<CurrentUser> Me(CancellationToken ct) =>
        await auth.GetCurrentUserAsync(CurrentUserId, ct);

    // [Authorize] guarantees an authenticated principal, and every access token is issued with `sub`.
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue("sub")!);
}
