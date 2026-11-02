using JobHunter.Domain.Common;

namespace JobHunter.Domain.Matching;

/// <summary>The current fit of one opportunity, kept so other screens and analytics can read it without recomputing.</summary>
public sealed class FitAssessment : IHasTimestamps
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required Guid OpportunityId { get; init; }

    public int? Score { get; set; }
    public string? Band { get; set; }

    /// <summary>Why there is no score; see <see cref="FitAlgorithm"/>.</summary>
    public string? MissingReason { get; set; }

    public List<RequirementOutcome> Outcomes { get; set; } = [];
    public List<FitNote> Notes { get; set; } = [];

    public int AlgorithmVersion { get; set; }

    /// <summary>When the result last changed: reads that compute the same answer don't write.</summary>
    public DateTimeOffset ComputedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>How one requirement came out, with what the profile has, so the UI can say why.</summary>
public sealed class RequirementOutcome
{
    public required string Text { get; set; }
    public required string Kind { get; set; }
    public string? SkillKey { get; set; }
    public string? SkillName { get; set; }
    public int? YearsMin { get; set; }

    /// <summary><c>covered</c>, <c>partial</c>, <c>gap</c> or <c>not_scored</c>.</summary>
    public required string Coverage { get; set; }

    /// <summary><c>meets</c>, <c>under_level</c>, <c>under_years</c>, <c>under_level_and_years</c>, <c>related</c>, <c>missing</c> or <c>no_skill</c>.</summary>
    public required string Reason { get; set; }

    public int? YourLevel { get; set; }
    public double? YourYears { get; set; }

    /// <summary>The skill of yours in the same catalog family, when that's all that matched.</summary>
    public string? RelatedSkill { get; set; }
}

/// <summary>A mismatch worth knowing that isn't in the score.</summary>
public sealed class FitNote
{
    /// <summary><c>work_mode</c>, <c>location</c> or <c>seniority</c>.</summary>
    public required string Code { get; set; }

    public required string Posting { get; set; }
    public required string Profile { get; set; }
}
