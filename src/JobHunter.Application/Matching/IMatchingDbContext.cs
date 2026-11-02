using JobHunter.Domain.Matching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace JobHunter.Application.Matching;

/// <summary>The Matching module's tables (spec 02 §3, one database port per module).</summary>
public interface IMatchingDbContext
{
    DbSet<OpportunityRequirement> OpportunityRequirements { get; }
    DbSet<FitAssessment> FitAssessments { get; }

    /// <summary>Used to drop a write that lost a race (see <see cref="FitHandler"/>).</summary>
    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
