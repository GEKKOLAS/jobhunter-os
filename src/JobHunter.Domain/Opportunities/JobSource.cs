using JobHunter.Domain.Common;

namespace JobHunter.Domain.Opportunities;

/// <summary>A company's public job board the user follows. Listing it is read-only; the user picks what to save.</summary>
public sealed class JobSource : IHasTimestamps
{
    public const int MaxPerUser = 50;

    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }

    /// <summary><c>greenhouse</c>, <c>lever</c> or <c>ashby</c>.</summary>
    public required string Provider { get; init; }

    /// <summary>The company's token at that provider; see <see cref="ImportRules.IsValidBoardToken"/>.</summary>
    public required string Board { get; init; }

    /// <summary>What the user calls it. Names the company when the provider doesn't.</summary>
    public required string Label { get; set; }

    public DateTimeOffset? LastCheckedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
