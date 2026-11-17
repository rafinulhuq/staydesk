namespace StayDesk.Application.Bookings;

// BOOK-9: the guest accounts a walk-in needs. Implemented over ASP.NET Core Identity in
// Infrastructure; account creation joins the caller's open transaction.
public interface IGuestAccounts
{
    Task<bool> IsActiveGuestAsync(Guid userId, CancellationToken ct);
    Task<Guid?> FindIdByEmailAsync(string email, CancellationToken ct);
    Task<Guid> CreateWithoutPasswordAsync(NewGuest guest, DateTimeOffset now, CancellationToken ct);
}
