using FluentValidation;
using JobHunter.Application.Common;
using JobHunter.Application.Opportunities.Contracts;
using JobHunter.Domain.Contacts;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.Contacts;

/// <summary>Links between contacts and opportunities, and the log of what was said.</summary>
public sealed class InteractionsHandler(
    IContactsDbContext db,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IOpportunityFacts opportunities,
    IValidator<LinkOpportunityCommand> linkValidator,
    IValidator<InteractionInput> interactionValidator)
{
    /// <summary>A long history is a long page, and this is a log, not an archive to page through.</summary>
    private const int MaxListed = 200;

    public async Task<LinkedOpportunityDto> LinkAsync(Guid contactId, Guid opportunityId, LinkOpportunityCommand command, CancellationToken ct)
    {
        await linkValidator.ValidateAndThrowAsync(command, ct);
        await FindActiveContactAsync(contactId, ct);
        var facts = await opportunities.FindAsync(opportunityId, ct) ?? throw new NotFoundException();

        var link = await db.ContactOpportunities.FirstOrDefaultAsync(l => l.ContactId == contactId && l.OpportunityId == opportunityId, ct);
        if (link is null)
        {
            link = new ContactOpportunity { ContactId = contactId, OpportunityId = opportunityId, UserId = currentUser.UserId, Relation = command.Relation };
            db.ContactOpportunities.Add(link);
        }
        else
        {
            link.Relation = command.Relation;
        }

        audit.Add(ContactsAudit.Linked, currentUser.UserId, "contact", contactId, new { opportunityId, command.Relation });
        await db.SaveChangesAsync(ct);
        return LinkedOpportunityDto.From(link, facts);
    }

    public async Task UnlinkAsync(Guid contactId, Guid opportunityId, CancellationToken ct)
    {
        await FindContactAsync(contactId, ct);
        var link = await db.ContactOpportunities.FirstOrDefaultAsync(l => l.ContactId == contactId && l.OpportunityId == opportunityId, ct);
        if (link is null) return;

        db.ContactOpportunities.Remove(link);
        audit.Add(ContactsAudit.Unlinked, currentUser.UserId, "contact", contactId, new { opportunityId });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<InteractionDto>> ListAsync(Guid contactId, CancellationToken ct)
    {
        var contact = await FindContactAsync(contactId, ct);
        var rows = await db.Interactions.AsNoTracking()
            .Where(i => i.ContactId == contactId)
            .OrderByDescending(i => i.OccurredAt).ThenByDescending(i => i.Id)
            .Take(MaxListed)
            .ToListAsync(ct);
        var titles = await TitlesAsync(rows, ct);
        return rows.Select(i => ToDto(i, contact.Name, titles)).ToList();
    }

    public async Task<InteractionDto> LogAsync(Guid contactId, InteractionInput input, CancellationToken ct)
    {
        await interactionValidator.ValidateAndThrowAsync(input, ct);
        var contact = await FindActiveContactAsync(contactId, ct);

        string? title = null;
        if (input.OpportunityId is { } opportunityId)
        {
            var facts = await opportunities.FindAsync(opportunityId, ct)
                ?? throw new InvalidInputException("interaction.unknown_opportunity", "Pick one of your opportunities.");
            title = facts.Title;
        }

        var interaction = Interaction.Log(
            currentUser.UserId, contactId, input.OpportunityId, input.Channel, input.Direction, input.OccurredAt, input.Summary.Trim());
        db.Interactions.Add(interaction);

        // Never the summary: the audit log is about what the user did, not what someone else said.
        audit.Add(ContactsAudit.InteractionLogged, currentUser.UserId, "interaction", interaction.Id, new { contactId, input.Channel, input.Direction });
        await db.SaveChangesAsync(ct);
        return new InteractionDto(
            interaction.Id, contactId, contact.Name, interaction.OpportunityId, title,
            interaction.Channel, interaction.Direction, interaction.OccurredAt, interaction.Summary);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var interaction = await db.Interactions.FirstOrDefaultAsync(i => i.Id == id && i.UserId == currentUser.UserId, ct)
            ?? throw new NotFoundException();
        db.Interactions.Remove(interaction);
        audit.Add(ContactsAudit.InteractionDeleted, currentUser.UserId, "interaction", id, new { interaction.ContactId });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Who's on an opportunity and what's been said about it, for its page.</summary>
    public async Task<PeopleDto> PeopleAsync(Guid opportunityId, CancellationToken ct)
    {
        var facts = await opportunities.FindAsync(opportunityId, ct) ?? throw new NotFoundException();

        var people = await db.ContactOpportunities.AsNoTracking()
            .Where(l => l.OpportunityId == opportunityId && l.UserId == currentUser.UserId)
            .Join(db.Contacts.Where(c => c.ArchivedAt == null), l => l.ContactId, c => c.Id, (l, c) => new
            {
                c.Id,
                c.Name,
                c.Role,
                l.Relation,
                LastInteraction = db.Interactions.Where(i => i.ContactId == c.Id).Max(i => (DateTimeOffset?)i.OccurredAt),
            })
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        var interactions = await db.Interactions.AsNoTracking()
            .Where(i => i.OpportunityId == opportunityId && i.UserId == currentUser.UserId)
            .OrderByDescending(i => i.OccurredAt).ThenByDescending(i => i.Id)
            .Take(MaxListed)
            .Join(db.Contacts, i => i.ContactId, c => c.Id, (i, c) => new { Interaction = i, ContactName = c.Name })
            .ToListAsync(ct);

        return new PeopleDto(
            people.Select(p => new PersonDto(p.Id, p.Name, p.Role, p.Relation, p.LastInteraction)).ToList(),
            interactions
                .Select(r => new InteractionDto(
                    r.Interaction.Id, r.Interaction.ContactId, r.ContactName, opportunityId, facts.Title,
                    r.Interaction.Channel, r.Interaction.Direction, r.Interaction.OccurredAt, r.Interaction.Summary))
                .ToList());
    }

    private async Task<IReadOnlyDictionary<Guid, string>> TitlesAsync(List<Interaction> rows, CancellationToken ct)
    {
        var ids = rows.Where(i => i.OpportunityId != null).Select(i => i.OpportunityId!.Value).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        return (await opportunities.ListAsync(ids, ct)).ToDictionary(o => o.Id, o => o.Title);
    }

    private static InteractionDto ToDto(Interaction i, string contactName, IReadOnlyDictionary<Guid, string> titles) => new(
        i.Id, i.ContactId, contactName, i.OpportunityId,
        i.OpportunityId is { } id ? titles.GetValueOrDefault(id) : null,
        i.Channel, i.Direction, i.OccurredAt, i.Summary);

    private async Task<Contact> FindContactAsync(Guid id, CancellationToken ct) =>
        await db.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.UserId == currentUser.UserId, ct) ?? throw new NotFoundException();

    private async Task<Contact> FindActiveContactAsync(Guid id, CancellationToken ct)
    {
        var contact = await FindContactAsync(id, ct);
        if (contact.ArchivedAt is not null)
        {
            throw new ConflictException("contact.archived", "Restore this contact first.");
        }
        return contact;
    }
}
