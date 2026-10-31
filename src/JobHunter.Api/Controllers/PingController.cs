using JobHunter.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobHunter.Api.Controllers;

/// <summary>Liveness for the web app's status indicator; `/healthz` is the database-aware check.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/ping")]
public sealed class PingController(IClock clock) : ControllerBase
{
    public sealed record PingResponse(string Status, string Service, DateTimeOffset Time);

    [HttpGet]
    public PingResponse Get() => new("ok", "jobhunter-api", clock.UtcNow);
}
