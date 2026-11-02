namespace JobHunter.Application.Opportunities.Contracts;

/// <summary>What another module may know about one of the signed-in user's opportunities.</summary>
/// <param name="CanTakePriority">Open and unarchived: the only ones a suggested priority applies to.</param>
public sealed record OpportunityFacts(
    Guid Id, string Title, string CompanyName, string? Location, string? WorkMode, string Stage, string Priority,
    bool CanTakePriority, bool Archived, DateTimeOffset CreatedAt);

public interface IOpportunityFacts
{
    Task<OpportunityFacts?> FindAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<OpportunityFacts>> ListAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>The posting's plain text, or null when the opportunity isn't the user's.</summary>
    Task<string?> PostingTextAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Applies suggested priorities (opportunity id → priority), leaving alone any the user set.
    /// Doesn't save: the request's one unit of work saves it with the caller's changes.
    /// </summary>
    Task SuggestPrioritiesAsync(IReadOnlyDictionary<Guid, string> suggestions, CancellationToken ct);
}
