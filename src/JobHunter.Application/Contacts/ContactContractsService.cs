using JobHunter.Application.Common;
using JobHunter.Application.Contacts.Contracts;
using JobHunter.Domain.Contacts;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.Contacts;

internal sealed class ContactContractsService(IContactsDbContext db, ICurrentUser currentUser) : IContactFacts, IInteractionLog
{
    public async Task<ContactFacts?> FindAsync(Guid id, CancellationToken ct) =>
        await db.Contacts.AsNoTracking()
            .Where(c => c.Id == id && c.UserId == currentUser.UserId)
            .Select(c => new ContactFacts(c.Id, c.Name, c.Role, c.Email, c.ArchivedAt != null))
            .FirstOrDefaultAsync(ct);

    public async Task LogSentEmailAsync(Guid contactId, Guid? opportunityId, string summary, string? outreachSendId, DateTimeOffset at, CancellationToken ct)
    {
        if (await FindAsync(contactId, ct) is null) return;

        var text = summary.Length <= ContactVocabulary.MaxSummary ? summary : summary[..ContactVocabulary.MaxSummary];
        db.Interactions.Add(Interaction.Log(currentUser.UserId, contactId, opportunityId, "email", "outbound", at, text, outreachSendId));
    }
}
