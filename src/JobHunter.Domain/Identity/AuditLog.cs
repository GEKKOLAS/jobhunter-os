using System.Text.Json;

namespace JobHunter.Domain.Identity;

/// <summary>
/// Append-only trail of security events and state changes. Every module writes to it
/// through <c>IAuditWriter</c>; Identity owns the table and the read endpoint.
/// </summary>
public sealed class AuditLog
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid? UserId { get; init; }

    /// <summary>A dotted code such as <c>auth.login</c> or <c>opportunity.stage_changed</c>.</summary>
    public required string Action { get; init; }

    public string? EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public string? Ip { get; init; }
    public string? UserAgent { get; init; }
    public JsonDocument? Metadata { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
