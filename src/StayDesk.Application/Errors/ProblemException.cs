namespace StayDesk.Application.Errors;

// Thrown by Application services for expected failures. The API turns it into an
// RFC 9457 response with the `code` extension, so services never deal with HTTP.
public sealed class ProblemException(
    ProblemKind kind,
    string code,
    string detail,
    IReadOnlyDictionary<string, object?>? extensions = null) : Exception(detail)
{
    public ProblemKind Kind { get; } = kind;
    public string Code { get; } = code;

    // Extra response members the client acts on, e.g. the new quote with price_changed (PRICE-7).
    public IReadOnlyDictionary<string, object?> Extensions { get; } = extensions ?? new Dictionary<string, object?>();
}

public enum ProblemKind
{
    Unauthorized,
    NotFound,
    Conflict,
    Unprocessable,
}
