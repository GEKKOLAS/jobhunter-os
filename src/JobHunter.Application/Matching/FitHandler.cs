using System.Text.Json;
using JobHunter.Application.Common;
using JobHunter.Application.Matching.Contracts;
using JobHunter.Application.Opportunities.Contracts;
using JobHunter.Application.Profile.Contracts;
using JobHunter.Domain.Matching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobHunter.Application.Matching;

/// <summary>
/// Computes fits on read and stores them only when the result changed (spec 03, Matching:
/// always current, no stale flag). The score uses confirmed requirements only.
/// </summary>
public sealed class FitHandler(
    IMatchingDbContext db,
    ICurrentUser currentUser,
    ICandidateFacts candidates,
    IOpportunityFacts opportunities,
    ISkillCatalog catalog,
    IClock clock,
    ILogger<FitHandler> logger) : IFitScores
{
    public async Task<FitDto> GetAsync(Guid opportunityId, CancellationToken ct)
    {
        var facts = await opportunities.FindAsync(opportunityId, ct) ?? throw new NotFoundException();
        var computed = await RefreshAsync([facts], ct);
        return computed.Single().Dto;
    }

    public async Task<IReadOnlyDictionary<Guid, FitScore>> CurrentAsync(CancellationToken ct)
    {
        var ids = await db.OpportunityRequirements.AsNoTracking()
            .Where(r => r.UserId == currentUser.UserId && r.Confirmed)
            .Select(r => r.OpportunityId)
            .Distinct()
            .ToListAsync(ct);
        if (ids.Count == 0) return new Dictionary<Guid, FitScore>();

        var computed = await RefreshAsync(await opportunities.ListAsync(ids, ct), ct);
        return computed
            .Where(c => c.Dto.Score is not null)
            .ToDictionary(c => c.Id, c => new FitScore(c.Dto.Score!.Value, c.Dto.Band!));
    }

    private async Task<IReadOnlyList<(Guid Id, FitDto Dto)>> RefreshAsync(IReadOnlyList<OpportunityFacts> targets, CancellationToken ct)
    {
        var ids = targets.Select(t => t.Id).ToList();
        var requirements = (await db.OpportunityRequirements.AsNoTracking()
                .Where(r => r.UserId == currentUser.UserId && r.Confirmed && ids.Contains(r.OpportunityId))
                .OrderBy(r => r.Position)
                .ToListAsync(ct))
            .ToLookup(r => r.OpportunityId);
        var stored = await db.FitAssessments
            .Where(a => a.UserId == currentUser.UserId && ids.Contains(a.OpportunityId))
            .ToDictionaryAsync(a => a.OpportunityId, ct);

        var candidate = await candidates.ForCurrentUserAsync(ct);
        var skills = candidate.Skills
            .Select(s => new SkillInput(s.Key, s.Name, s.Level, s.Years, catalog.Find(s.Key)?.Family))
            .ToList();
        // To the microsecond, which is what the database keeps: the first read and the next must agree.
        var now = clock.UtcNow;
        now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));

        var results = new List<(Guid Id, FitDto Dto)>(targets.Count);
        var suggestions = new Dictionary<Guid, string>();

        foreach (var target in targets)
        {
            var result = FitAlgorithm.Assess(requirements[target.Id].Select(ToInput).ToList(), skills);
            var notes = FitAlgorithm.Notes(target.Title, target.Location, target.WorkMode, candidate.Seniority, candidate.WorkModes, candidate.Location);
            var suggested = FitAlgorithm.SuggestedPriority(result.Band, target.Stage, target.CreatedAt, now);

            // Without a score an earlier suggestion no longer stands. A priority the user set is left alone by the contract.
            var wanted = suggested ?? "normal";
            if (target.CanTakePriority && target.Priority != wanted) suggestions[target.Id] = wanted;

            stored.TryGetValue(target.Id, out var assessment);
            if (requirements[target.Id].Any())
            {
                if (assessment is null || !IsSame(assessment, result, notes))
                {
                    assessment ??= AddAssessment(target.Id);
                    Store(assessment, result, notes, now);
                }
            }
            else if (assessment is not null)
            {
                db.FitAssessments.Remove(assessment);
                assessment = null;
            }

            results.Add((target.Id, new FitDto(
                result.Score, result.Band, result.MissingReason, result.Outcomes, notes, suggested,
                FitAlgorithm.Version, assessment?.ComputedAt)));
        }

        await opportunities.SuggestPrioritiesAsync(suggestions, ct);
        if (db.ChangeTracker.HasChanges()) await SaveQuietlyAsync(ct);
        return results;
    }

    /// <summary>
    /// Two reads can race to write the same deterministic result, or a read can race an edit of
    /// the opportunity. Neither is worth failing a read for: this one keeps the answer it
    /// computed and drops its write, and the next read settles it.
    /// </summary>
    private async Task SaveQuietlyAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            logger.LogInformation("A fit update lost a race; the next read will settle it");
            db.ChangeTracker.Clear();
        }
    }

    private FitAssessment AddAssessment(Guid opportunityId)
    {
        var assessment = new FitAssessment { UserId = currentUser.UserId, OpportunityId = opportunityId };
        db.FitAssessments.Add(assessment);
        return assessment;
    }

    private RequirementInput ToInput(OpportunityRequirement r)
    {
        var known = r.SkillKey is null ? null : catalog.Find(r.SkillKey);
        return new RequirementInput(r.Text, r.Kind, r.SkillKey, r.SkillKey is null ? null : known?.Name ?? r.SkillKey, r.YearsMin, known?.Family);
    }

    private static bool IsSame(FitAssessment stored, FitResult result, IReadOnlyList<FitNote> notes) =>
        stored.Score == result.Score
        && stored.Band == result.Band
        && stored.MissingReason == result.MissingReason
        && stored.AlgorithmVersion == FitAlgorithm.Version
        && JsonSerializer.Serialize(stored.Outcomes) == JsonSerializer.Serialize(result.Outcomes)
        && JsonSerializer.Serialize(stored.Notes) == JsonSerializer.Serialize(notes);

    private static void Store(FitAssessment assessment, FitResult result, IReadOnlyList<FitNote> notes, DateTimeOffset now)
    {
        assessment.Score = result.Score;
        assessment.Band = result.Band;
        assessment.MissingReason = result.MissingReason;
        assessment.Outcomes = result.Outcomes.ToList();
        assessment.Notes = notes.ToList();
        assessment.AlgorithmVersion = FitAlgorithm.Version;
        assessment.ComputedAt = now;
    }
}
