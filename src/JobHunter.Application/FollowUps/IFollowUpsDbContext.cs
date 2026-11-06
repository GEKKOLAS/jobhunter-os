using JobHunter.Domain.FollowUps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace JobHunter.Application.FollowUps;

/// <summary>The FollowUps module's tables (spec 02 §3, one database port per module).</summary>
public interface IFollowUpsDbContext
{
    DbSet<Reminder> Reminders { get; }
    DbSet<Draft> Drafts { get; }
    DbSet<Notification> Notifications { get; }

    /// <summary>Used to check a client's <c>version</c> against the stored one on save.</summary>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class FollowUpsOptions
{
    public const string SectionName = "FollowUps";

    /// <summary>How long after applying the follow-up reminder comes due.</summary>
    public int AfterApplyDays { get; init; } = 7;
}
