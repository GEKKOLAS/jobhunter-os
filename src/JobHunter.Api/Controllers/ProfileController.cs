using JobHunter.Application.Profile;
using JobHunter.Application.Profile.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace JobHunter.Api.Controllers;

[ApiController]
[Route("api/v1/profile")]
public sealed class ProfileController : ControllerBase
{
    public sealed record SkillRequest(string Name, string? Category, int Level, decimal Years, int? LastUsedYear, uint Version = 0)
    {
        public SkillInput ToInput() => new(Name, Category, Level, Years, LastUsedYear);
    }

    public sealed record EvidenceRequest(
        string Kind, string Title, string? Organization, DateOnly? StartDate, DateOnly? EndDate, string? Description,
        string? Outcome, IReadOnlyList<LinkDto>? Links, IReadOnlyList<Guid>? SkillIds, uint Version = 0)
    {
        public EvidenceInput ToInput() =>
            new(Kind, Title, Organization, StartDate, EndDate, Description, Outcome, Links, SkillIds);
    }

    [HttpGet]
    public Task<ProfileDto> Get([FromServices] ProfileHandler handler, CancellationToken ct) => handler.GetAsync(ct);

    [HttpPut]
    public Task<ProfileDto> Update(UpdateProfileCommand request, [FromServices] ProfileHandler handler, CancellationToken ct) =>
        handler.UpdateAsync(request, ct);

    [HttpGet("skills")]
    public async Task<IActionResult> Skills([FromServices] SkillsHandler handler, CancellationToken ct) =>
        Ok(new { items = await handler.ListAsync(ct) });

    [HttpPost("skills")]
    public async Task<IActionResult> CreateSkill(SkillRequest request, [FromServices] SkillsHandler handler, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await handler.CreateAsync(request.ToInput(), ct));

    [HttpPut("skills/{id:guid}")]
    public Task<SkillDto> UpdateSkill(Guid id, SkillRequest request, [FromServices] SkillsHandler handler, CancellationToken ct) =>
        handler.UpdateAsync(id, request.ToInput(), request.Version, ct);

    [HttpDelete("skills/{id:guid}")]
    public async Task<IActionResult> DeleteSkill(Guid id, [FromServices] SkillsHandler handler, CancellationToken ct)
    {
        await handler.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("evidence")]
    public async Task<IActionResult> Evidence([FromServices] EvidenceHandler handler, CancellationToken ct) =>
        Ok(new { items = await handler.ListAsync(ct) });

    [HttpPost("evidence")]
    public async Task<IActionResult> CreateEvidence(EvidenceRequest request, [FromServices] EvidenceHandler handler, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await handler.CreateAsync(request.ToInput(), ct));

    [HttpPut("evidence/{id:guid}")]
    public Task<EvidenceDto> UpdateEvidence(Guid id, EvidenceRequest request, [FromServices] EvidenceHandler handler, CancellationToken ct) =>
        handler.UpdateAsync(id, request.ToInput(), request.Version, ct);

    [HttpDelete("evidence/{id:guid}")]
    public async Task<IActionResult> DeleteEvidence(Guid id, [FromServices] EvidenceHandler handler, CancellationToken ct)
    {
        await handler.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/skills")]
public sealed class SkillCatalogController(ISkillCatalog catalog) : ControllerBase
{
    /// <summary>Autocomplete over the skill catalog (spec 03, Profile).</summary>
    [HttpGet("catalog")]
    public IActionResult Search([FromQuery] string? q, [FromQuery] int limit = 8) =>
        Ok(new { items = catalog.Search(q ?? "", Math.Clamp(limit, 1, 25)) });
}
