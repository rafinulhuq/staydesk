using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayDesk.Application.Auth;
using StayDesk.Application.Pricing;

namespace StayDesk.Api.Controllers;

[ApiController]
[Route("api/v1/pricing")]
public sealed class PricingController(PricingService pricing) : ControllerBase
{
    // POST although it changes nothing: the body carries the stay, and preview's body a whole rule.
    [HttpPost("quote")]
    public async Task<StayQuote> Quote(QuoteRequest request, CancellationToken ct) =>
        await pricing.QuoteAsync(request, ct);

    [Authorize(Roles = Roles.Admin)]
    [HttpPost("preview")]
    public async Task<PricePreview> Preview(PreviewRequest request, CancellationToken ct) =>
        await pricing.PreviewAsync(request, ct);
}
