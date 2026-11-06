using JobHunter.Application.FollowUps;
using JobHunter.Application.Integrations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobHunter.Api.Controllers;

[ApiController]
[Route("api/v1/reminders")]
public sealed class RemindersController(RemindersHandler handler) : ControllerBase
{
    public sealed record ReminderRequest(string Title, DateTimeOffset DueAt, string Kind, Guid? OpportunityId, Guid? ContactId, uint Version = 0)
    {
        public ReminderInput ToInput() => new(Title, DueAt, Kind, OpportunityId, ContactId);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] DateTimeOffset? until, [FromQuery] Guid? opportunityId, [FromQuery] Guid? contactId,
        CancellationToken ct) =>
        Ok(new { items = await handler.ListAsync(new ReminderListQuery(status, until, opportunityId, contactId), ct) });

    [HttpPost]
    public async Task<IActionResult> Create(ReminderRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await handler.CreateAsync(request.ToInput(), ct));

    [HttpPut("{id:guid}")]
    public Task<ReminderDto> Update(Guid id, ReminderRequest request, CancellationToken ct) =>
        handler.UpdateAsync(id, request.ToInput(), request.Version, ct);

    /// <summary>Dismisses it: it stays in the record, out of the way.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken ct)
    {
        await handler.DismissAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/done")]
    public Task<ReminderDto> Done(Guid id, CancellationToken ct) => handler.DoneAsync(id, ct);

    [HttpPost("{id:guid}/snooze")]
    public Task<ReminderDto> Snooze(Guid id, SnoozeCommand command, CancellationToken ct) => handler.SnoozeAsync(id, command, ct);
}

[ApiController]
[Route("api/v1/drafts")]
public sealed class DraftsController(DraftsHandler handler) : ControllerBase
{
    public sealed record DraftEditRequest(string? ToEmail, string? Subject, string? Body, uint Version = 0)
    {
        public DraftEdit ToEdit() => new(ToEmail, Subject, Body);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] Guid? opportunityId, [FromQuery] Guid? contactId, CancellationToken ct) =>
        Ok(new { items = await handler.ListAsync(new DraftListQuery(status, opportunityId, contactId), ct) });

    /// <summary>A written draft, when asked for and AI is configured, still only a draft.</summary>
    [HttpPost]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> Create(DraftRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await handler.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public Task<DraftDto> Update(Guid id, DraftEditRequest request, CancellationToken ct) =>
        handler.UpdateAsync(id, request.ToEdit(), request.Version, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Discard(Guid id, CancellationToken ct)
    {
        await handler.DiscardAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/approve")]
    public Task<DraftDto> Approve(Guid id, CancellationToken ct) => handler.ApproveAsync(id, ct);

    [HttpPost("{id:guid}/send")]
    [EnableRateLimiting("import")]
    public Task<DraftDto> Send(Guid id, CancellationToken ct) => handler.SendAsync(id, ct);

    [HttpPost("{id:guid}/mark-sent")]
    public Task<DraftDto> MarkSent(Guid id, CancellationToken ct) => handler.MarkSentAsync(id, ct);
}

[ApiController]
[Route("api/v1/notifications")]
public sealed class NotificationsController(NotificationsHandler handler) : ControllerBase
{
    public sealed record ReadRequest(IReadOnlyList<Guid>? Ids);

    [HttpGet]
    public Task<NotificationsDto> List(CancellationToken ct) => handler.ListAsync(ct);

    [HttpPost("read")]
    public async Task<IActionResult> Read(ReadRequest request, CancellationToken ct)
    {
        await handler.MarkReadAsync(request.Ids, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1")]
public sealed class IntegrationsController(OutreachHandler outreach, AiCapabilitiesHandler ai) : ControllerBase
{
    [HttpGet("integrations/outreach")]
    public Task<OutreachSettingsDto> GetOutreach(CancellationToken ct) => outreach.GetAsync(ct);

    [HttpPut("integrations/outreach")]
    public Task<OutreachSettingsDto> SaveOutreach(OutreachInput input, CancellationToken ct) => outreach.SaveAsync(input, ct);

    [HttpPost("integrations/outreach/test")]
    [EnableRateLimiting("import")]
    public Task<OutreachTestDto> TestOutreach(CancellationToken ct) => outreach.TestAsync(ct);

    [HttpDelete("integrations/outreach")]
    public async Task<IActionResult> RemoveOutreach(CancellationToken ct)
    {
        await outreach.RemoveAsync(ct);
        return NoContent();
    }

    [HttpGet("ai/capabilities")]
    public AiCapabilitiesDto Capabilities() => ai.Get();
}
