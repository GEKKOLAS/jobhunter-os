namespace JobHunter.Application.Analytics;

/// <summary>The thresholds of spec 03, Analytics, in one place.</summary>
public static class AnalyticsRules
{
    /// <summary>Below this many, a rate or a median is withheld: too few to say.</summary>
    public const int MinSample = 5;

    public const int NoResponseAfterDays = 30;
    public const int QuietAfterDays = 10;

    public const int DefaultDays = 90;
    public static readonly IReadOnlyList<int> AllowedDays = [30, 90, 365];

    public const int TopRequested = 15;
    public const int TopGaps = 10;
    public const int QuietListed = 10;
    public const int NextUpListed = 5;
}

/// <summary>A rate with what it was computed from. <see cref="Value"/> is null under <see cref="AnalyticsRules.MinSample"/>.</summary>
public sealed record Rate(int Numerator, int Denominator, double? Value)
{
    public static Rate Of(int numerator, int denominator) =>
        new(numerator, denominator,
            denominator < AnalyticsRules.MinSample ? null : Math.Round((double)numerator / denominator, 4, MidpointRounding.AwayFromZero));
}

/// <summary>The last <see cref="Days"/> days up to <see cref="To"/>.</summary>
public sealed record AnalyticsWindow(int Days, DateTimeOffset From, DateTimeOffset To)
{
    public static AnalyticsWindow Ending(DateTimeOffset now, int days) => new(days, now.AddDays(-days), now);

    public bool Contains(DateTimeOffset? at) => at is { } t && t >= From && t <= To;
}

/// <summary>One opportunity as analytics sees it: its dates and how far it got.</summary>
public sealed record OpportunityRow(
    Guid Id,
    string Title,
    string Source,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AppliedAt,
    DateTimeOffset? FirstResponseAt,
    bool ReachedInterviewing,
    bool ReachedOffer);

public sealed record MedianDays(double? Days, int Count);

public sealed record SummaryRates(Rate Response, Rate Interview, Rate Offer, Rate NoResponse);

/// <param name="Step"><c>saved</c>, <c>applied</c>, <c>responded</c>, <c>interviewing</c> or <c>offer</c>.</param>
/// <param name="Rate">Over applications; null for the two steps it would mean nothing for.</param>
public sealed record FunnelStep(string Step, int Count, Rate? Rate);

/// <param name="Partial">The range starts or ends inside this week, so it holds fewer days.</param>
public sealed record WeekDto(DateOnly WeekStart, int Applications, int Responses, bool Partial);

/// <param name="Key">The source, or the target role (null for applications matching none).</param>
public sealed record BreakdownDto(string? Key, int Applications, int Responses, int Interviews, Rate ResponseRate, Rate InterviewRate);

public sealed record SummaryDto(
    int Days,
    DateTimeOffset From,
    DateTimeOffset To,
    int Saved,
    int Applications,
    SummaryRates Rates,
    MedianDays TimeToFirstResponse,
    IReadOnlyList<FunnelStep> Funnel,
    IReadOnlyList<WeekDto> Weekly,
    IReadOnlyList<BreakdownDto> BySource,
    IReadOnlyList<BreakdownDto> ByRole);

/// <summary>A confirmed requirement naming a skill, from one posting.</summary>
public sealed record RequirementRow(Guid OpportunityId, string SkillKey, string Kind);

/// <summary>A requirement of a posting that the current fit marks as a gap.</summary>
public sealed record GapRow(Guid OpportunityId, string SkillKey, string Kind);

public sealed record RequestedSkillDto(string Key, string Name, int Postings, int Must, int Nice, bool InProfile);

public sealed record GapDto(string Key, string Name, int Postings, int Must, int Nice);

/// <param name="Postings">Postings in the range with at least one confirmed requirement naming a skill.</param>
public sealed record SkillsDto(int Days, int Postings, IReadOnlyList<RequestedSkillDto> Requested, IReadOnlyList<GapDto> Gaps);

public sealed record QuietItemDto(Guid Id, string Title, string CompanyName, DateTimeOffset AppliedAt, int DaysWaiting);

public sealed record NextUpItemDto(
    Guid Id, string Title, string CompanyName, string Stage, string Priority, int? Score, string? Band, DateTimeOffset CreatedAt);

public sealed record DashboardPart<T>(IReadOnlyList<T> Items, int Total);

/// <param name="Stages">Every stage, open ones in pipeline order, then the closed ones.</param>
public sealed record DashboardDto(DashboardPart<QuietItemDto> Quiet, DashboardPart<NextUpItemDto> NextUp, IReadOnlyDictionary<string, int> Stages);
