using System.Linq.Expressions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using StayDesk.Application.Errors;
using StayDesk.Application.Persistence;
using StayDesk.Domain.Pricing;

namespace StayDesk.Application.Pricing;

// Admin CRUD for price rules. Deactivated rules are hidden by the soft-delete query filter,
// so they stop applying to quotes at once (PRICE-1 counts active rules only).
public sealed class PriceRuleService(IStayDeskDbContext db)
{
    private static readonly Expression<Func<PriceRule, PriceRuleDetail>> ToDetail = r => new PriceRuleDetail(
        r.Id, r.PropertyId, r.RoomId, r.Name, r.Type, r.PercentChange,
        r.StartDate, r.EndDate, r.MinNights, r.DaysBeforeCheckIn);

    public async Task<IReadOnlyList<PriceRuleDetail>> ListAsync(Guid propertyId, CancellationToken ct) =>
        await db.PriceRules.AsNoTracking()
            .Where(r => r.PropertyId == propertyId)
            .OrderBy(r => r.Type).ThenBy(r => r.Name)
            .Select(ToDetail)
            .ToListAsync(ct);

    public async Task<PriceRuleDetail> GetAsync(Guid id, CancellationToken ct) =>
        await db.PriceRules.AsNoTracking().Where(r => r.Id == id).Select(ToDetail).SingleOrDefaultAsync(ct)
            ?? throw NotFound();

    public async Task<PriceRuleDetail> CreateAsync(PriceRuleRequest request, CancellationToken ct)
    {
        await EnsureScopeExistsAsync(request, ct);
        var rule = new PriceRule(request.PropertyId, request.RoomId, request.Name.Trim(), request.Type,
            request.PercentChange, request.StartDate, request.EndDate, request.MinNights, request.DaysBeforeCheckIn);
        db.PriceRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return await GetAsync(rule.Id, ct);
    }

    // Last write wins: SRS §5 gives PriceRule no concurrency token, and a rule is a few
    // fields one Admin edits, not a record several people work on.
    public async Task<PriceRuleDetail> UpdateAsync(Guid id, PriceRuleRequest request, CancellationToken ct)
    {
        var rule = await db.PriceRules.SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw NotFound();
        await EnsureScopeExistsAsync(request, ct);
        rule.Update(request.PropertyId, request.RoomId, request.Name.Trim(), request.Type, request.PercentChange,
            request.StartDate, request.EndDate, request.MinNights, request.DaysBeforeCheckIn);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        var rule = await db.PriceRules.SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw NotFound();
        rule.Deactivate();
        await db.SaveChangesAsync(ct);
    }

    // Inactive properties and rooms are allowed, so Admin can prepare prices before reactivating.
    // A room rule must name a room of the same property (PRICE-3).
    private async Task EnsureScopeExistsAsync(PriceRuleRequest request, CancellationToken ct)
    {
        if (!await db.Properties.IgnoreQueryFilters().AnyAsync(p => p.Id == request.PropertyId, ct))
        {
            throw Invalid(nameof(request.PropertyId), "Property not found.");
        }
        if (request.RoomId is { } roomId
            && !await db.Rooms.IgnoreQueryFilters().AnyAsync(r => r.Id == roomId && r.PropertyId == request.PropertyId, ct))
        {
            throw Invalid(nameof(request.RoomId), "Pick a room of this property, or leave empty for all rooms.");
        }
    }

    private static ValidationException Invalid(string field, string message) =>
        new([new ValidationFailure(field, message)]);

    private static ProblemException NotFound() =>
        new(ProblemKind.NotFound, "price_rule_not_found", "Price rule not found.");
}
