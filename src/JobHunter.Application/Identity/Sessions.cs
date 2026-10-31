using JobHunter.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.Identity;

public sealed class SessionsHandler(IIdentityDbContext db, ICurrentUser currentUser, IAuditWriter audit)
{
    public async Task<IReadOnlyList<SessionDto>> ListAsync(CancellationToken ct)
    {
        var sessions = await db.UserSessions
            .Where(s => s.UserId == currentUser.UserId)
            .OrderByDescending(s => s.LastSeenAt)
            .ToListAsync(ct);

        return sessions
            .Select(s => new SessionDto(s.Id, s.Ip, s.UserAgent, s.CreatedAt, s.LastSeenAt, s.Id == currentUser.SessionId))
            .ToList();
    }

    /// <summary>Ends the current session.</summary>
    public async Task LogoutAsync(CancellationToken ct)
    {
        await db.UserSessions
            .Where(s => s.Id == currentUser.SessionId && s.UserId == currentUser.UserId)
            .ExecuteDeleteAsync(ct);

        audit.Add(IdentityAudit.Logout, currentUser.UserId);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Ends every session of the user, this one included.</summary>
    public async Task LogoutAllAsync(CancellationToken ct)
    {
        await db.UserSessions.Where(s => s.UserId == currentUser.UserId).ExecuteDeleteAsync(ct);

        audit.Add(IdentityAudit.Logout, currentUser.UserId, metadata: new { everywhere = true });
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeAsync(Guid sessionId, CancellationToken ct)
    {
        var deleted = await db.UserSessions
            .Where(s => s.Id == sessionId && s.UserId == currentUser.UserId)
            .ExecuteDeleteAsync(ct);
        if (deleted == 0) throw new NotFoundException();

        audit.Add(IdentityAudit.SessionRevoked, currentUser.UserId, "user_session", sessionId);
        await db.SaveChangesAsync(ct);
    }
}
