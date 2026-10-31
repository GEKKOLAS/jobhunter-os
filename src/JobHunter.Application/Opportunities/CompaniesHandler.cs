using FluentValidation;
using JobHunter.Application.Common;
using JobHunter.Domain.Opportunities;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.Opportunities;

/// <summary>Turns an opportunity's company reference (an id, or a name) into a company row.</summary>
public sealed class CompanyResolver(IOpportunitiesDbContext db, ICurrentUser currentUser, IAuditWriter audit)
{
    /// <summary>
    /// The user's company with that id; otherwise the one with that name (case-insensitive,
    /// citext), created when there is none. The caller saves.
    /// </summary>
    public async Task<Company> ResolveAsync(Guid? companyId, string? companyName, CancellationToken ct)
    {
        if (companyId is { } id)
        {
            return await db.Companies.FirstOrDefaultAsync(c => c.Id == id && c.UserId == currentUser.UserId, ct)
                ?? throw new InvalidInputException("opportunity.unknown_company", "Pick one of your companies.");
        }

        var name = companyName!.Trim();
        var existing = await db.Companies.FirstOrDefaultAsync(c => c.UserId == currentUser.UserId && c.Name == name, ct);
        if (existing is not null) return existing;

        var company = new Company { UserId = currentUser.UserId, Name = name };
        db.Companies.Add(company);
        audit.Add(OpportunitiesAudit.CompanyCreated, currentUser.UserId, "company", company.Id);
        return company;
    }
}

public sealed class CompaniesHandler(
    IOpportunitiesDbContext db, ICurrentUser currentUser, IAuditWriter audit, IValidator<CompanyInput> validator)
{
    public async Task<IReadOnlyList<CompanyDto>> ListAsync(string? q, CancellationToken ct)
    {
        var query = db.Companies.AsNoTracking().Where(c => c.UserId == currentUser.UserId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(term));
        }
        var companies = await query.OrderBy(c => c.Name).Take(200).ToListAsync(ct);
        return companies.Select(CompanyDto.From).ToList();
    }

    public async Task<CompanyDto> CreateAsync(CompanyInput input, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        var company = new Company { UserId = currentUser.UserId, Name = input.Name.Trim(), Notes = Trimmed(input.Notes) };
        company.SetWebsite(input.Website);
        await EnsureUniqueAsync(company.Name, company.Domain, exceptId: null, ct);

        db.Companies.Add(company);
        audit.Add(OpportunitiesAudit.CompanyCreated, currentUser.UserId, "company", company.Id);
        await db.SaveChangesAsync(ct);
        return CompanyDto.From(company);
    }

    public async Task<CompanyDto> UpdateAsync(Guid id, CompanyInput input, uint version, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == id && c.UserId == currentUser.UserId, ct)
            ?? throw new NotFoundException();

        db.Entry(company).Property(c => c.Version).OriginalValue = version;
        company.Name = input.Name.Trim();
        company.Notes = Trimmed(input.Notes);
        company.SetWebsite(input.Website);
        await EnsureUniqueAsync(company.Name, company.Domain, exceptId: id, ct);

        audit.Add(OpportunitiesAudit.CompanyUpdated, currentUser.UserId, "company", id);
        await db.SaveChangesAsync(ct);
        return CompanyDto.From(company);
    }

    private async Task EnsureUniqueAsync(string name, string? domain, Guid? exceptId, CancellationToken ct)
    {
        var clash = await db.Companies
            .Where(c => c.UserId == currentUser.UserId && c.Id != exceptId && (c.Name == name || (domain != null && c.Domain == domain)))
            .Select(c => c.Name)
            .FirstOrDefaultAsync(ct);
        if (clash is not null)
        {
            throw new ConflictException("company.duplicate", $"You already have {clash}.");
        }
    }

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
