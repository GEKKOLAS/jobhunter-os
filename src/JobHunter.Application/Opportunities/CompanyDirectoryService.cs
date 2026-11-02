using JobHunter.Application.Common;
using JobHunter.Application.Opportunities.Contracts;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.Opportunities;

internal sealed class CompanyDirectoryService(IOpportunitiesDbContext db, ICurrentUser currentUser, CompanyResolver resolver) : ICompanyDirectory
{
    public async Task<CompanyRef> ResolveAsync(Guid? companyId, string? companyName, CancellationToken ct)
    {
        var company = await resolver.ResolveAsync(companyId, companyName, ct);
        return new CompanyRef(company.Id, company.Name);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        return await db.Companies.AsNoTracking()
            .Where(c => c.UserId == currentUser.UserId && ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
    }

    public async Task<IReadOnlyList<Guid>> MatchingAsync(string term, CancellationToken ct)
    {
        var lowered = term.Trim().ToLower();
        return await db.Companies.AsNoTracking()
            .Where(c => c.UserId == currentUser.UserId && c.Name.ToLower().Contains(lowered))
            .Select(c => c.Id)
            .ToListAsync(ct);
    }
}
