using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayDesk.Application.Auth;
using StayDesk.Application.Pricing;

namespace StayDesk.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/v1/price-rules")]
public sealed class PriceRulesController(PriceRuleService rules) : ControllerBase
{
    // Rules are always managed per property, so the list needs one.
    [HttpGet]
    public async Task<IReadOnlyList<PriceRuleDetail>> List([FromQuery] Guid propertyId, CancellationToken ct) =>
        await rules.ListAsync(propertyId, ct);

    [HttpGet("{id:guid}")]
    public async Task<PriceRuleDetail> Get(Guid id, CancellationToken ct) =>
        await rules.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<PriceRuleDetail>> Create(PriceRuleRequest request, CancellationToken ct)
    {
        var rule = await rules.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = rule.Id }, rule);
    }

    [HttpPut("{id:guid}")]
    public async Task<PriceRuleDetail> Update(Guid id, PriceRuleRequest request, CancellationToken ct) =>
        await rules.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    public async Task<NoContentResult> Deactivate(Guid id, CancellationToken ct)
    {
        await rules.DeactivateAsync(id, ct);
        return NoContent();
    }
}
