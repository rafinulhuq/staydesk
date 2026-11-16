using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayDesk.Application.Errors;

namespace StayDesk.Api.Errors;

// Maps expected failures to RFC 9457 responses with a `code` extension. Anything else
// falls through to the default handler: a plain 500 without exception details.
public sealed class ProblemExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        ProblemDetails? problem = exception switch
        {
            ValidationException validation => ValidationProblem(validation),
            ProblemException known => new ProblemDetails
            {
                Status = known.Kind switch
                {
                    ProblemKind.Unauthorized => StatusCodes.Status401Unauthorized,
                    ProblemKind.NotFound => StatusCodes.Status404NotFound,
                    ProblemKind.Conflict => StatusCodes.Status409Conflict,
                    _ => throw new ArgumentOutOfRangeException(nameof(exception), known.Kind, null),
                },
                Detail = known.Message,
                Extensions = { ["code"] = known.Code },
            },
            // Raised when a row's xmin no longer matches the version the client edited.
            DbUpdateConcurrencyException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Detail = "Someone else changed this record. Reload and try again.",
                Extensions = { ["code"] = "concurrency_conflict" },
            },
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ValidationProblemDetails ValidationProblem(ValidationException exception) =>
        new(exception.Errors
            .GroupBy(e => JsonFieldName(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
        {
            Status = StatusCodes.Status400BadRequest,
            Extensions = { ["code"] = "validation_failed" },
        };

    // Field names match the JSON the Angular form sent (camelCase), so it can show each
    // message next to its field. Nested names are camelCased per segment: "Rule.PercentChange"
    // becomes "rule.percentChange".
    private static string JsonFieldName(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(segment =>
            segment.Length == 0 ? "" : char.ToLowerInvariant(segment[0]) + segment[1..]));
}
