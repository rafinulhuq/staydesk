using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayDesk.Application.Auth;
using StayDesk.Application.Bookings;

namespace StayDesk.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/bookings")]
public sealed class BookingsController(BookingService bookings) : ControllerBase
{
    // 201 for a new booking, 200 when an Idempotency-Key retry returns the original (BOOK-8).
    // Staff (and Admin, who also holds Staff) create walk-ins (BOOK-9).
    [Authorize(Roles = $"{Roles.Guest},{Roles.Staff}")]
    [HttpPost]
    public async Task<ActionResult<BookingDetail>> Create(
        CreateBookingRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid? idempotencyKey,
        CancellationToken ct)
    {
        var (booking, created) = await bookings.CreateAsync(CurrentUserId, User.IsInRole(Roles.Staff), idempotencyKey, request, ct);
        return created ? CreatedAtAction(nameof(Get), new { id = booking.Id }, booking) : Ok(booking);
    }

    // Admin also holds Staff, so one role check covers both (BOOK-6).
    [Authorize(Roles = Roles.Guest)]
    [HttpGet("my")]
    public async Task<IReadOnlyList<BookingSummary>> Mine([FromQuery] MyBookingsScope scope, CancellationToken ct) =>
        await bookings.ListMineAsync(CurrentUserId, scope, ct);

    [HttpGet("{id:guid}")]
    public async Task<BookingDetail> Get(Guid id, CancellationToken ct) =>
        await bookings.GetAsync(id, CurrentUserId, User.IsInRole(Roles.Staff), ct);

    // Owner only (SRS §6); anyone else gets 404.
    [HttpPost("{id:guid}/pay")]
    public async Task<BookingDetail> Pay(Guid id, CancellationToken ct) =>
        await bookings.PayAsync(id, CurrentUserId, ct);

    [HttpGet("{id:guid}/cancellation-quote")]
    public async Task<CancellationQuote> CancellationQuote(Guid id, CancellationToken ct) =>
        await bookings.GetCancellationQuoteAsync(id, CurrentUserId, User.IsInRole(Roles.Staff), ct);

    [HttpPost("{id:guid}/cancel")]
    public async Task<BookingDetail> Cancel(Guid id, CancelBookingRequest request, CancellationToken ct) =>
        await bookings.CancelAsync(id, CurrentUserId, User.IsInRole(Roles.Staff), request, ct);

    // [Authorize] guarantees an authenticated principal, and every access token is issued with `sub`.
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue("sub")!);
}
