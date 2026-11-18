using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StayDesk.Application.Errors;
using StayDesk.Application.Paging;
using StayDesk.Application.Persistence;
using StayDesk.Domain.Auditing;
using StayDesk.Domain.Bookings;

namespace StayDesk.Application.Auditing;

// Reads only: entries are written by the EF interceptor and never changed or deleted (AUD-4).
public sealed class AuditService(IStayDeskDbContext db)
{
    // AUD-3: the Admin audit log, newest first. GUID v7 ids sort by creation time, so they
    // break ties between entries written in the same save.
    public async Task<Paged<AuditEntrySummary>> ListAsync(AuditLogRequest request, CancellationToken ct)
    {
        var entries = db.AuditEntries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Entity))
        {
            entries = entries.Where(a => a.EntityName == request.Entity);
        }
        if (request.EntityId is { } entityId)
        {
            entries = entries.Where(a => a.EntityId == entityId);
        }
        if (request.UserId is { } userId)
        {
            entries = entries.Where(a => a.UserId == userId);
        }
        if (request.From is { } from)
        {
            var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            entries = entries.Where(a => a.Timestamp >= start);
        }
        if (request.To is { } to)
        {
            var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            entries = entries.Where(a => a.Timestamp < end);
        }

        var total = await entries.CountAsync(ct);
        var page = await entries
            .OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);
        return new Paged<AuditEntrySummary>([.. page.Select(ToSummary)], request.Page, request.PageSize, total);
    }

    // BO-4: one booking's history for Staff, oldest first, so it reads as a timeline.
    public async Task<IReadOnlyList<AuditEntrySummary>> BookingHistoryAsync(Guid bookingId, CancellationToken ct)
    {
        if (!await db.Bookings.AnyAsync(b => b.Id == bookingId, ct))
        {
            throw new ProblemException(ProblemKind.NotFound, "booking_not_found", "Booking not found.");
        }
        var entries = await db.AuditEntries.AsNoTracking()
            .Where(a => a.EntityName == nameof(Booking) && a.EntityId == bookingId)
            .OrderBy(a => a.Timestamp).ThenBy(a => a.Id)
            .ToListAsync(ct);
        return [.. entries.Select(ToSummary)];
    }

    // The JSON is parsed after the query: Changes is stored as text in a jsonb column.
    private static AuditEntrySummary ToSummary(AuditEntry entry)
    {
        using var changes = JsonDocument.Parse(entry.Changes);
        return new AuditEntrySummary(entry.Id, entry.Timestamp, entry.UserId, entry.UserDisplayName, entry.EntityName,
            entry.EntityId, entry.Action, changes.RootElement.Clone(), entry.CorrelationId);
    }
}
