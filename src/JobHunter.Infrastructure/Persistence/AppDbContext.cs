using JobHunter.Application.Identity;
using JobHunter.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Infrastructure.Persistence;

/// <summary>
/// One database, one context. It implements each module's narrow port
/// (<see cref="IIdentityDbContext"/>, …), so handlers only see their own module's tables
/// (spec 02 §3). Configurations are registered from this assembly.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IIdentityDbContext
{
    // Identity
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // citext: case-insensitive emails without lower() everywhere.
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
