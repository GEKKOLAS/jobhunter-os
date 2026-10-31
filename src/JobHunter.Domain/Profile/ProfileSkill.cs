using JobHunter.Domain.Common;

namespace JobHunter.Domain.Profile;

/// <summary>A skill as the user rates it. Matching compares postings against these.</summary>
public sealed class ProfileSkill : IHasTimestamps
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }

    /// <summary>As the user writes it, e.g. "ASP.NET Core".</summary>
    public required string Name { get; set; }

    /// <summary>Unique per user; see <see cref="SkillKey"/>. Renaming can change it.</summary>
    public required string Key { get; set; }

    /// <summary>One of <see cref="ProfileVocabulary.SkillCategories"/>.</summary>
    public required string Category { get; set; }

    /// <summary>1 (aware) to 5 (expert).</summary>
    public int Level { get; set; }

    /// <summary>Years of real use, one decimal.</summary>
    public decimal Years { get; set; }

    public int? LastUsedYear { get; set; }

    public uint Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
