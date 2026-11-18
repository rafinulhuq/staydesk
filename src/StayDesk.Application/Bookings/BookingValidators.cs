using FluentValidation;
using StayDesk.Application.Paging;
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

// BO-1 and BO-3.
public sealed class BookingListRequestValidator : AbstractValidator<BookingListRequest>
{
    public BookingListRequestValidator()
    {
        RuleFor(r => r.Status).IsInEnum();
        RuleFor(r => r.To).GreaterThanOrEqualTo(r => r.From).When(r => r.From is not null)
            .WithMessage("The end date can't be before the start date.");
        RuleFor(r => r.Search).MaximumLength(100);
        RuleFor(r => r.Sort).IsInEnum();
        RuleFor(r => r.Dir).IsInEnum();
        RuleFor(r => r.Page).GreaterThanOrEqualTo(1);
        RuleFor(r => r.PageSize).InclusiveBetween(1, Paged<BookingSummary>.MaxPageSize);
    }
}

// Two characters at least, so one keystroke doesn't list every guest.
public sealed class GuestSearchRequestValidator : AbstractValidator<GuestSearchRequest>
{
    public GuestSearchRequestValidator()
    {
        RuleFor(r => r.Search).NotEmpty().Must(s => s.Trim().Length >= 2)
            .WithMessage("Type at least 2 characters.").MaximumLength(100);
    }
}

// BO-2: max 2,000 characters, the column length.
public sealed class UpdateNotesRequestValidator : AbstractValidator<UpdateNotesRequest>
{
    public UpdateNotesRequestValidator()
    {
        RuleFor(r => r.InternalNotes).MaximumLength(2000);
    }
}
