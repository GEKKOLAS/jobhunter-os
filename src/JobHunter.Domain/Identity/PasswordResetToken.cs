namespace JobHunter.Domain.Identity;

/// <summary>Single-use, short-lived; stored as the SHA-256 of the token in the link.</summary>
public sealed class PasswordResetToken
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required byte[] TokenHash { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? UsedAt { get; set; }

    public User? User { get; init; }

    public bool IsUsable(DateTimeOffset now) => UsedAt is null && now < ExpiresAt;
}
