using FluentValidation;
using StayDesk.Application.Validation;

namespace StayDesk.Application.Properties;

public sealed class PropertyRequestValidator : AbstractValidator<PropertyRequest>
{
    public PropertyRequestValidator()
    {
        RuleFor(p => p.Name).NotEmpty().MaximumLength(200);
        RuleFor(p => p.City).NotEmpty().MaximumLength(100);
        RuleFor(p => p.Address).NotEmpty().MaximumLength(300);
        RuleFor(p => p.Description).NotNull().MaximumLength(4000);
        RuleFor(p => p.ImageUrl).HttpUrl();

        // IANA ids only (SRS §5): the server also accepts Windows ids, which would break the
        // check-in instant on any other platform.
        RuleFor(p => p.TimeZone).NotEmpty()
            .Must(id => TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) && zone.HasIanaId)
            .WithMessage("Must be an IANA time zone such as Europe/Berlin.");
    }
}
