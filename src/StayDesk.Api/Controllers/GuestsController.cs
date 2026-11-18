using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayDesk.Application.Auth;
using StayDesk.Application.Bookings;

namespace StayDesk.Api.Controllers;

// BOOK-9: staff pick the existing guest a walk-in is for. Admin also holds Staff.
[ApiController]
[Authorize(Roles = Roles.Staff)]
[Route("api/v1/guests")]
public sealed class GuestsController(IGuestAccounts guests) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<GuestSummary>> Search([FromQuery] GuestSearchRequest request, CancellationToken ct) =>
        await guests.SearchAsync(request.Search, ct);
}
