namespace JobHunter.Domain.Profile;

/// <summary>The closed value sets of the Profile module (spec 03, Profile). Stored as text.</summary>
public static class ProfileVocabulary
{
    public static readonly IReadOnlyList<string> Seniorities = ["junior", "mid", "senior", "lead", "principal"];
    public static readonly IReadOnlyList<string> WorkModes = ["remote", "hybrid", "onsite"];
    public static readonly IReadOnlyList<string> LanguageLevels = ["basic", "conversational", "professional", "native"];

    public static readonly IReadOnlyList<string> SkillCategories =
        ["language", "framework", "data", "cloud", "tooling", "practice", "domain", "soft"];

    public static readonly IReadOnlyList<string> EvidenceKinds = ["project", "role", "certification", "education"];

    public const int MaxSkills = 150;
    public const int MaxEvidence = 100;
}
