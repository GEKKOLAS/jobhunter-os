using JobHunter.Domain.Profile;

namespace JobHunter.Application.Profile;

public sealed record LanguageDto(string Name, string Level);

public sealed record ProfileDto(
    string Headline, string Summary, string Location, IReadOnlyList<string> TargetRoles, string? Seniority,
    IReadOnlyList<string> WorkModes, IReadOnlyList<LanguageDto> Languages, uint Version, DateTimeOffset? UpdatedAt)
{
    /// <summary>Before the first save there is no row: an empty profile at version 0.</summary>
    public static readonly ProfileDto Empty = new("", "", "", [], null, [], [], 0, null);

    public static ProfileDto From(CandidateProfile p) => new(
        p.Headline, p.Summary, p.Location, p.TargetRoles, p.Seniority, p.WorkModes,
        p.Languages.Select(l => new LanguageDto(l.Name, l.Level)).ToList(), p.Version, p.UpdatedAt);
}

/// <param name="EvidenceCount">How many evidence items cite the skill; 0 shows as "no evidence yet".</param>
public sealed record SkillDto(
    Guid Id, string Name, string Key, string Category, int Level, decimal Years, int? LastUsedYear,
    int EvidenceCount, uint Version)
{
    public static SkillDto From(ProfileSkill s, int evidenceCount) =>
        new(s.Id, s.Name, s.Key, s.Category, s.Level, s.Years, s.LastUsedYear, evidenceCount, s.Version);
}

public sealed record LinkDto(string Label, string Url);

public sealed record EvidenceDto(
    Guid Id, string Kind, string Title, string? Organization, DateOnly? StartDate, DateOnly? EndDate,
    string Description, string? Outcome, IReadOnlyList<LinkDto> Links, IReadOnlyList<Guid> SkillIds,
    uint Version, DateTimeOffset CreatedAt)
{
    public static EvidenceDto From(EvidenceItem e) => new(
        e.Id, e.Kind, e.Title, e.Organization, e.StartDate, e.EndDate, e.Description, e.Outcome,
        e.Links.Select(l => new LinkDto(l.Label, l.Url)).ToList(), e.SkillIds, e.Version, e.CreatedAt);
}

/// <summary>Audit action codes of the Profile module.</summary>
public static class ProfileAudit
{
    public const string ProfileUpdated = "profile.updated";
    public const string SkillCreated = "skill.created";
    public const string SkillUpdated = "skill.updated";
    public const string SkillDeleted = "skill.deleted";
    public const string EvidenceCreated = "evidence.created";
    public const string EvidenceUpdated = "evidence.updated";
    public const string EvidenceDeleted = "evidence.deleted";
}
