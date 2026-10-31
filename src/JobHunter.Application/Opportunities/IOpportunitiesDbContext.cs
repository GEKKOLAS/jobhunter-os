using JobHunter.Domain.Opportunities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace JobHunter.Application.Opportunities;

/// <summary>The Opportunities module's tables (spec 02 §3, one database port per module).</summary>
public interface IOpportunitiesDbContext
{
    DbSet<Company> Companies { get; }
    DbSet<Opportunity> Opportunities { get; }
    DbSet<OpportunityStageChange> OpportunityStageChanges { get; }

    /// <summary>Used to check a client's <c>version</c> against the stored one on save.</summary>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
