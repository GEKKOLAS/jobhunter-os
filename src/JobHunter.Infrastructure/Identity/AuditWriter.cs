using System.Text.Json;
using JobHunter.Application.Common;
using JobHunter.Domain.Identity;
using JobHunter.Infrastructure.Persistence;

namespace JobHunter.Infrastructure.Identity;

internal sealed class AuditWriter(AppDbContext db, IRequestContext requestContext, IClock clock) : IAuditWriter
{
    public void Add(string action, Guid? userId, string? entityType = null, Guid? entityId = null, object? metadata = null) =>
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Ip = requestContext.Ip,
            UserAgent = requestContext.UserAgent,
            Metadata = metadata is null ? null : JsonSerializer.SerializeToDocument(metadata),
            CreatedAt = clock.UtcNow,
        });
}
