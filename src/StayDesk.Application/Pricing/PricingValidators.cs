using FluentValidation;
using StayDesk.Domain.Pricing;

namespace StayDesk.Application.Pricing;

// SRCH-2. "Check-in is today or later" needs the property's time zone, so PricingService checks it.
public sealed class StayDatesValidator : AbstractValidator<IStayDates>
{
    public const int MaxNights = 30;

    public StayDatesValidator()
    {
        RuleFor(s => s.CheckOut)
            .GreaterThan(s => s.CheckIn).WithMessage("Check-out must be after check-in.")
            .Must((s, checkOut) => checkOut.DayNumber - s.CheckIn.DayNumber <= MaxNights)
            .WithMessage($"A stay is at most {MaxNights} nights.");
    }
}

public sealed class QuoteRequestValidator : AbstractValidator<QuoteRequest>
{
    public QuoteRequestValidator()
    {
        RuleFor(q => q.RoomId).NotEmpty();
        Include(new StayDatesValidator());
    }
}

// PRICE-2. Each type takes only its own fields, so a stored rule never carries a value
// that looks meaningful but is ignored (e.g. dates on a Weekend rule).
public sealed class PriceRuleRequestValidator : AbstractValidator<PriceRuleRequest>
{
    public PriceRuleRequestValidator()
    {
        RuleFor(r => r.PropertyId).NotEmpty();
        RuleFor(r => r.Name).NotEmpty().MaximumLength(100);
        RuleFor(r => r.Type).IsInEnum();
        RuleFor(r => r.PercentChange).InclusiveBetween(-90, 300).PrecisionScale(6, 2, ignoreTrailingZeros: true);

        When(r => r.Type == PriceRuleType.Seasonal, () =>
        {
            RuleFor(r => r.StartDate).NotNull();
            RuleFor(r => r.EndDate).NotNull()
                .GreaterThanOrEqualTo(r => r.StartDate).WithMessage("End date can't be before the start date.");
        }).Otherwise(() =>
        {
            RuleFor(r => r.StartDate).Null().WithMessage("Only seasonal rules have dates.");
            RuleFor(r => r.EndDate).Null().WithMessage("Only seasonal rules have dates.");
        });

        // Above the 30-night stay limit the rule could never match.
        When(r => r.Type == PriceRuleType.LengthOfStay, () =>
            RuleFor(r => r.MinNights).NotNull().InclusiveBetween(1, StayDatesValidator.MaxNights))
            .Otherwise(() => RuleFor(r => r.MinNights).Null().WithMessage("Only length-of-stay rules have a minimum stay."));

        When(r => r.Type == PriceRuleType.LastMinute, () =>
            RuleFor(r => r.DaysBeforeCheckIn).NotNull().GreaterThanOrEqualTo(0))
            .Otherwise(() => RuleFor(r => r.DaysBeforeCheckIn).Null().WithMessage("Only last-minute rules have days before check-in."));
    }
}

public sealed class PreviewRequestValidator : AbstractValidator<PreviewRequest>
{
    public PreviewRequestValidator()
    {
        RuleFor(p => p.RoomId).NotEmpty();
        Include(new StayDatesValidator());
        RuleFor(p => p.Rule).NotNull().SetValidator(new PriceRuleRequestValidator());
    }
}
