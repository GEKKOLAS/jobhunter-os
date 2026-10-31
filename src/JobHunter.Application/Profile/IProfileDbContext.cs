using JobHunter.Domain.Profile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace JobHunter.Application.Profile;

/// <summary>The Profile module's tables (spec 02 §3, one database port per module).</summary>
public interface IProfileDbContext
{
    DbSet<CandidateProfile> Profiles { get; }
    DbSet<ProfileSkill> ProfileSkills { get; }
    DbSet<EvidenceItem> EvidenceItems { get; }

    /// <summary>Used to check a client's <c>version</c> against the stored one on save.</summary>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
