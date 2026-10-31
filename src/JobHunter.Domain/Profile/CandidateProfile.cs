using JobHunter.Domain.Common;

namespace JobHunter.Domain.Profile;

/// <summary>
/// What the user is looking for and how they describe themselves: the side of the match
/// that doesn't change per posting. One per user, created empty on first read.
/// </summary>
public sealed class CandidateProfile : IHasTimestamps
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }

    public string Headline { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Location { get; set; } = "";
    public List<string> TargetRoles { get; set; } = [];

    /// <summary>One of <see cref="ProfileVocabulary.Seniorities"/>, or null when not stated.</summary>
    public string? Seniority { get; set; }

    public List<string> WorkModes { get; set; } = [];
    public List<SpokenLanguage> Languages { get; set; } = [];

    /// <summary>PostgreSQL <c>xmin</c>: a stale write is a conflict, not a silent overwrite.</summary>
    public uint Version { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class SpokenLanguage
{
    public required string Name { get; set; }

    /// <summary>One of <see cref="ProfileVocabulary.LanguageLevels"/>.</summary>
    public required string Level { get; set; }
}
