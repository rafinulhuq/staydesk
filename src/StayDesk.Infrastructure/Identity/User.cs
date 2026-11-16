using Microsoft.AspNetCore.Identity;

namespace StayDesk.Infrastructure.Identity;

// Lives in Infrastructure because it inherits from Identity; the Domain refers to users by Id only.
public class User : IdentityUser<Guid>
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Demo accounts are exempt from lockout so the public demo can't be locked (AUTH-5).
    public bool IsDemo { get; set; }
    public DateTimeOffset? PseudonymisedAt { get; set; }

    public User() => Id = Guid.CreateVersion7();
}
