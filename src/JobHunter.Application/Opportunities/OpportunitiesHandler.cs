using FluentValidation;
using JobHunter.Application.Common;
using JobHunter.Application.Matching.Contracts;
using JobHunter.Domain.Opportunities;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.Opportunities;

public sealed class OpportunitiesHandler(
    IOpportunitiesDbContext db,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock,
    CompanyResolver companies,
    IFitScores fitScores,
    IValidator<OpportunityInput> inputValidator,
    IValidator<MoveStageCommand> moveValidator)
{
    public async Task<Paged<OpportunityCardDto>> ListAsync(OpportunityListQuery query, CancellationToken ct)
    {
        var fits = await fitScores.CurrentAsync(ct);
        var (page, pageSize) = Paged<OpportunityCardDto>.Clamp(query.Page, query.PageSize);
        var q = db.Opportunities.AsNoTracking()
            .Where(o => o.UserId == currentUser.UserId && (o.ArchivedAt != null) == query.Archived);

        q = query.Stage switch
        {
            null or "" => q,
            "open" => q.Where(o => Stages.Open.Contains(o.Stage)),
            "closed" => q.Where(o => Stages.Closed.Contains(o.Stage)),
            var stage => q.Where(o => o.Stage == stage),
        };
        if (!string.IsNullOrWhiteSpace(query.Priority)) q = q.Where(o => o.Priority == query.Priority);
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim().ToLower();
            q = q.Where(o => o.Title.ToLower().Contains(term) || o.Company!.Name.ToLower().Contains(term));
        }

        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderByDescending(o => o.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new { Opportunity = o, CompanyName = o.Company!.Name })
            .ToListAsync(ct);

        return new Paged<OpportunityCardDto>(
            rows.Select(r => OpportunityCardDto.From(r.Opportunity, r.CompanyName, fits.GetValueOrDefault(r.Opportunity.Id))).ToList(),
            page, pageSize, total);
    }

    public async Task<OpportunityDto> GetAsync(Guid id, CancellationToken ct)
    {
        // First: bringing the fits up to date can change a suggested priority, and with it the version the client edits.
        await fitScores.CurrentAsync(ct);
        var opportunity = await FindAsync(id, ct, tracking: false);
        return OpportunityDto.From(opportunity, opportunity.Company!);
    }

    /// <summary>
    /// Saves a new opportunity. A <c>SourceRef</c> that's already saved updates that one instead:
    /// the posting's fields change; its stage, priority and history stay (spec 03, Dedupe).
    /// </summary>
    public async Task<SavedOpportunity> SaveAsync(OpportunityInput input, CancellationToken ct)
    {
        await inputValidator.ValidateAndThrowAsync(input, ct);
        var company = await companies.ResolveAsync(input.CompanyId, input.CompanyName, ct);
        var source = input.Source ?? "manual";
        var sourceRef = Trimmed(input.SourceRef);

        if (sourceRef is not null)
        {
            var existing = await db.Opportunities.FirstOrDefaultAsync(
                o => o.UserId == currentUser.UserId && o.Source == source && o.SourceRef == sourceRef, ct);
            if (existing is not null)
            {
                existing.CompanyId = company.Id;
                existing.Title = input.Title.Trim();
                Apply(existing, input with { Priority = null });
                audit.Add(OpportunitiesAudit.Reimported, currentUser.UserId, "opportunity", existing.Id);
                await db.SaveChangesAsync(ct);
                return new SavedOpportunity(OpportunityDto.From(existing, company), Created: false);
            }
        }

        var opportunity = new Opportunity
        {
            UserId = currentUser.UserId,
            CompanyId = company.Id,
            Title = input.Title.Trim(),
            Source = source,
            SourceRef = sourceRef,
        };
        Apply(opportunity, input);
        var start = opportunity.Start(input.Stage ?? Stages.Saved, clock.UtcNow);

        db.Opportunities.Add(opportunity);
        db.OpportunityStageChanges.Add(start);
        audit.Add(OpportunitiesAudit.Created, currentUser.UserId, "opportunity", opportunity.Id, new { opportunity.Stage, source });
        await db.SaveChangesAsync(ct);
        return new SavedOpportunity(OpportunityDto.From(opportunity, company), Created: true);
    }

    /// <summary>Edits everything but the stage, which only moves through <see cref="MoveAsync"/>.</summary>
    public async Task<OpportunityDto> UpdateAsync(Guid id, OpportunityInput input, uint version, CancellationToken ct)
    {
        await inputValidator.ValidateAndThrowAsync(input with { Stage = null }, ct);
        var opportunity = await FindAsync(id, ct, tracking: true);
        var company = await companies.ResolveAsync(input.CompanyId, input.CompanyName, ct);

        db.Entry(opportunity).Property(o => o.Version).OriginalValue = version;
        opportunity.CompanyId = company.Id;
        opportunity.Title = input.Title.Trim();
        Apply(opportunity, input);

        audit.Add(OpportunitiesAudit.Updated, currentUser.UserId, "opportunity", opportunity.Id);
        await db.SaveChangesAsync(ct);
        return OpportunityDto.From(opportunity, company);
    }

    public async Task<OpportunityDto> MoveAsync(Guid id, MoveStageCommand command, CancellationToken ct)
    {
        await moveValidator.ValidateAndThrowAsync(command, ct);
        var opportunity = await FindAsync(id, ct, tracking: true);
        if (opportunity.ArchivedAt is not null)
        {
            throw new ConflictException("opportunity.archived", "Restore this opportunity before moving it.");
        }

        var allowed = StageRules.AllowedMoves(opportunity.Stage, opportunity.PreviousStage);
        if (!allowed.Contains(command.Stage))
        {
            throw new ConflictException("opportunity.invalid_stage",
                $"From '{opportunity.Stage}' it can move to: {string.Join(", ", allowed)}.");
        }

        var from = opportunity.Stage;
        db.OpportunityStageChanges.Add(opportunity.MoveTo(command.Stage, clock.UtcNow, Trimmed(command.Note)));
        audit.Add(OpportunitiesAudit.StageChanged, currentUser.UserId, "opportunity", opportunity.Id, new { from, to = command.Stage });
        await db.SaveChangesAsync(ct);
        return OpportunityDto.From(opportunity, opportunity.Company!);
    }

    public async Task<IReadOnlyList<StageChangeDto>> HistoryAsync(Guid id, CancellationToken ct)
    {
        await FindAsync(id, ct, tracking: false); // ownership
        return await db.OpportunityStageChanges.AsNoTracking()
            .Where(c => c.OpportunityId == id && c.UserId == currentUser.UserId)
            .OrderBy(c => c.OccurredAt).ThenBy(c => c.Id)
            .Select(c => new StageChangeDto(c.Id, c.FromStage, c.ToStage, c.OccurredAt, c.Note))
            .ToListAsync(ct);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct)
    {
        var opportunity = await FindAsync(id, ct, tracking: true);
        if (opportunity.ArchivedAt is not null) return;
        opportunity.ArchivedAt = clock.UtcNow;
        audit.Add(OpportunitiesAudit.Archived, currentUser.UserId, "opportunity", id);
        await db.SaveChangesAsync(ct);
    }

    public async Task<OpportunityDto> RestoreAsync(Guid id, CancellationToken ct)
    {
        var opportunity = await FindAsync(id, ct, tracking: true);
        if (opportunity.ArchivedAt is not null)
        {
            opportunity.ArchivedAt = null;
            audit.Add(OpportunitiesAudit.Restored, currentUser.UserId, "opportunity", id);
            await db.SaveChangesAsync(ct);
        }
        return OpportunityDto.From(opportunity, opportunity.Company!);
    }

    private async Task<Opportunity> FindAsync(Guid id, CancellationToken ct, bool tracking)
    {
        var query = db.Opportunities.Include(o => o.Company).Where(o => o.Id == id && o.UserId == currentUser.UserId);
        return await (tracking ? query : query.AsNoTracking()).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
    }

    private static void Apply(Opportunity o, OpportunityInput input)
    {
        o.Location = Trimmed(input.Location);
        o.WorkMode = input.WorkMode;
        o.EmploymentType = input.EmploymentType;
        o.SalaryMin = input.SalaryMin;
        o.SalaryMax = input.SalaryMax;
        o.SalaryCurrency = input.SalaryCurrency?.ToUpperInvariant();
        o.SalaryPeriod = input.SalaryPeriod;
        o.SourceUrl = Trimmed(input.SourceUrl);
        o.Description = input.Description?.Trim() ?? "";
        // Choosing one counts as setting it; sending back the value it already has doesn't, so a
        // new opportunity left on the default and an edit that didn't touch it stay open to suggestions.
        if (input.Priority is not null && input.Priority != o.Priority)
        {
            o.Priority = input.Priority;
            o.PrioritySetBy = OpportunityVocabulary.PrioritySetByUser;
        }
    }

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
