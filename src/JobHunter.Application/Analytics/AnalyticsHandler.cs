using JobHunter.Application.Common;
using JobHunter.Application.Matching.Contracts;
using JobHunter.Application.Profile.Contracts;
using JobHunter.Domain.FollowUps;
using JobHunter.Domain.Opportunities;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.Analytics;

/// <summary>F6: how the search is going, computed from rows on every read (spec 03, Analytics).</summary>
public sealed class AnalyticsHandler(
    IAnalyticsDbContext db, ICurrentUser currentUser, IClock clock, IFitScores fitScores, ISkillCatalog catalog)
{
    private static readonly string[] InterviewingOrLater = [Stages.Interviewing, Stages.Offer, Stages.Accepted, Stages.Declined];
    private static readonly string[] OfferOrLater = [Stages.Offer, Stages.Accepted, Stages.Declined];

    public async Task<SummaryDto> SummaryAsync(int? days, string? timeZone, CancellationToken ct)
    {
        var window = AnalyticsWindow.Ending(clock.UtcNow, CheckedDays(days));
        var zone = ZoneFor(timeZone);
        var userId = currentUser.UserId;
        var from = window.From;

        // Everything with a moment in the range: saved, sent or answered in it.
        var opportunities = await db.Opportunities
            .Where(o => o.UserId == userId && o.ArchivedAt == null
                && (o.CreatedAt >= from || o.AppliedAt >= from || o.FirstResponseAt >= from))
            .Select(o => new { o.Id, o.Title, o.Source, o.CreatedAt, o.AppliedAt, o.FirstResponseAt })
            .ToListAsync(ct);

        var ids = opportunities.Select(o => o.Id).ToList();
        var reached = (await db.OpportunityStageChanges
                .Where(c => c.UserId == userId && ids.Contains(c.OpportunityId) && InterviewingOrLater.Contains(c.ToStage))
                .Select(c => new { c.OpportunityId, c.ToStage })
                .Distinct()
                .ToListAsync(ct))
            .ToLookup(c => c.OpportunityId, c => c.ToStage);

        var rows = opportunities
            .Select(o => new OpportunityRow(
                o.Id, o.Title, o.Source, o.CreatedAt, o.AppliedAt, o.FirstResponseAt,
                ReachedInterviewing: reached[o.Id].Any(),
                ReachedOffer: reached[o.Id].Any(OfferOrLater.Contains)))
            .ToList();

        var targetRoles = await db.Profiles
            .Where(p => p.UserId == userId)
            .Select(p => p.TargetRoles)
            .FirstOrDefaultAsync(ct) ?? [];

        return AnalyticsCalculator.Summary(rows, targetRoles, window, zone);
    }

    public async Task<SkillsDto> SkillsAsync(int? days, CancellationToken ct)
    {
        var window = AnalyticsWindow.Ending(clock.UtcNow, CheckedDays(days));
        var userId = currentUser.UserId;
        var (from, to) = (window.From, window.To);

        // The gaps come from the stored fits, so bring them up to date first, as the board does.
        await fitScores.CurrentAsync(ct);

        var postings = db.Opportunities
            .Where(o => o.UserId == userId && o.ArchivedAt == null && o.CreatedAt >= from && o.CreatedAt <= to)
            .Select(o => o.Id);

        var requirements = await db.OpportunityRequirements
            .Where(r => r.UserId == userId && r.Confirmed && r.SkillKey != null && postings.Contains(r.OpportunityId))
            .Select(r => new RequirementRow(r.OpportunityId, r.SkillKey!, r.Kind))
            .ToListAsync(ct);

        var assessments = await db.FitAssessments
            .Where(a => a.UserId == userId && postings.Contains(a.OpportunityId))
            .Select(a => new { a.OpportunityId, a.Outcomes })
            .ToListAsync(ct);
        var gaps = assessments
            .SelectMany(a => a.Outcomes
                .Where(o => o.Coverage == "gap" && o.SkillKey is not null)
                .Select(o => new GapRow(a.OpportunityId, o.SkillKey!, o.Kind)))
            .ToList();

        var profileSkills = await db.ProfileSkills
            .Where(s => s.UserId == userId)
            .Select(s => new { s.Key, s.Name })
            .ToListAsync(ct);
        var profileNames = profileSkills.ToDictionary(s => s.Key, s => s.Name, StringComparer.Ordinal);

        string NameOf(string key) => catalog.Find(key)?.Name ?? profileNames.GetValueOrDefault(key) ?? key;

        return AnalyticsCalculator.Skills(window.Days, requirements, gaps, profileNames.Keys.ToHashSet(StringComparer.Ordinal), NameOf);
    }

    private static int CheckedDays(int? days)
    {
        var value = days ?? AnalyticsRules.DefaultDays;
        return AnalyticsRules.AllowedDays.Contains(value)
            ? value
            : throw new InvalidInputException("analytics.invalid_range", "Choose 30, 90 or 365 days.");
    }

    /// <summary>An IANA name such as <c>Europe/Madrid</c>; UTC when none is given.</summary>
    private static TimeZoneInfo ZoneFor(string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone) || timeZone == "UTC") return TimeZoneInfo.Utc;
        if (timeZone.Length <= 64 && TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var zone)) return zone;
        throw new InvalidInputException("analytics.unknown_time_zone", "That time zone isn't one this server knows.");
    }
}

/// <summary>F7: what to do today, what has gone quiet, and what to work on next.</summary>
public sealed class DashboardHandler(IAnalyticsDbContext db, ICurrentUser currentUser, IClock clock, IFitScores fitScores)
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        // First: bringing the fits up to date can change a suggested priority, which orders "next up".
        var fits = await fitScores.CurrentAsync(ct);
        var userId = currentUser.UserId;
        var now = clock.UtcNow;
        var quietSince = now.AddDays(-AnalyticsRules.QuietAfterDays);

        var mine = db.Opportunities.Where(o => o.UserId == userId && o.ArchivedAt == null);

        var quiet = mine.Where(o => o.Stage == Stages.Applied && o.FirstResponseAt == null && o.AppliedAt <= quietSince
            && !db.Reminders.Any(r => r.UserId == userId && r.OpportunityId == o.Id && r.Status == FollowUpVocabulary.Open));
        var quietTotal = await quiet.CountAsync(ct);
        var quietItems = (await quiet
                .OrderBy(o => o.AppliedAt).ThenBy(o => o.Id)
                .Take(AnalyticsRules.QuietListed)
                .Select(o => new { o.Id, o.Title, CompanyName = o.Company!.Name, AppliedAt = o.AppliedAt!.Value })
                .ToListAsync(ct))
            .Select(o => new QuietItemDto(o.Id, o.Title, o.CompanyName, o.AppliedAt, (int)Math.Floor((now - o.AppliedAt).TotalDays)))
            .ToList();

        var candidates = await mine
            .Where(o => o.Stage == Stages.Saved || o.Stage == Stages.Preparing)
            .Select(o => new { o.Id, o.Title, CompanyName = o.Company!.Name, o.Stage, o.Priority, o.CreatedAt })
            .ToListAsync(ct);
        var nextUp = AnalyticsCalculator.NextUpOrder(candidates.Select(c =>
            {
                var fit = fits.GetValueOrDefault(c.Id);
                return new NextUpItemDto(c.Id, c.Title, c.CompanyName, c.Stage, c.Priority, fit?.Score, fit?.Band, c.CreatedAt);
            }))
            .Take(AnalyticsRules.NextUpListed)
            .ToList();

        var counts = await mine
            .GroupBy(o => o.Stage)
            .Select(g => new { Stage = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Stage, g => g.Count, ct);
        var stages = Stages.All.ToDictionary(s => s, s => counts.GetValueOrDefault(s));

        return new DashboardDto(
            new DashboardPart<QuietItemDto>(quietItems, quietTotal),
            new DashboardPart<NextUpItemDto>(nextUp, candidates.Count),
            stages);
    }
}
