using Microsoft.AspNetCore.Mvc;
using StayDesk.Application.Rooms;

namespace StayDesk.Api.Controllers;

[ApiController]
[Route("api/v1/amenities")]
public sealed class AmenitiesController(RoomService rooms) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<AmenitySummary>> List(CancellationToken ct) =>
        await rooms.ListAmenitiesAsync(ct);
}
