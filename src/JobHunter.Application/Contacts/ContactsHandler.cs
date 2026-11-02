using FluentValidation;
using JobHunter.Application.Common;
using JobHunter.Application.Opportunities.Contracts;
using JobHunter.Domain.Contacts;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.Contacts;

public sealed class ContactsHandler(
    IContactsDbContext db,
    ICurrentUser currentUser,
    IAuditWriter audit,
    ICompanyDirectory companies,
    IOpportunityFacts opportunities,
    IValidator<ContactInput> validator)
{
    public async Task<Paged<ContactDto>> ListAsync(ContactListQuery query, CancellationToken ct)
    {
        var (page, pageSize) = Paged<ContactDto>.Clamp(query.Page, query.PageSize);
        var q = db.Contacts.AsNoTracking()
            .Where(c => c.UserId == currentUser.UserId && (c.ArchivedAt != null) == query.Archived);

        if (query.CompanyId is { } companyId) q = q.Where(c => c.CompanyId == companyId);
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim().ToLower();
            var matchingCompanies = await companies.MatchingAsync(term, ct);
            q = q.Where(c => c.Name.ToLower().Contains(term)
                || (c.Role != null && c.Role.ToLower().Contains(term))
                || (c.Email != null && c.Email.ToLower().Contains(term))
                || (c.CompanyId != null && matchingCompanies.Contains(c.CompanyId.Value)));
        }

        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderBy(c => c.Name).ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                Contact = c,
                Interactions = db.Interactions.Count(i => i.ContactId == c.Id),
                LastInteraction = db.Interactions.Where(i => i.ContactId == c.Id).Max(i => (DateTimeOffset?)i.OccurredAt),
                Links = db.ContactOpportunities.Count(l => l.ContactId == c.Id),
            })
            .ToListAsync(ct);

        var names = await companies.NamesAsync(rows.Where(r => r.Contact.CompanyId != null).Select(r => r.Contact.CompanyId!.Value).Distinct().ToList(), ct);
        var items = rows
            .Select(r => ToDto(r.Contact, Company(r.Contact.CompanyId, names), r.Interactions, r.LastInteraction, r.Links, []))
            .ToList();
        return new Paged<ContactDto>(items, page, pageSize, total);
    }

    public async Task<ContactDto> GetAsync(Guid id, CancellationToken ct)
    {
        var contact = await db.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.UserId == currentUser.UserId, ct)
            ?? throw new NotFoundException();
        return await ReadAsync(contact, ct);
    }

    public async Task<ContactDto> CreateAsync(ContactInput input, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        var contact = new Contact { UserId = currentUser.UserId, Name = input.Name.Trim() };
        var company = await ApplyAsync(contact, input, ct);

        db.Contacts.Add(contact);
        audit.Add(ContactsAudit.Created, currentUser.UserId, "contact", contact.Id);
        await db.SaveChangesAsync(ct);
        return ToDto(contact, company, 0, null, 0, []);
    }

    public async Task<ContactDto> UpdateAsync(Guid id, ContactInput input, uint version, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        var contact = await FindAsync(id, ct);

        db.Entry(contact).Property(c => c.Version).OriginalValue = version;
        contact.Name = input.Name.Trim();
        await ApplyAsync(contact, input, ct);

        audit.Add(ContactsAudit.Updated, currentUser.UserId, "contact", contact.Id);
        await db.SaveChangesAsync(ct);
        return await ReadAsync(contact, ct);
    }

    public async Task ArchiveAsync(Guid id, DateTimeOffset now, CancellationToken ct)
    {
        var contact = await FindAsync(id, ct);
        if (contact.ArchivedAt is not null) return;
        contact.ArchivedAt = now;
        audit.Add(ContactsAudit.Archived, currentUser.UserId, "contact", id);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ContactDto> RestoreAsync(Guid id, CancellationToken ct)
    {
        var contact = await FindAsync(id, ct);
        if (contact.ArchivedAt is not null)
        {
            contact.ArchivedAt = null;
            audit.Add(ContactsAudit.Restored, currentUser.UserId, "contact", id);
            await db.SaveChangesAsync(ct);
        }
        return await ReadAsync(contact, ct);
    }

    private async Task<ContactDto> ReadAsync(Contact contact, CancellationToken ct)
    {
        var links = await db.ContactOpportunities.AsNoTracking().Where(l => l.ContactId == contact.Id).ToListAsync(ct);
        var facts = (await opportunities.ListAsync(links.Select(l => l.OpportunityId).ToList(), ct)).ToDictionary(o => o.Id);
        var linked = links
            .Where(l => facts.ContainsKey(l.OpportunityId))
            .Select(l => LinkedOpportunityDto.From(l, facts[l.OpportunityId]))
            .OrderBy(l => l.Archived).ThenBy(l => l.Title)
            .ToList();

        var interactions = db.Interactions.AsNoTracking().Where(i => i.ContactId == contact.Id);
        var count = await interactions.CountAsync(ct);
        var last = count == 0 ? null : await interactions.MaxAsync(i => (DateTimeOffset?)i.OccurredAt, ct);

        var names = contact.CompanyId is { } companyId ? await companies.NamesAsync([companyId], ct) : new Dictionary<Guid, string>();
        return ToDto(contact, Company(contact.CompanyId, names), count, last, links.Count, linked);
    }

    /// <summary>Returns the company as it now reads, since a new one has no row to look up until saved.</summary>
    private async Task<ContactCompanyDto?> ApplyAsync(Contact contact, ContactInput input, CancellationToken ct)
    {
        contact.Role = Trimmed(input.Role);
        contact.Email = Trimmed(input.Email);
        contact.LinkedInUrl = ContactRules.NormalizeLinkedInUrl(input.LinkedInUrl);
        contact.Phone = Trimmed(input.Phone);
        contact.Notes = Trimmed(input.Notes);
        await EnsureEmailIsFreeAsync(contact, ct);

        if (input.CompanyId is null && string.IsNullOrWhiteSpace(input.CompanyName))
        {
            contact.CompanyId = null;
            return null;
        }

        var company = await companies.ResolveAsync(input.CompanyId, input.CompanyName, ct);
        contact.CompanyId = company.Id;
        return new ContactCompanyDto(company.Id, company.Name);
    }

    private async Task EnsureEmailIsFreeAsync(Contact contact, CancellationToken ct)
    {
        if (contact.Email is null) return;

        var email = contact.Email;
        var clash = await db.Contacts.AsNoTracking()
            .Where(c => c.UserId == currentUser.UserId && c.Id != contact.Id && c.Email == email)
            .Select(c => new { c.Name, c.ArchivedAt })
            .FirstOrDefaultAsync(ct);
        if (clash is null) return;

        throw new ConflictException("contact.duplicate", clash.ArchivedAt is null
            ? $"{clash.Name} already has this email."
            : $"{clash.Name} has this email, and is archived. Restore them instead.");
    }

    private async Task<Contact> FindAsync(Guid id, CancellationToken ct) =>
        await db.Contacts.FirstOrDefaultAsync(c => c.Id == id && c.UserId == currentUser.UserId, ct) ?? throw new NotFoundException();

    private static ContactCompanyDto? Company(Guid? id, IReadOnlyDictionary<Guid, string> names) =>
        id is { } companyId && names.TryGetValue(companyId, out var name) ? new ContactCompanyDto(companyId, name) : null;

    private static ContactDto ToDto(
        Contact c, ContactCompanyDto? company, int interactions, DateTimeOffset? last, int links, IReadOnlyList<LinkedOpportunityDto> opportunities) =>
        new(c.Id, c.Name, company, c.Role, c.Email, c.LinkedInUrl, c.Phone, c.Notes, c.ArchivedAt, c.Version, c.CreatedAt, c.UpdatedAt,
            interactions, last, links, opportunities);

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
