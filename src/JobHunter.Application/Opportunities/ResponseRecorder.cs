using JobHunter.Application.Common;
using JobHunter.Domain.Contacts.Events;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.Opportunities;

/// <summary>
/// An inbound conversation about an opportunity is its first response, if it came after
/// applying and nothing came sooner (spec 03, Follow-ups). Response rates are built on this.
/// </summary>
public sealed class ResponseRecorder(IOpportunitiesDbContext db) : IEventHandler<InteractionLogged>
{
    public async Task HandleAsync(InteractionLogged e, CancellationToken ct)
    {
        // The Contacts vocabulary, as published in its events.
        if (e.Direction != "inbound" || e.OpportunityId is not { } opportunityId) return;

        var opportunity = await db.Opportunities.FirstOrDefaultAsync(o => o.Id == opportunityId && o.UserId == e.UserId, ct);
        opportunity?.RecordResponse(e.OccurredAt);
    }
}
