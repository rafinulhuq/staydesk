using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StayDesk.Application.Auth;
using StayDesk.Application.Bookings;
using StayDesk.Infrastructure.Persistence;

namespace StayDesk.Infrastructure.Identity;

// UserManager shares the scoped StayDeskDbContext, so its saves run inside the booking
// transaction that BookingService opened.
public class GuestAccounts(UserManager<User> users, StayDeskDbContext db) : IGuestAccounts
{
    public async Task<bool> IsActiveGuestAsync(Guid userId, CancellationToken ct) =>
        await users.FindByIdAsync(userId.ToString()) is { IsActive: true } user && await users.IsInRoleAsync(user, Roles.Guest);

    public async Task<Guid?> FindIdByEmailAsync(string email, CancellationToken ct) =>
        (await users.FindByEmailAsync(email.Trim()))?.Id;

    // No password: the account can't log in until one is set, which is out of scope (BOOK-9).
    public async Task<Guid> CreateWithoutPasswordAsync(NewGuest guest, DateTimeOffset now, CancellationToken ct)
    {
        var email = guest.Email.Trim();
        var user = new User
        {
            UserName = email,
            Email = email,
            FirstName = guest.FirstName.Trim(),
            LastName = guest.LastName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(guest.Phone) ? null : guest.Phone.Trim(),
            CreatedAt = now,
        };
        Check(await users.CreateAsync(user));
        Check(await users.AddToRoleAsync(user, Roles.Guest));
        return user.Id;
    }

    // UserManager has no role filter that stays in SQL, so the role check is a subquery on the
    // Identity tables. Accounts always get an email (registration and walk-ins require one).
    public async Task<IReadOnlyList<GuestSummary>> SearchAsync(string search, CancellationToken ct)
    {
        var term = search.Trim().ToLower();
        return await db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.PseudonymisedAt == null)
            .Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Guest)))
            .Where(u => (u.FirstName + " " + u.LastName).ToLower().Contains(term) || (u.Email != null && u.Email.ToLower().Contains(term)))
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Email)
            .Take(GuestSearchRequest.MaxResults)
            .Select(u => new GuestSummary(u.Id, u.FirstName, u.LastName, u.Email ?? "", u.PhoneNumber))
            .ToListAsync(ct);
    }

    // Identity reports problems such as a duplicate email (two walk-ins racing) as results, not exceptions.
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(e => new ValidationFailure("NewGuest.Email", e.Description)));
        }
    }
}
