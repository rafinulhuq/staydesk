namespace StayDesk.Application.Errors;

// Thrown by Application services for expected failures. The API turns it into an
// RFC 9457 response with the `code` extension, so services never deal with HTTP.
public sealed class ProblemException(ProblemKind kind, string code, string detail) : Exception(detail)
{
    public ProblemKind Kind { get; } = kind;
    public string Code { get; } = code;
}

public enum ProblemKind
{
    Unauthorized,
    NotFound,
    Conflict,
}
