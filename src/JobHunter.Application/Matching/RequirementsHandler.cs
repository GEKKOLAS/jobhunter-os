using FluentValidation;
using JobHunter.Application.Common;
using JobHunter.Application.Opportunities.Contracts;
using JobHunter.Application.Profile.Contracts;
using JobHunter.Domain.Matching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobHunter.Application.Matching;

public sealed class RequirementsHandler(
    IMatchingDbContext db,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IOpportunityFacts opportunities,
    ISkillCatalog catalog,
    IRequirementExtractor extractor,
    RuleBasedRequirementExtractor rules,
    FitHandler fit,
    IValidator<ReplaceRequirements> validator,
    ILogger<RequirementsHandler> logger)
{
    public async Task<RequirementsDto> GetAsync(Guid opportunityId, CancellationToken ct)
    {
        await EnsureOwnedAsync(opportunityId, ct);
        var rows = await ListAsync(opportunityId, ct);
        return new RequirementsDto(rows.Select(r => RequirementDto.From(r, catalog)).ToList(), extractor.Origin);
    }

    /// <summary>The user's list replaces whatever was there, and all of it counts as confirmed.</summary>
    public async Task<RequirementsDto> ReplaceAsync(Guid opportunityId, ReplaceRequirements command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);
        await EnsureOwnedAsync(opportunityId, ct);

        db.OpportunityRequirements.RemoveRange(await db.OpportunityRequirements
            .Where(r => r.OpportunityId == opportunityId && r.UserId == currentUser.UserId)
            .ToListAsync(ct));

        var rows = command.Items.Select((item, position) => new OpportunityRequirement
        {
            UserId = currentUser.UserId,
            OpportunityId = opportunityId,
            Text = item.Text.Trim(),
            Kind = item.Kind,
            SkillKey = KeyOf(item.Skill),
            YearsMin = item.YearsMin,
            Origin = RequirementVocabulary.OriginUser,
            Confirmed = true,
            Position = position,
        }).ToList();
        db.OpportunityRequirements.AddRange(rows);

        if (rows.Count == 0)
        {
            // Nothing left to estimate from, so nothing left to suggest either.
            db.FitAssessments.RemoveRange(await db.FitAssessments
                .Where(a => a.OpportunityId == opportunityId && a.UserId == currentUser.UserId)
                .ToListAsync(ct));
            await opportunities.SuggestPrioritiesAsync(new Dictionary<Guid, string> { [opportunityId] = "normal" }, ct);
        }

        audit.Add(MatchingAudit.Updated, currentUser.UserId, "opportunity", opportunityId, new { count = rows.Count });
        await db.SaveChangesAsync(ct);

        // Now, not on the next read: the page reloads several things at once, and they'd race to insert this row.
        await fit.GetAsync(opportunityId, ct);
        return new RequirementsDto(rows.Select(r => RequirementDto.From(r, catalog)).ToList(), extractor.Origin);
    }

    /// <summary>
    /// Reads the posting again. Only unconfirmed rows are replaced, and a new row repeating
    /// something confirmed is left out: what the user checked is never undone by a parser.
    /// </summary>
    public async Task<ExtractionDto> ExtractAsync(Guid opportunityId, CancellationToken ct)
    {
        var posting = await opportunities.PostingTextAsync(opportunityId, ct) ?? throw new NotFoundException();
        if (string.IsNullOrWhiteSpace(posting))
        {
            throw new InvalidInputException("requirements.no_posting", "This opportunity has no posting text to read. Add it, or list the requirements yourself.");
        }

        var (extracted, origin, fellBack) = await RunExtractorAsync(posting, ct);

        var existing = await ListAsync(opportunityId, ct, tracking: true);
        var confirmed = existing.Where(r => r.Confirmed).ToList();
        db.OpportunityRequirements.RemoveRange(existing.Where(r => !r.Confirmed));

        var confirmedKeys = confirmed.Where(r => r.SkillKey is not null).Select(r => r.SkillKey!).ToHashSet();
        var confirmedTexts = confirmed.Select(r => r.Text.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var position = confirmed.Count == 0 ? 0 : confirmed.Max(r => r.Position) + 1;

        var added = extracted
            .Where(e => e.SkillKey is null ? !confirmedTexts.Contains(e.Text.Trim()) : !confirmedKeys.Contains(e.SkillKey))
            .Take(RequirementVocabulary.MaxPerOpportunity - confirmed.Count)
            .Select((e, index) => new OpportunityRequirement
            {
                UserId = currentUser.UserId,
                OpportunityId = opportunityId,
                Text = e.Text,
                Kind = e.Kind,
                SkillKey = e.SkillKey,
                YearsMin = e.YearsMin,
                Origin = origin,
                Confirmed = false,
                Position = position + index,
            })
            .ToList();
        db.OpportunityRequirements.AddRange(added);

        audit.Add(MatchingAudit.Extracted, currentUser.UserId, "opportunity", opportunityId, new { origin, count = added.Count, fellBack });
        await db.SaveChangesAsync(ct);

        var items = confirmed.Concat(added).Select(r => RequirementDto.From(r, catalog)).ToList();
        return new ExtractionDto(items, origin, fellBack);
    }

    private async Task<(IReadOnlyList<ExtractedRequirement> Rows, string Origin, bool FellBack)> RunExtractorAsync(string posting, CancellationToken ct)
    {
        try
        {
            return (await extractor.ExtractAsync(posting, ct), extractor.Origin, false);
        }
        catch (AiUnavailableException ex)
        {
            logger.LogWarning(ex, "AI extraction failed, using the rules instead");
            return (await rules.ExtractAsync(posting, ct), rules.Origin, true);
        }
    }

    private string? KeyOf(string? skill)
    {
        if (string.IsNullOrWhiteSpace(skill)) return null;
        var key = catalog.KeyFor(skill);
        return key.Length == 0 ? null : key;
    }

    private Task<List<OpportunityRequirement>> ListAsync(Guid opportunityId, CancellationToken ct, bool tracking = false)
    {
        var query = db.OpportunityRequirements
            .Where(r => r.OpportunityId == opportunityId && r.UserId == currentUser.UserId)
            .OrderBy(r => r.Position);
        return (tracking ? query : query.AsNoTracking()).ToListAsync(ct);
    }

    private async Task EnsureOwnedAsync(Guid opportunityId, CancellationToken ct)
    {
        if (await opportunities.FindAsync(opportunityId, ct) is null) throw new NotFoundException();
    }
}
