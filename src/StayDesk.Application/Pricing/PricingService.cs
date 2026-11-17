using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using StayDesk.Application.Errors;
using StayDesk.Application.Persistence;
using StayDesk.Domain.Pricing;
using StayDesk.Domain.Properties;
using StayDesk.Domain.Rooms;

namespace StayDesk.Application.Pricing;

public sealed class PricingService(IStayDeskDbContext db, TimeProvider time)
{
    // Public: only active rooms in active properties can be quoted (PROP-3).
    public async Task<StayQuote> QuoteAsync(QuoteRequest request, CancellationToken ct)
    {
        var priced = await PriceStayAsync(request.RoomId, request, ct);
        return ToQuote(priced.Room.Id, priced.Stay, priced.Quote);
    }

    // Also used by BookingService, which stores the domain snapshot (PRICE-5) and recomputes
    // the price on the server (PRICE-7), so a quote and a booking can't be priced differently.
    internal async Task<PricedStay> PriceStayAsync(Guid roomId, IStayDates dates, CancellationToken ct)
    {
        var (room, property) = await LoadRoomAsync(roomId, includeInactive: false, ct);
        var stay = StayAt(property, dates);
        return new PricedStay(room, property, stay, PricingEngine.Quote(room, await LoadRulesAsync(room, ct), stay));
    }

    // PRICE-8: the draft rule is built in memory and never saved. Admin may preview an
    // inactive room, e.g. before reactivating it with new rules.
    public async Task<PricePreview> PreviewAsync(PreviewRequest request, CancellationToken ct)
    {
        var (room, property) = await LoadRoomAsync(request.RoomId, includeInactive: true, ct);
        var draft = request.Rule;
        if (draft.PropertyId != room.PropertyId)
        {
            throw Invalid("Rule.PropertyId", "The rule must belong to the room's property.");
        }
        if (draft.RoomId is { } roomId && roomId != room.Id)
        {
            throw Invalid("Rule.RoomId", "Leave empty for the whole property, or use the previewed room.");
        }

        var stay = StayAt(property, request);
        var rules = await LoadRulesAsync(room, ct);
        var draftRule = new PriceRule(draft.PropertyId, draft.RoomId, draft.Name, draft.Type, draft.PercentChange,
            draft.StartDate, draft.EndDate, draft.MinNights, draft.DaysBeforeCheckIn);

        return new PricePreview(
            ToQuote(room.Id, stay, PricingEngine.Quote(room, rules, stay)),
            ToQuote(room.Id, stay, PricingEngine.Quote(room, [.. rules, draftRule], stay)));
    }

    private async Task<(Room Room, Property Property)> LoadRoomAsync(Guid roomId, bool includeInactive, CancellationToken ct)
    {
        var rooms = includeInactive ? db.Rooms.IgnoreQueryFilters() : db.Rooms;
        var room = await rooms.AsNoTracking().SingleOrDefaultAsync(r => r.Id == roomId, ct) ?? throw RoomNotFound();

        var properties = includeInactive ? db.Properties.IgnoreQueryFilters() : db.Properties;
        var property = await properties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == room.PropertyId, ct)
            ?? throw RoomNotFound();
        return (room, property);
    }

    // Inactive rules are hidden by the soft-delete query filter.
    private async Task<List<PriceRule>> LoadRulesAsync(Room room, CancellationToken ct) =>
        await db.PriceRules.AsNoTracking()
            .Where(r => r.PropertyId == room.PropertyId && (r.RoomId == null || r.RoomId == room.Id))
            .ToListAsync(ct);

    // SRCH-2: "today" is the date at the property, so a guest in another time zone can't
    // book a night that has already started there.
    private Stay StayAt(Property property, IStayDates dates)
    {
        var today = property.TodayAt(time.GetUtcNow());
        if (dates.CheckIn < today)
        {
            throw Invalid(nameof(IStayDates.CheckIn), "Check-in can't be in the past.");
        }
        return new Stay(dates.CheckIn, dates.CheckOut, today);
    }

    internal static StayQuote ToQuote(Guid roomId, Stay stay, PriceQuote quote) => new(
        roomId,
        stay.CheckIn,
        stay.CheckOut,
        [.. quote.Nights.Select(n => new QuoteNight(
            n.Date,
            n.BasePrice,
            [.. n.Rules.Select(r => new AppliedRuleSummary(r.Name, r.Type, r.PercentChange))],
            n.Price))],
        quote.Total);

    private static ValidationException Invalid(string field, string message) =>
        new([new ValidationFailure(field, message)]);

    private static ProblemException RoomNotFound() =>
        new(ProblemKind.NotFound, "room_not_found", "Room not found.");
}

internal sealed record PricedStay(Room Room, Property Property, Stay Stay, PriceQuote Quote);
