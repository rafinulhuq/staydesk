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
    // Staff and Admin walk-ins (BOOK-9) are added in 4d.
    [Authorize(Roles = Roles.Guest)]
    [HttpPost]
    public async Task<ActionResult<BookingDetail>> Create(
        CreateBookingRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid? idempotencyKey,
        CancellationToken ct)
    {
        var (booking, created) = await bookings.CreateAsync(CurrentUserId, idempotencyKey, request, ct);
        return created ? CreatedAtAction(nameof(Get), new { id = booking.Id }, booking) : Ok(booking);
    }

    // Admin also holds Staff, so one role check covers both (BOOK-6).
    [HttpGet("{id:guid}")]
    public async Task<BookingDetail> Get(Guid id, CancellationToken ct) =>
        await bookings.GetAsync(id, CurrentUserId, User.IsInRole(Roles.Staff), ct);

    // [Authorize] guarantees an authenticated principal, and every access token is issued with `sub`.
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue("sub")!);
}
