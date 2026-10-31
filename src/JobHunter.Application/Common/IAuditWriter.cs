namespace JobHunter.Application.Common;

/// <summary>
/// Stages an audit entry on the current unit of work. It is saved by the caller's own
/// SaveChangesAsync, so the action and its audit entry commit together or not at all.
/// </summary>
public interface IAuditWriter
{
    void Add(string action, Guid? userId, string? entityType = null, Guid? entityId = null, object? metadata = null);
}
