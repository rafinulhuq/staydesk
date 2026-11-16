using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayDesk.Application.Auth;
using StayDesk.Application.Rooms;

namespace StayDesk.Api.Controllers;

[ApiController]
[Route("api/v1/rooms")]
public sealed class RoomsController(RoomService rooms) : ControllerBase
{
    // Admin sees inactive rooms (to edit or reactivate them); everyone else gets 404 for them.
    [HttpGet("{id:guid}")]
    public async Task<RoomDetail> Get(Guid id, CancellationToken ct) =>
        await rooms.GetAsync(id, includeInactive: User.IsInRole(Roles.Admin), ct);

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<ActionResult<RoomDetail>> Create(CreateRoomRequest request, CancellationToken ct)
    {
        var room = await rooms.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = room.Id }, room);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:guid}")]
    public async Task<RoomDetail> Update(Guid id, UpdateRoomRequest request, CancellationToken ct) =>
        await rooms.UpdateAsync(id, request, ct);

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id:guid}")]
    public async Task<RoomDeactivation> Deactivate(Guid id, CancellationToken ct) =>
        await rooms.DeactivateAsync(id, ct);

    [Authorize(Roles = Roles.Admin)]
    [HttpPost("{id:guid}/reactivate")]
    public async Task<NoContentResult> Reactivate(Guid id, CancellationToken ct)
    {
        await rooms.ReactivateAsync(id, ct);
        return NoContent();
    }
}
