using JobHunter.Domain.Common;

namespace JobHunter.Domain.Profile;

/// <summary>
/// Something that backs up a claim: a project, a role, a certification, a degree. Every
/// word here is the user's own; drafts and matching may cite it but never embellish it.
/// </summary>
public sealed class EvidenceItem : IHasTimestamps
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }

    /// <summary>One of <see cref="ProfileVocabulary.EvidenceKinds"/>.</summary>
    public required string Kind { get; set; }

    public required string Title { get; set; }
    public string? Organization { get; set; }
    public DateOnly? StartDate { get; set; }

    /// <summary>Null while ongoing.</summary>
    public DateOnly? EndDate { get; set; }

    public string Description { get; set; } = "";

    /// <summary>What came of it, in the user's words. Never generated.</summary>
    public string? Outcome { get; set; }

    public List<EvidenceLink> Links { get; set; } = [];

    /// <summary>The user's <see cref="ProfileSkill"/> ids this item demonstrates.</summary>
    public List<Guid> SkillIds { get; set; } = [];

    public uint Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class EvidenceLink
{
    public required string Label { get; set; }
    public required string Url { get; set; }
}
