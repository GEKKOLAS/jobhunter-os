using System.Text.Json;
using JobHunter.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.Identity;

public sealed record AuditLogQuery(string? Action, DateTimeOffset? From, DateTimeOffset? To, int Page, int PageSize);

public sealed record AuditLogDto(
    Guid Id, string Action, string? EntityType, Guid? EntityId, string? Ip, JsonElement? Metadata, DateTimeOffset CreatedAt);

/// <summary>The caller's own events, newest first. Nobody can read another user's trail.</summary>
public sealed class AuditLogsHandler(IIdentityDbContext db, ICurrentUser currentUser)
{
    public async Task<Paged<AuditLogDto>> ListAsync(AuditLogQuery query, CancellationToken ct)
    {
        var (page, pageSize) = Paged<AuditLogDto>.Clamp(query.Page, query.PageSize);

        var q = db.AuditLogs.Where(a => a.UserId == currentUser.UserId);
        if (!string.IsNullOrWhiteSpace(query.Action)) q = q.Where(a => a.Action == query.Action);
        if (query.From is { } from) q = q.Where(a => a.CreatedAt >= from);
        if (query.To is { } to) q = q.Where(a => a.CreatedAt <= to);

        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows
            .Select(a => new AuditLogDto(
                a.Id, a.Action, a.EntityType, a.EntityId, a.Ip, a.Metadata?.RootElement.Clone(), a.CreatedAt))
            .ToList();

        return new Paged<AuditLogDto>(items, page, pageSize, total);
    }
}
