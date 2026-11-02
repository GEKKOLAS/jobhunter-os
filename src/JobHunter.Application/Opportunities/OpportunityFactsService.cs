using JobHunter.Application.Common;
using JobHunter.Application.Opportunities.Contracts;
using JobHunter.Domain.Opportunities;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.Opportunities;

internal sealed class OpportunityFactsService(IOpportunitiesDbContext db, ICurrentUser currentUser, IAuditWriter audit) : IOpportunityFacts
{
    public async Task<OpportunityFacts?> FindAsync(Guid id, CancellationToken ct) =>
        (await ListAsync([id], ct)).FirstOrDefault();

    public async Task<IReadOnlyList<OpportunityFacts>> ListAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var rows = await db.Opportunities.AsNoTracking()
            .Where(o => o.UserId == currentUser.UserId && ids.Contains(o.Id))
            .Select(o => new { o.Id, o.Title, o.Location, o.WorkMode, o.Stage, o.Priority, o.ArchivedAt, o.CreatedAt })
            .ToListAsync(ct);

        return rows
            .Select(o => new OpportunityFacts(
                o.Id, o.Title, o.Location, o.WorkMode, o.Stage, o.Priority,
                o.ArchivedAt is null && Stages.IsOpen(o.Stage), o.CreatedAt))
            .ToList();
    }

    public async Task<string?> PostingTextAsync(Guid id, CancellationToken ct) =>
        await db.Opportunities.AsNoTracking()
            .Where(o => o.Id == id && o.UserId == currentUser.UserId)
            .Select(o => o.Description)
            .FirstOrDefaultAsync(ct);

    public async Task SuggestPrioritiesAsync(IReadOnlyDictionary<Guid, string> suggestions, CancellationToken ct)
    {
        if (suggestions.Count == 0) return;

        var ids = suggestions.Keys.ToList();
        var rows = await db.Opportunities
            .Where(o => o.UserId == currentUser.UserId && ids.Contains(o.Id)
                && o.ArchivedAt == null && o.PrioritySetBy != OpportunityVocabulary.PrioritySetByUser)
            .ToListAsync(ct);

        foreach (var opportunity in rows.Where(o => Stages.IsOpen(o.Stage) && o.Priority != suggestions[o.Id]))
        {
            var from = opportunity.Priority;
            opportunity.Priority = suggestions[opportunity.Id];
            opportunity.PrioritySetBy = OpportunityVocabulary.PrioritySetBySuggestion;
            audit.Add(OpportunitiesAudit.PrioritySuggested, currentUser.UserId, "opportunity", opportunity.Id, new { from, to = opportunity.Priority });
        }
    }
}
