using JobHunter.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.Identity;

/// <summary>
/// The Identity module's view of the database: its own tables and nothing else. Each
/// module gets a port like this, so a handler can't reach another module's tables
/// (spec 02 §3). The single AppDbContext implements them all.
/// </summary>
public interface IIdentityDbContext
{
    DbSet<User> Users { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
