using System.Text.Json;
using StayDesk.Domain.Auditing;

namespace StayDesk.Application.Auditing;

// Changes is the stored JSON object { "Field": { "old": ..., "new": ... } }, passed through as JSON.
public sealed record AuditEntrySummary(
    Guid Id,
    DateTimeOffset Timestamp,
    Guid? UserId,
    string UserDisplayName,
    string EntityName,
    Guid EntityId,
    AuditAction Action,
    JsonElement Changes,
    string? CorrelationId);

// AUD-3, bound from the query string. From/To are UTC calendar days, both included.
public sealed class AuditLogRequest
{
    public string? Entity { get; init; }
    public Guid? EntityId { get; init; }
    public Guid? UserId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
