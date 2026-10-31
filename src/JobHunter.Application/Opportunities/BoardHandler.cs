using JobHunter.Application.Common;
using JobHunter.Domain.Opportunities;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.Opportunities;

/// <summary>The Kanban board: one column per open stage, plus how many ended in each closed one.</summary>
public sealed class BoardHandler(IOpportunitiesDbContext db, ICurrentUser currentUser)
{
    private static readonly Dictionary<string, int> PriorityRank = new() { ["high"] = 0, ["normal"] = 1, ["low"] = 2 };

    public async Task<BoardDto> GetAsync(CancellationToken ct)
    {
        var rows = await db.Opportunities.AsNoTracking()
            .Where(o => o.UserId == currentUser.UserId && o.ArchivedAt == null)
            .Select(o => new { Opportunity = o, CompanyName = o.Company!.Name })
            .ToListAsync(ct);

        var columns = Stages.Open
            .Select(stage => new BoardColumnDto(stage, rows
                .Where(r => r.Opportunity.Stage == stage)
                .OrderBy(r => PriorityRank.GetValueOrDefault(r.Opportunity.Priority, 1))
                .ThenBy(r => r.Opportunity.CreatedAt)
                .Select(r => OpportunityCardDto.From(r.Opportunity, r.CompanyName))
                .ToList()))
            .ToList();

        var closed = Stages.Closed.ToDictionary(stage => stage, stage => rows.Count(r => r.Opportunity.Stage == stage));
        return new BoardDto(columns, closed);
    }
}
