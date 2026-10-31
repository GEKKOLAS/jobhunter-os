using JobHunter.Domain.Common;

namespace JobHunter.Domain.Identity;

public sealed class User : IHasTimestamps
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>Case-insensitive and unique (citext).</summary>
    public required string Email { get; set; }

    /// <summary>Argon2id, parameters encoded with the hash.</summary>
    public required string PasswordHash { get; set; }

    public required string DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<UserSession> Sessions { get; init; } = [];
}
