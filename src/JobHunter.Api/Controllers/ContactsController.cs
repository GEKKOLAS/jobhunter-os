using JobHunter.Application.Common;
using JobHunter.Application.Contacts;
using Microsoft.AspNetCore.Mvc;

namespace JobHunter.Api.Controllers;

[ApiController]
[Route("api/v1/contacts")]
public sealed class ContactsController(ContactsHandler contacts, InteractionsHandler interactions, IClock clock) : ControllerBase
{
    public sealed record ContactRequest(
        string Name, Guid? CompanyId, string? CompanyName, string? Role, string? Email, string? LinkedInUrl, string? Phone, string? Notes, uint Version = 0)
    {
        public ContactInput ToInput() => new(Name, CompanyId, CompanyName, Role, Email, LinkedInUrl, Phone, Notes);
    }

    [HttpGet]
    public Task<Paged<ContactDto>> List(
        [FromQuery] string? q, [FromQuery] Guid? companyId, [FromQuery] bool archived,
        [FromQuery] int page, [FromQuery] int pageSize, CancellationToken ct) =>
        contacts.ListAsync(new ContactListQuery(q, companyId, archived, page, pageSize), ct);

    [HttpPost]
    public async Task<IActionResult> Create(ContactRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await contacts.CreateAsync(request.ToInput(), ct));

    [HttpGet("{id:guid}")]
    public Task<ContactDto> Get(Guid id, CancellationToken ct) => contacts.GetAsync(id, ct);

    [HttpPut("{id:guid}")]
    public Task<ContactDto> Update(Guid id, ContactRequest request, CancellationToken ct) =>
        contacts.UpdateAsync(id, request.ToInput(), request.Version, ct);

    /// <summary>Archives (a soft delete); <c>POST …/restore</c> undoes it.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await contacts.ArchiveAsync(id, clock.UtcNow, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    public Task<ContactDto> Restore(Guid id, CancellationToken ct) => contacts.RestoreAsync(id, ct);

    [HttpPost("{id:guid}/opportunities/{opportunityId:guid}")]
    public Task<LinkedOpportunityDto> Link(Guid id, Guid opportunityId, LinkOpportunityCommand command, CancellationToken ct) =>
        interactions.LinkAsync(id, opportunityId, command, ct);

    [HttpDelete("{id:guid}/opportunities/{opportunityId:guid}")]
    public async Task<IActionResult> Unlink(Guid id, Guid opportunityId, CancellationToken ct)
    {
        await interactions.UnlinkAsync(id, opportunityId, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/interactions")]
    public async Task<IActionResult> ListInteractions(Guid id, CancellationToken ct) =>
        Ok(new { items = await interactions.ListAsync(id, ct) });

    [HttpPost("{id:guid}/interactions")]
    public async Task<IActionResult> LogInteraction(Guid id, InteractionInput input, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await interactions.LogAsync(id, input, ct));
}

[ApiController]
[Route("api/v1")]
public sealed class InteractionsController(InteractionsHandler interactions) : ControllerBase
{
    [HttpDelete("interactions/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await interactions.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("opportunities/{id:guid}/people")]
    public Task<PeopleDto> People(Guid id, CancellationToken ct) => interactions.PeopleAsync(id, ct);
}
