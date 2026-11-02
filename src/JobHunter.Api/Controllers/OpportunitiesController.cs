using JobHunter.Application.Common;
using JobHunter.Application.Opportunities;
using Microsoft.AspNetCore.Mvc;

namespace JobHunter.Api.Controllers;

[ApiController]
[Route("api/v1/opportunities")]
public sealed class OpportunitiesController(OpportunitiesHandler handler) : ControllerBase
{
    public sealed record OpportunityRequest(
        string Title, Guid? CompanyId, string? CompanyName, string? Location, string? WorkMode, string? EmploymentType,
        int? SalaryMin, int? SalaryMax, string? SalaryCurrency, string? SalaryPeriod, string? SourceUrl,
        string? Description, string? Priority, string? Stage = null, string? Source = null, string? SourceRef = null, uint Version = 0)
    {
        public OpportunityInput ToInput() => new(
            Title, CompanyId, CompanyName, Location, WorkMode, EmploymentType, SalaryMin, SalaryMax,
            SalaryCurrency, SalaryPeriod, SourceUrl, Description, Priority, Stage, Source, SourceRef);
    }

    [HttpGet]
    public Task<Paged<OpportunityCardDto>> List(
        [FromQuery] string? stage, [FromQuery] string? priority, [FromQuery] string? q,
        [FromQuery] bool archived, [FromQuery] int page, [FromQuery] int pageSize, CancellationToken ct) =>
        handler.ListAsync(new OpportunityListQuery(stage, priority, q, archived, page, pageSize), ct);

    [HttpPost]
    public async Task<IActionResult> Create(OpportunityRequest request, CancellationToken ct)
    {
        var saved = await handler.SaveAsync(request.ToInput(), ct);
        return saved.Created ? StatusCode(StatusCodes.Status201Created, saved.Opportunity) : Ok(saved.Opportunity);
    }

    [HttpGet("{id:guid}")]
    public Task<OpportunityDto> Get(Guid id, CancellationToken ct) => handler.GetAsync(id, ct);

    [HttpPut("{id:guid}")]
    public Task<OpportunityDto> Update(Guid id, OpportunityRequest request, CancellationToken ct) =>
        handler.UpdateAsync(id, request.ToInput(), request.Version, ct);

    /// <summary>Archives (a soft delete); <c>POST …/restore</c> undoes it.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await handler.ArchiveAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    public Task<OpportunityDto> Restore(Guid id, CancellationToken ct) => handler.RestoreAsync(id, ct);

    [HttpPost("{id:guid}/stage")]
    public Task<OpportunityDto> Move(Guid id, MoveStageCommand command, CancellationToken ct) =>
        handler.MoveAsync(id, command, ct);

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> History(Guid id, CancellationToken ct) =>
        Ok(new { items = await handler.HistoryAsync(id, ct) });
}

[ApiController]
[Route("api/v1/board")]
public sealed class BoardController(BoardHandler handler) : ControllerBase
{
    [HttpGet]
    public Task<BoardDto> Get(CancellationToken ct) => handler.GetAsync(ct);
}

[ApiController]
[Route("api/v1/companies")]
public sealed class CompaniesController(CompaniesHandler handler) : ControllerBase
{
    public sealed record CompanyRequest(string Name, string? Website, string? Notes, uint Version = 0)
    {
        public CompanyInput ToInput() => new(Name, Website, Notes);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, CancellationToken ct) =>
        Ok(new { items = await handler.ListAsync(q, ct) });

    [HttpPost]
    public async Task<IActionResult> Create(CompanyRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await handler.CreateAsync(request.ToInput(), ct));

    [HttpPut("{id:guid}")]
    public Task<CompanyDto> Update(Guid id, CompanyRequest request, CancellationToken ct) =>
        handler.UpdateAsync(id, request.ToInput(), request.Version, ct);
}
