using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayDesk.Application.Auditing;
using StayDesk.Application.Auth;
using StayDesk.Application.Paging;

namespace StayDesk.Api.Controllers;

// AUD-3, Admin only. There is no update or delete endpoint (AUD-4).
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/v1/audit")]
public sealed class AuditController(AuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<Paged<AuditEntrySummary>> List([FromQuery] AuditLogRequest request, CancellationToken ct) =>
        await audit.ListAsync(request, ct);
}
