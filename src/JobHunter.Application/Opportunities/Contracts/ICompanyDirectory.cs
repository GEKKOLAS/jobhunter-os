namespace JobHunter.Application.Opportunities.Contracts;

public sealed record CompanyRef(Guid Id, string Name);

/// <summary>The signed-in user's companies, for modules that refer to one (a contact works somewhere).</summary>
public interface ICompanyDirectory
{
    /// <summary>
    /// The company with that id; otherwise the one with that name, created when there is none.
    /// Doesn't save: the request's one unit of work saves it with the caller's changes.
    /// </summary>
    Task<CompanyRef> ResolveAsync(Guid? companyId, string? companyName, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>Companies whose name contains the term, for searches that include the company.</summary>
    Task<IReadOnlyList<Guid>> MatchingAsync(string term, CancellationToken ct);
}
