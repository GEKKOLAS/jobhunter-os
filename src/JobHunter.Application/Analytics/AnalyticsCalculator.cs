using System.Globalization;
using System.Text;
using JobHunter.Domain.Matching;

namespace JobHunter.Application.Analytics;

/// <summary>
/// The definitions of spec 03, Analytics, as pure functions over rows: no database, no clock,
/// so every definition has its own unit test.
/// </summary>
public static class AnalyticsCalculator
{
    public static SummaryDto Summary(
        IReadOnlyList<OpportunityRow> rows, IReadOnlyList<string> targetRoles, AnalyticsWindow window, TimeZoneInfo zone)
    {
        var saved = rows.Count(r => window.Contains(r.CreatedAt));
        var applications = rows.Where(r => window.Contains(r.AppliedAt)).ToList();
        var total = applications.Count;
        var responded = applications.Count(r => r.FirstResponseAt is not null);
        var interviewing = applications.Count(r => r.ReachedInterviewing);
        var offers = applications.Count(r => r.ReachedOffer);

        var cutoff = window.To.AddDays(-AnalyticsRules.NoResponseAfterDays);
        var oldEnough = applications.Where(r => r.AppliedAt <= cutoff).ToList();

        var rates = new SummaryRates(
            Response: Rate.Of(responded, total),
            Interview: Rate.Of(interviewing, total),
            Offer: Rate.Of(offers, total),
            NoResponse: Rate.Of(oldEnough.Count(r => r.FirstResponseAt is null), oldEnough.Count));

        IReadOnlyList<FunnelStep> funnel =
        [
            new("saved", saved, null),
            new("applied", total, null),
            new("responded", responded, rates.Response),
            new("interviewing", interviewing, rates.Interview),
            new("offer", offers, rates.Offer),
        ];

        return new SummaryDto(
            window.Days, window.From, window.To, saved, total, rates,
            TimeToFirstResponse(applications), funnel, Weekly(rows, window, zone), BySource(applications), ByRole(applications, targetRoles));
    }

    /// <summary>Median days from applying to the first answer, responded applications only.</summary>
    public static MedianDays TimeToFirstResponse(IReadOnlyList<OpportunityRow> applications)
    {
        var days = applications
            .Where(r => r is { AppliedAt: not null, FirstResponseAt: not null })
            .Select(r => Math.Max(0, (r.FirstResponseAt!.Value - r.AppliedAt!.Value).TotalDays))
            .Order()
            .ToList();
        if (days.Count < AnalyticsRules.MinSample) return new MedianDays(null, days.Count);

        var middle = days.Count / 2;
        var median = days.Count % 2 == 1 ? days[middle] : (days[middle - 1] + days[middle]) / 2;
        return new MedianDays(Math.Round(median, 1, MidpointRounding.AwayFromZero), days.Count);
    }

    /// <summary>Every week overlapping the range, Monday-based in the viewer's zone, counting only moments inside the range.</summary>
    public static IReadOnlyList<WeekDto> Weekly(IReadOnlyList<OpportunityRow> rows, AnalyticsWindow window, TimeZoneInfo zone)
    {
        var fromLocal = TimeZoneInfo.ConvertTime(window.From, zone).DateTime;
        var toLocal = TimeZoneInfo.ConvertTime(window.To, zone).DateTime;
        var first = MondayOf(DateOnly.FromDateTime(fromLocal));
        var weeks = (MondayOf(DateOnly.FromDateTime(toLocal)).DayNumber - first.DayNumber) / 7 + 1;

        var applications = new int[weeks];
        var responses = new int[weeks];
        int WeekOf(DateTimeOffset at) =>
            (MondayOf(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime)).DayNumber - first.DayNumber) / 7;

        foreach (var row in rows)
        {
            if (window.Contains(row.AppliedAt)) applications[WeekOf(row.AppliedAt!.Value)]++;
            if (window.Contains(row.FirstResponseAt)) responses[WeekOf(row.FirstResponseAt!.Value)]++;
        }

        return Enumerable.Range(0, weeks)
            .Select(i =>
            {
                var start = first.AddDays(7 * i);
                var startLocal = start.ToDateTime(TimeOnly.MinValue);
                return new WeekDto(start, applications[i], responses[i], Partial: startLocal < fromLocal || startLocal.AddDays(7) > toLocal);
            })
            .ToList();
    }

    public static IReadOnlyList<BreakdownDto> BySource(IReadOnlyList<OpportunityRow> applications) =>
        applications
            .GroupBy(a => a.Source)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => Breakdown(g.Key, g.ToList()))
            .ToList();

    /// <summary>Each target role in the profile's order, then "other" (null) when something matched none.</summary>
    public static IReadOnlyList<BreakdownDto> ByRole(IReadOnlyList<OpportunityRow> applications, IReadOnlyList<string> targetRoles)
    {
        var roles = targetRoles.Select(role => (Role: role, Words: Words(role))).ToList();
        var buckets = roles.Select(_ => new List<OpportunityRow>()).ToList();
        var other = new List<OpportunityRow>();

        foreach (var application in applications)
        {
            var title = Words(application.Title).ToHashSet(StringComparer.Ordinal);
            var index = roles.FindIndex(r => r.Words.Count > 0 && r.Words.All(title.Contains));
            (index >= 0 ? buckets[index] : other).Add(application);
        }

        var result = roles.Select((r, i) => Breakdown(r.Role, buckets[i])).ToList();
        if (other.Count > 0) result.Add(Breakdown(null, other));
        return result;
    }

    /// <summary>A title or role as words: runs of letters and digits, lowercased, accents dropped.</summary>
    public static IReadOnlyList<string> Words(string text)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c))
            {
                word.Append(char.ToLowerInvariant(c));
            }
            else if (word.Length > 0)
            {
                words.Add(word.ToString());
                word.Clear();
            }
        }
        if (word.Length > 0) words.Add(word.ToString());
        return words;
    }

    /// <summary>Most requested skills and most frequent gaps; each posting counts once per skill.</summary>
    public static SkillsDto Skills(
        int days,
        IReadOnlyList<RequirementRow> requirements,
        IReadOnlyList<GapRow> gaps,
        IReadOnlySet<string> profileKeys,
        Func<string, string> nameOf)
    {
        var requested = Tally(requirements.Select(r => (r.OpportunityId, r.SkillKey, r.Kind)))
            .Select(t => new RequestedSkillDto(t.Key, nameOf(t.Key), t.Postings, t.Must, t.Postings - t.Must, profileKeys.Contains(t.Key)))
            .OrderByDescending(s => s.Postings)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Key, StringComparer.Ordinal)
            .Take(AnalyticsRules.TopRequested)
            .ToList();

        var frequentGaps = Tally(gaps.Select(g => (g.OpportunityId, g.SkillKey, g.Kind)))
            .Select(t => new GapDto(t.Key, nameOf(t.Key), t.Postings, t.Must, t.Postings - t.Must))
            .OrderByDescending(g => g.Postings)
            .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Take(AnalyticsRules.TopGaps)
            .ToList();

        var postings = requirements.Select(r => r.OpportunityId).Distinct().Count();
        return new SkillsDto(days, postings, requested, frequentGaps);
    }

    /// <summary>Per skill: how many postings name it, and in how many of those it's a must.</summary>
    private static IEnumerable<(string Key, int Postings, int Must)> Tally(IEnumerable<(Guid OpportunityId, string Key, string Kind)> rows) =>
        rows
            .GroupBy(r => r.Key, StringComparer.Ordinal)
            .Select(skill =>
            {
                var perPosting = skill.GroupBy(r => r.OpportunityId).ToList();
                var must = perPosting.Count(p => p.Any(r => r.Kind == RequirementVocabulary.Must));
                return (skill.Key, perPosting.Count, must);
            });

    private static readonly Dictionary<string, int> PriorityRank = new() { ["high"] = 0, ["normal"] = 1, ["low"] = 2 };

    /// <summary>What to work on next: priority, then fit (unscored last), then the oldest.</summary>
    public static IEnumerable<NextUpItemDto> NextUpOrder(IEnumerable<NextUpItemDto> items) =>
        items
            .OrderBy(i => PriorityRank.GetValueOrDefault(i.Priority, 1))
            .ThenBy(i => i.Score is null ? 1 : 0)
            .ThenByDescending(i => i.Score)
            .ThenBy(i => i.CreatedAt)
            .ThenBy(i => i.Id);

    private static BreakdownDto Breakdown(string? key, IReadOnlyCollection<OpportunityRow> applications)
    {
        var responses = applications.Count(a => a.FirstResponseAt is not null);
        var interviews = applications.Count(a => a.ReachedInterviewing);
        return new BreakdownDto(
            key, applications.Count, responses, interviews, Rate.Of(responses, applications.Count), Rate.Of(interviews, applications.Count));
    }

    private static DateOnly MondayOf(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
}
