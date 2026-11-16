using FluentValidation;

namespace StayDesk.Application.Validation;

public static class ValidationRules
{
    public static IRuleBuilderOptions<T, string?> HttpUrl<T>(this IRuleBuilder<T, string?> rule) => rule
        .MaximumLength(1000)
        .Must(url => url is null
            || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)))
        .WithMessage("Must be an absolute http or https URL.");
}
