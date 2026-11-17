using FluentValidation;
using StayDesk.Application.Pricing;

namespace StayDesk.Application.Bookings;

// BOOK-1 (plus SRCH-2 dates). Guests ≤ the room's MaxGuests needs the room, so BookingService checks it.
public sealed class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        RuleFor(b => b.RoomId).NotEmpty();
        Include(new StayDatesValidator());
        RuleFor(b => b.Guests).InclusiveBetween(1, 10);
        RuleFor(b => b.GuestFullName).NotEmpty().MaximumLength(200);
        RuleFor(b => b.GuestEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(b => b.GuestPhone).MaximumLength(30);
        RuleFor(b => b.SpecialRequests).MaximumLength(500);
        RuleFor(b => b.ExpectedTotal).GreaterThan(0);
        RuleFor(b => b.GuestId).Null().When(b => b.NewGuest is not null)
            .WithMessage("Choose an existing guest or enter a new one, not both.");
        RuleFor(b => b.NewGuest).SetValidator(new NewGuestValidator());
    }
}

// Same limits as registration (AUTH-1), since this becomes a user account.
public sealed class NewGuestValidator : AbstractValidator<NewGuest?>
{
    public NewGuestValidator()
    {
        RuleFor(g => g!.FirstName).NotEmpty().MaximumLength(100); // FluentValidation skips null children
        RuleFor(g => g!.LastName).NotEmpty().MaximumLength(100);
        RuleFor(g => g!.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(g => g!.Phone).MaximumLength(30);
    }
}
