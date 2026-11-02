using JobHunter.Domain.Contacts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace JobHunter.Application.Contacts;

/// <summary>The Contacts module's tables (spec 02 §3, one database port per module).</summary>
public interface IContactsDbContext
{
    DbSet<Contact> Contacts { get; }
    DbSet<ContactOpportunity> ContactOpportunities { get; }
    DbSet<Interaction> Interactions { get; }

    /// <summary>Used to check a client's <c>version</c> against the stored one on save.</summary>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
