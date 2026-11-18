using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using ValidationFailure = FluentValidation.Results.ValidationFailure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StayDesk.Application.Auth;
using StayDesk.Application.Errors;
using StayDesk.Infrastructure.Persistence;

namespace StayDesk.Infrastructure.Identity;

public sealed class AuthService(
    UserManager<User> users,
    StayDeskDbContext db,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider time) : IAuthService
{
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public async Task<AuthTokens> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var user = new User
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CreatedAt = time.GetUtcNow(),
        };

        // User and role row are written by two Identity calls; one transaction keeps a
        // half-registered user (no role) from ever existing.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            throw new ValidationException(created.Errors.Select(ToValidationFailure));
        }

        ThrowIfFailed(await users.AddToRoleAsync(user, Roles.Guest));
        var tokens = await IssueTokensAsync(user, ct);
        await transaction.CommitAsync(ct);
        return tokens;
    }

    public async Task<AuthTokens> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email)
            ?? throw InvalidCredentials();

        if (await users.IsLockedOutAsync(user))
        {
            throw new ProblemException(ProblemKind.Unauthorized, "account_locked",
                "Too many failed logins. Try again in a few minutes.");
        }

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            // AUTH-5 / D5: demo accounts never count failures, so strangers can't lock the public demo.
            if (!user.IsDemo)
            {
                await users.AccessFailedAsync(user);
            }
            throw InvalidCredentials();
        }

        // Checked after the password, so a wrong guess can't tell whether an account is deactivated.
        if (!user.IsActive)
        {
            throw AccountInactive();
        }

        await users.ResetAccessFailedCountAsync(user);
        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthTokens> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var presentedHash = Hash(request.RefreshToken);
        var presented = await db.RefreshTokens.AsNoTracking()
            .SingleOrDefaultAsync(t => t.TokenHash == presentedHash, ct);

        if (presented is null || presented.ExpiresAt <= now)
        {
            throw InvalidRefreshToken();
        }

        if (presented.RevokedAt is not null)
        {
            await RevokeChainAfterAsync(presented.Id, now, ct);
            throw InvalidRefreshToken();
        }

        var user = await users.FindByIdAsync(presented.UserId.ToString())
            ?? throw InvalidRefreshToken();
        if (!user.IsActive)
        {
            throw AccountInactive();
        }

        var (token, hash) = NewRefreshToken();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Claim the old token with a conditional update instead of read-then-write: if two
        // requests race with the same token, Postgres lets only one update match, and the
        // loser is treated as reuse (AUTH-2).
        var claimed = await db.RefreshTokens
            .Where(t => t.Id == presented.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.ReplacedByTokenHash, hash), ct);

        if (claimed == 0)
        {
            await transaction.RollbackAsync(ct);
            await RevokeChainAfterAsync(presented.Id, now, ct);
            throw InvalidRefreshToken();
        }

        var tokens = await IssueTokensAsync(user, ct, (token, hash));
        await transaction.CommitAsync(ct);
        return tokens;
    }

    public async Task LogoutAsync(Guid userId, RefreshRequest request, CancellationToken ct)
    {
        // Idempotent: an unknown, foreign or already revoked token changes nothing.
        var hash = Hash(request.RefreshToken);
        await db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, time.GetUtcNow()), ct);
    }

    public async Task<CurrentUser> GetCurrentUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString())
            ?? throw new ProblemException(ProblemKind.NotFound, "user_not_found", "User not found.");
        var roles = await users.GetRolesAsync(user);
        return new CurrentUser(user.Id, user.Email!, user.FirstName, user.LastName, [.. roles.Order()], user.IsActive);
    }

    // Reuse of a revoked token means it was stolen or replayed: revoke every token that
    // descends from it, so whoever holds the newest one is logged out too.
    private async Task RevokeChainAfterAsync(Guid tokenId, DateTimeOffset now, CancellationToken ct)
    {
        var next = await db.RefreshTokens.AsNoTracking()
            .Where(t => t.Id == tokenId)
            .Select(t => t.ReplacedByTokenHash)
            .SingleAsync(ct);

        while (next is not null)
        {
            var token = await db.RefreshTokens.SingleAsync(t => t.TokenHash == next, ct);
            token.RevokedAt ??= now;
            next = token.ReplacedByTokenHash;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<AuthTokens> IssueTokensAsync(User user, CancellationToken ct, (string Token, string Hash)? refresh = null)
    {
        var now = time.GetUtcNow();
        var (refreshToken, refreshHash) = refresh ?? NewRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            CreatedAt = now,
            ExpiresAt = now + RefreshTokenLifetime,
        });
        await db.SaveChangesAsync(ct);

        var accessExpires = now + AccessTokenLifetime;
        return new AuthTokens(await CreateAccessTokenAsync(user, now, accessExpires), accessExpires, refreshToken, now + RefreshTokenLifetime);
    }

    private async Task<string> CreateAccessTokenAsync(User user, DateTimeOffset now, DateTimeOffset expires)
    {
        var options = jwtOptions.Value;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            // The audit log records who made a change by this name (AUD-1).
            new(JwtRegisteredClaimNames.Name, $"{user.FirstName} {user.LastName}"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange((await users.GetRolesAsync(user)).Select(role => new Claim("role", role)));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)), SecurityAlgorithms.HmacSha256),
        });
    }

    // 256 random bits: unguessable, so a fast unsalted SHA-256 is enough for the stored hash.
    private static (string Token, string Hash) NewRefreshToken()
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return (token, Hash(token));
    }

    private static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static ValidationFailure ToValidationFailure(IdentityError error) => new(
        error.Code.StartsWith("Password", StringComparison.Ordinal) ? nameof(RegisterRequest.Password)
        : error.Code is "DuplicateEmail" or "DuplicateUserName" or "InvalidEmail" or "InvalidUserName" ? nameof(RegisterRequest.Email)
        : "",
        error.Code is "DuplicateEmail" or "DuplicateUserName" ? "This email is already registered." : error.Description);

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    private static ProblemException InvalidCredentials() =>
        new(ProblemKind.Unauthorized, "invalid_credentials", "Email or password is wrong.");

    private static ProblemException InvalidRefreshToken() =>
        new(ProblemKind.Unauthorized, "invalid_refresh_token", "Session expired. Log in again.");

    private static ProblemException AccountInactive() =>
        new(ProblemKind.Unauthorized, "account_inactive", "This account is deactivated.");
}
