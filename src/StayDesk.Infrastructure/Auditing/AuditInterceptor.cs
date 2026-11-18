using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.JsonWebTokens;
using StayDesk.Domain.Auditing;
using StayDesk.Domain.Bookings;
using StayDesk.Domain.Pricing;
using StayDesk.Domain.Properties;
using StayDesk.Domain.Rooms;

namespace StayDesk.Infrastructure.Auditing;

// AUD-1/AUD-2: one AuditEntry per created or changed Booking, Room, Property and PriceRule,
// and per role given to a user, saved in the same SaveChanges (so the same transaction) as
// the change itself. Services never write audit entries by hand.
public sealed class AuditInterceptor(IHttpContextAccessor http, TimeProvider time) : SaveChangesInterceptor
{
    public const string SystemUser = "system";
    public const string AnonymousUser = "anonymous";

    private static readonly HashSet<Type> AuditedTypes = [typeof(Booking), typeof(Room), typeof(Property), typeof(PriceRule)];
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddAuditEntries(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        AddAuditEntries(eventData.Context);
        return ValueTask.FromResult(result);
    }

    // A failed save (a 409 from the exclusion constraint, a concurrency conflict) keeps its
    // entities in the change tracker. Its audit entries must not ride along with the next save
    // on the same context, e.g. the next hold in BookingService.ExpireHoldsAsync.
    public override void SaveChangesFailed(DbContextErrorEventData eventData) => DropUnsavedEntries(eventData.Context);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken ct = default)
    {
        DropUnsavedEntries(eventData.Context);
        return Task.CompletedTask;
    }

    private void AddAuditEntries(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();
        var (userId, userName) = Actor();
        var now = time.GetUtcNow();
        var correlationId = http.HttpContext?.TraceIdentifier;

        List<AuditEntry> entries = [];
        foreach (var entry in context.ChangeTracker.Entries())
        {
            var audited = entry.Entity switch
            {
                IdentityUserRole<Guid> userRole when entry.State == EntityState.Added => RoleGiven(context, userRole),
                _ when AuditedTypes.Contains(entry.Entity.GetType()) => Describe(entry),
                _ => null,
            };
            if (audited is var (entityName, entityId, action, changes))
            {
                entries.Add(new AuditEntry(userId, userName, entityName, entityId, action,
                    JsonSerializer.Serialize(changes, Json), now, correlationId));
            }
        }
        context.AddRange(entries);
    }

    // Created: every value. Updated: only fields whose value really changed, so a save that
    // touched nothing writes nothing. IsActive turning off or on is Deactivated/Reactivated.
    // The key and the xmin concurrency token are left out: neither is a change anyone made.
    private static (string, Guid, AuditAction, Dictionary<string, FieldChange>)? Describe(EntityEntry entry)
    {
        if (entry.State is not (EntityState.Added or EntityState.Modified))
        {
            return null;
        }

        var changes = new Dictionary<string, FieldChange>();
        foreach (var property in entry.Properties)
        {
            if (property.Metadata.IsPrimaryKey() || property.Metadata.IsConcurrencyToken)
            {
                continue;
            }
            if (entry.State == EntityState.Added)
            {
                changes[property.Metadata.Name] = new FieldChange(null, property.CurrentValue);
            }
            else if (property.IsModified && !property.Metadata.GetValueComparer().Equals(property.OriginalValue, property.CurrentValue))
            {
                changes[property.Metadata.Name] = new FieldChange(property.OriginalValue, property.CurrentValue);
            }
        }
        if (changes.Count == 0)
        {
            return null;
        }

        var action = entry.State == EntityState.Added
            ? AuditAction.Created
            : changes.TryGetValue("IsActive", out var isActive)
                ? isActive.New is true ? AuditAction.Reactivated : AuditAction.Deactivated
                : AuditAction.Updated;
        return (entry.Metadata.ClrType.Name, (Guid)entry.Property("Id").CurrentValue!, action, changes);
    }

    // Identity has loaded the role by name before adding the user to it, so it is tracked.
    private static (string, Guid, AuditAction, Dictionary<string, FieldChange>) RoleGiven(DbContext context, IdentityUserRole<Guid> userRole)
    {
        var role = context.Set<IdentityRole<Guid>>().Local.FirstOrDefault(r => r.Id == userRole.RoleId)?.Name
            ?? userRole.RoleId.ToString();
        return ("UserRole", userRole.UserId, AuditAction.Created, new() { ["Role"] = new FieldChange(null, role) });
    }

    // A request's user comes from the access token (sub and name claims). No HTTP request
    // means a background job or startup seeding: "system". A request without a login
    // (registration) is "anonymous".
    private (Guid?, string) Actor()
    {
        var user = http.HttpContext?.User;
        if (user is null)
        {
            return (null, SystemUser);
        }
        return Guid.TryParse(user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? (id, user.FindFirst(JwtRegisteredClaimNames.Name)?.Value ?? id.ToString())
            : (null, AnonymousUser);
    }

    private static void DropUnsavedEntries(DbContext? context)
    {
        if (context is null)
        {
            return;
        }
        foreach (var entry in context.ChangeTracker.Entries<AuditEntry>().Where(e => e.State == EntityState.Added).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private sealed record FieldChange(
        [property: JsonPropertyName("old")] object? Old,
        [property: JsonPropertyName("new")] object? New);
}
