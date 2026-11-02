using JobHunter.Application.Opportunities.Imports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobHunter.Api.Controllers;

/// <summary>Previews of a pasted text or a link. Nothing is saved until <c>POST /opportunities</c>.</summary>
[ApiController]
[Route("api/v1/imports")]
public sealed class ImportsController(ImportsHandler handler) : ControllerBase
{
    [HttpPost("text")]
    public Task<PostingPreview> FromText(TextImportCommand command, CancellationToken ct) => handler.FromTextAsync(command, ct);

    [HttpPost("url")]
    [EnableRateLimiting("import")]
    public Task<PostingPreview> FromUrl(UrlImportCommand command, CancellationToken ct) => handler.FromUrlAsync(command, ct);
}

/// <summary>Public company job boards the user follows; browsing them is read-only.</summary>
[ApiController]
[Route("api/v1/sources")]
public sealed class SourcesController(SourcesHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(new { items = await handler.ListAsync(ct) });

    [HttpPost]
    [EnableRateLimiting("import")]
    public async Task<IActionResult> Create(SourceInput input, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await handler.CreateAsync(input, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await handler.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/postings")]
    [EnableRateLimiting("import")]
    public async Task<IActionResult> Postings(Guid id, CancellationToken ct) =>
        Ok(new { items = await handler.PostingsAsync(id, ct) });

    [HttpPost("{id:guid}/postings/{postingRef}/save")]
    [EnableRateLimiting("import")]
    public async Task<IActionResult> SavePosting(Guid id, string postingRef, CancellationToken ct)
    {
        var saved = await handler.SavePostingAsync(id, postingRef, ct);
        return saved.Created ? StatusCode(StatusCodes.Status201Created, saved.Opportunity) : Ok(saved.Opportunity);
    }
}
