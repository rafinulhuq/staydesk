using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayDesk.Application.Auth;
using StayDesk.Application.Properties;

namespace StayDesk.Api.Controllers;

[ApiController]
[Route("api/v1/properties")]
public sealed class PropertiesController(PropertyService properties) : ControllerBase
{
    // includeInactive is Admin-only (PROP-3); anyone else asking for it gets 403, not a silently filtered list.
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PropertySummary>>> List(string? city, bool includeInactive, CancellationToken ct)
    {
        if (includeInactive && !User.IsInRole(Roles.Admin))
        {
            return Forbid();
        }
        return Ok(await properties.ListAsync(city, includeInactive, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PropertyDetail>> Get(Guid id, bool includeInactive, CancellationToken ct)
    {
        if (includeInactive && !User.IsInRole(Roles.Admin))
        {
            return Forbid();
        }
        return await properties.GetAsync(id, includeInactive, ct);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<ActionResult<PropertyDetail>> Create(PropertyRequest request, CancellationToken ct)
    {
        var property = await properties.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = property.Id }, property);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:guid}")]
    public async Task<PropertyDetail> Update(Guid id, PropertyRequest request, CancellationToken ct) =>
        await properties.UpdateAsync(id, request, ct);

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id:guid}")]
    public async Task<NoContentResult> Deactivate(Guid id, CancellationToken ct)
    {
        await properties.DeactivateAsync(id, ct);
        return NoContent();
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost("{id:guid}/reactivate")]
    public async Task<NoContentResult> Reactivate(Guid id, CancellationToken ct)
    {
        await properties.ReactivateAsync(id, ct);
        return NoContent();
    }
}
