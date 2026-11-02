using JobHunter.Domain.Common;

namespace JobHunter.Domain.Matching;

/// <summary>The closed value sets and limits of requirements (spec 03, Matching). Stored as text.</summary>
public static class RequirementVocabulary
{
    public const string Must = "must";
    public const string Nice = "nice";
    public static readonly IReadOnlyList<string> Kinds = [Must, Nice];

    public const string OriginRules = "rules";
    public const string OriginAi = "ai";
    public const string OriginUser = "user";

    public const int MaxPerOpportunity = 50;
    public const int MaxText = 300;
    public const int MaxSkillName = 60;
    public const int MaxYears = 40;
}

/// <summary>One thing a posting asks for. Counts toward the fit only once the user has confirmed it.</summary>
public sealed class OpportunityRequirement : IHasTimestamps
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required Guid OpportunityId { get; init; }

    public required string Text { get; set; }

    /// <summary>One of <see cref="RequirementVocabulary.Kinds"/>.</summary>
    public required string Kind { get; set; }

    /// <summary>The skill catalog's key, like a profile skill's. Null when it isn't about one skill: listed, not scored.</summary>
    public string? SkillKey { get; set; }

    public int? YearsMin { get; set; }

    /// <summary><c>rules</c>, <c>ai</c> or <c>user</c>.</summary>
    public required string Origin { get; init; }

    public bool Confirmed { get; set; }

    /// <summary>Keeps the posting's order, or the user's.</summary>
    public int Position { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
