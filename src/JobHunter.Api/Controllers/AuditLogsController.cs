using JobHunter.Application.Common;
using JobHunter.Application.Identity;
using Microsoft.AspNetCore.Mvc;

namespace JobHunter.Api.Controllers;

[ApiController]
[Route("api/v1/audit-logs")]
public sealed class AuditLogsController(AuditLogsHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<Paged<AuditLogDto>> List(
        [FromQuery] string? action,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken ct) =>
        await handler.ListAsync(new AuditLogQuery(action, from, to, page, pageSize), ct);
}
