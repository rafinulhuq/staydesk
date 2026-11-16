using Serilog.Context;

namespace StayDesk.Api.Middleware;

// NFR-8: every request gets a correlation ID. A caller-supplied X-Correlation-ID is reused
// when it looks sane, so one ID can follow a request across services. Audit entries read it
// from HttpContext.TraceIdentifier.
public class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        var correlationId = IsValid(incoming) ? incoming : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    // Rejects values that could pollute logs (newlines, very long strings).
    private static bool IsValid(string value) =>
        value.Length is > 0 and <= MaxLength && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
