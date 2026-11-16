namespace StayDesk.Domain.Auditing;

public enum AuditAction
{
    Created,
    Updated,
    Deactivated,
    Reactivated,
}

// Append-only (AUD-4). No foreign keys, so entries outlive the rows they describe.
public class AuditEntry
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    // Null for background jobs, which are recorded as "system".
    public Guid? UserId { get; private init; }
    public string UserDisplayName { get; private init; }
    public string EntityName { get; private init; }
    public Guid EntityId { get; private init; }
    public AuditAction Action { get; private init; }

    // JSON object: { "Field": { "old": ..., "new": ... } }
    public string Changes { get; private init; }
    public DateTimeOffset Timestamp { get; private init; }
    public string? CorrelationId { get; private init; }

    public AuditEntry(
        Guid? userId,
        string userDisplayName,
        string entityName,
        Guid entityId,
        AuditAction action,
        string changes,
        DateTimeOffset timestamp,
        string? correlationId)
    {
        UserId = userId;
        UserDisplayName = userDisplayName;
        EntityName = entityName;
        EntityId = entityId;
        Action = action;
        Changes = changes;
        Timestamp = timestamp;
        CorrelationId = correlationId;
    }
}
