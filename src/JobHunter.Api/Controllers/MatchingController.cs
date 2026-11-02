using JobHunter.Application.Matching;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobHunter.Api.Controllers;

[ApiController]
[Route("api/v1/opportunities/{id:guid}")]
public sealed class MatchingController(RequirementsHandler requirements, FitHandler fit) : ControllerBase
{
    [HttpGet("requirements")]
    public Task<RequirementsDto> GetRequirements(Guid id, CancellationToken ct) => requirements.GetAsync(id, ct);

    [HttpPut("requirements")]
    public Task<RequirementsDto> ReplaceRequirements(Guid id, ReplaceRequirements command, CancellationToken ct) =>
        requirements.ReplaceAsync(id, command, ct);

    [HttpPost("requirements/extract")]
    [EnableRateLimiting("ai")]
    public Task<ExtractionDto> Extract(Guid id, CancellationToken ct) => requirements.ExtractAsync(id, ct);

    [HttpGet("fit")]
    public Task<FitDto> GetFit(Guid id, CancellationToken ct) => fit.GetAsync(id, ct);
}
