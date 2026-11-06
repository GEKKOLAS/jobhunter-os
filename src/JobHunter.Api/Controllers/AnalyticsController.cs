using JobHunter.Application.Analytics;
using Microsoft.AspNetCore.Mvc;

namespace JobHunter.Api.Controllers;

[ApiController]
[Route("api/v1/analytics")]
public sealed class AnalyticsController(AnalyticsHandler handler) : ControllerBase
{
    /// <summary>Rates, funnel, weekly series and breakdowns for the last <paramref name="days"/> days.</summary>
    /// <param name="days">30, 90 or 365 (default 90).</param>
    /// <param name="tz">The viewer's IANA time zone, for the weekly buckets (default UTC).</param>
    [HttpGet("summary")]
    public Task<SummaryDto> Summary([FromQuery] int? days, [FromQuery] string? tz, CancellationToken ct) =>
        handler.SummaryAsync(days, tz, ct);

    [HttpGet("skills")]
    public Task<SkillsDto> Skills([FromQuery] int? days, CancellationToken ct) => handler.SkillsAsync(days, ct);
}

[ApiController]
[Route("api/v1/dashboard")]
public sealed class DashboardController(DashboardHandler handler) : ControllerBase
{
    [HttpGet]
    public Task<DashboardDto> Get(CancellationToken ct) => handler.GetAsync(ct);
}
