using FluentValidation;
using StayDesk.Application.Paging;

namespace StayDesk.Application.Auditing;

public sealed class AuditLogRequestValidator : AbstractValidator<AuditLogRequest>
{
    public AuditLogRequestValidator()
    {
        RuleFor(r => r.Entity).MaximumLength(100);
        RuleFor(r => r.To).GreaterThanOrEqualTo(r => r.From).When(r => r.From is not null)
            .WithMessage("The end date can't be before the start date.");
        RuleFor(r => r.Page).GreaterThanOrEqualTo(1);
        RuleFor(r => r.PageSize).InclusiveBetween(1, Paged<AuditEntrySummary>.MaxPageSize);
    }
}
