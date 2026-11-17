using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using StayDesk.Application.Auth;
using StayDesk.Application.Bookings;

namespace StayDesk.Infrastructure.Identity;

// UserManager shares the scoped StayDeskDbContext, so its saves run inside the booking
// transaction that BookingService opened.
public class GuestAccounts(UserManager<User> users) : IGuestAccounts
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

    // Identity reports problems such as a duplicate email (two walk-ins racing) as results, not exceptions.
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(e => new ValidationFailure("NewGuest.Email", e.Description)));
        }
    }
}
