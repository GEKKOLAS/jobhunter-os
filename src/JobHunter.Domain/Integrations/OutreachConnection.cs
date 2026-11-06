using JobHunter.Domain.Common;

namespace JobHunter.Domain.Integrations;

/// <summary>
/// The user's link to their Outreach Studio (spec 02 §7). The API key is stored only encrypted,
/// under a data key that's itself wrapped by a versioned key from configuration (spec 02 §5).
/// </summary>
public sealed class OutreachConnection : IHasTimestamps
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }

    public required string BaseUrl { get; set; }
    public required Guid TemplateVersionId { get; set; }

    public required byte[] WrappedDataKey { get; set; }
    public required int KeyVersion { get; set; }
    public required byte[] ApiKeyCiphertext { get; set; }
    public required byte[] ApiKeyNonce { get; set; }

    /// <summary>The key's last four characters, so the user can tell which key is saved.</summary>
    public required string KeyHint { get; set; }

    public DateTimeOffset? LastTestedAt { get; set; }
    public bool? LastTestOk { get; set; }

    public uint Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
