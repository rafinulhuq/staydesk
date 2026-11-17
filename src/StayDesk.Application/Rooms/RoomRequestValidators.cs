using FluentValidation;
using StayDesk.Application.Paging;
using StayDesk.Application.Pricing;
using StayDesk.Application.Validation;

namespace StayDesk.Application.Rooms;

public sealed class RoomFieldsValidator : AbstractValidator<IRoomFields>
{
    public RoomFieldsValidator()
    {
        RuleFor(r => r.RoomNumber).NotEmpty().MaximumLength(20);
        RuleFor(r => r.Type).IsInEnum();
        RuleFor(r => r.MaxGuests).InclusiveBetween(1, 10);
        RuleFor(r => r.BasePrice).GreaterThan(0).PrecisionScale(10, 2, ignoreTrailingZeros: true);
        RuleFor(r => r.ImageUrls).NotNull().Must(urls => urls is null || urls.Count <= 10).WithMessage("At most 10 images.");
        RuleForEach(r => r.ImageUrls).HttpUrl();
        RuleFor(r => r.AmenityIds).NotNull();
    }
}

public sealed class CreateRoomRequestValidator : AbstractValidator<CreateRoomRequest>
{
    public CreateRoomRequestValidator()
    {
        RuleFor(r => r.PropertyId).NotEmpty();
        Include(new RoomFieldsValidator());
    }
}

public sealed class UpdateRoomRequestValidator : AbstractValidator<UpdateRoomRequest>
{
    public UpdateRoomRequestValidator()
    {
        Include(new RoomFieldsValidator());
    }
}

// SRCH-2 and SRCH-4. "Check-in is today or later" needs each property's time zone, so
// RoomSearchService checks it.
public sealed class RoomSearchRequestValidator : AbstractValidator<RoomSearchRequest>
{
    public RoomSearchRequestValidator()
    {
        RuleFor(s => s.City).NotEmpty().MaximumLength(100);
        Include(new StayDatesValidator());
        RuleFor(s => s.Guests).InclusiveBetween(1, 10);
        RuleFor(s => s.Type).IsInEnum();
        RuleFor(s => s.MaxPrice).GreaterThan(0);
        RuleFor(s => s.Sort).IsInEnum();
        RuleFor(s => s.Dir).IsInEnum();
        RuleFor(s => s.Page).GreaterThanOrEqualTo(1);
        RuleFor(s => s.PageSize).InclusiveBetween(1, Paged<RoomSearchResult>.MaxPageSize);
    }
}
