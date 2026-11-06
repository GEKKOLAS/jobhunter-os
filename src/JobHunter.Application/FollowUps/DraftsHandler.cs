using FluentValidation;
using JobHunter.Application.Common;
using JobHunter.Application.Contacts.Contracts;
using JobHunter.Application.Identity.Contracts;
using JobHunter.Application.Integrations.Contracts;
using JobHunter.Application.Opportunities.Contracts;
using JobHunter.Application.Profile.Contracts;
using JobHunter.Domain.Common;
using JobHunter.Domain.FollowUps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobHunter.Application.FollowUps;

/// <summary>Drafts, from blank or written, through approval to sent (spec 03, Follow-ups). Nothing is sent without its click.</summary>
public sealed class DraftsHandler(
    IFollowUpsDbContext db,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock,
    IOpportunityFacts opportunities,
    ICompanyDirectory companies,
    IContactFacts contacts,
    IInteractionLog interactionLog,
    IUserFacts users,
    ISkillCatalog catalog,
    IDraftWriter writer,
    IOutreachSender outreach,
    IValidator<DraftRequest> requestValidator,
    IValidator<DraftEdit> editValidator,
    ILogger<DraftsHandler> logger)
{
    private const int MaxListed = 100;

    public async Task<IReadOnlyList<DraftDto>> ListAsync(DraftListQuery query, CancellationToken ct)
    {
        var q = db.Drafts.AsNoTracking().Where(d => d.UserId == currentUser.UserId);
        q = string.IsNullOrWhiteSpace(query.Status)
            ? q.Where(d => d.Status == Draft.StatusDraft || d.Status == Draft.StatusApproved)
            : q.Where(d => d.Status == query.Status);
        if (query.OpportunityId is { } opportunityId) q = q.Where(d => d.OpportunityId == opportunityId);
        if (query.ContactId is { } contactId) q = q.Where(d => d.ContactId == contactId);

        var rows = await q.OrderByDescending(d => d.UpdatedAt).ThenByDescending(d => d.Id).Take(MaxListed).ToListAsync(ct);
        return await ToDtosAsync(rows, ct);
    }

    public async Task<CreatedDraftDto> CreateAsync(DraftRequest request, CancellationToken ct)
    {
        await requestValidator.ValidateAndThrowAsync(request, ct);

        OpportunityFacts? opportunity = null;
        if (request.OpportunityId is { } opportunityId)
        {
            opportunity = await opportunities.FindAsync(opportunityId, ct)
                ?? throw new InvalidInputException("draft.unknown_opportunity", "Pick one of your opportunities.");
        }
        ContactFacts? contact = null;
        if (request.ContactId is { } contactId)
        {
            contact = await contacts.FindAsync(contactId, ct)
                ?? throw new InvalidInputException("draft.unknown_contact", "Pick one of your contacts.");
        }

        var (written, note) = request.Generate ? await WriteAsync(request, opportunity, contact, ct) : (null, null);
        var origin = written is null ? FollowUpVocabulary.OriginUser : FollowUpVocabulary.OriginAi;

        var draft = new Draft
        {
            UserId = currentUser.UserId,
            Kind = request.Kind,
            OpportunityId = request.OpportunityId,
            ContactId = request.ContactId,
            Origin = origin,
        };
        draft.Edit(contact?.Email, written?.Subject ?? "", written?.Body ?? "");

        db.Drafts.Add(draft);
        audit.Add(FollowUpsAudit.DraftCreated, currentUser.UserId, "draft", draft.Id, new { draft.Kind, origin });
        await db.SaveChangesAsync(ct);
        return new CreatedDraftDto((await ToDtosAsync([draft], ct))[0], written is not null, note);
    }

    public async Task<DraftDto> UpdateAsync(Guid id, DraftEdit edit, uint version, CancellationToken ct)
    {
        await editValidator.ValidateAndThrowAsync(edit, ct);
        var draft = await FindAsync(id, ct);
        db.Entry(draft).Property(d => d.Version).OriginalValue = version;

        Guarded(() => draft.Edit(Trimmed(edit.ToEmail), edit.Subject?.Trim() ?? "", edit.Body ?? ""));
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([draft], ct))[0];
    }

    public async Task<DraftDto> ApproveAsync(Guid id, CancellationToken ct)
    {
        var draft = await FindAsync(id, ct);
        Guarded(() => draft.Approve(clock.UtcNow));
        audit.Add(FollowUpsAudit.DraftApproved, currentUser.UserId, "draft", id);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([draft], ct))[0];
    }

    /// <summary>
    /// Sends an approved draft. If saving fails after Outreach Studio accepted it, a retry reuses
    /// the same idempotency key, so the email still goes out once.
    /// </summary>
    public async Task<DraftDto> SendAsync(Guid id, CancellationToken ct)
    {
        var draft = await FindAsync(id, ct);
        if (draft.IsClosed) throw new ConflictException("draft.closed", $"This draft was {draft.Status} and can't change.");
        if (draft.Status != Draft.StatusApproved)
        {
            throw new ConflictException("draft.not_approved", "Approve it first: nothing is sent without your approval.");
        }
        if (string.IsNullOrWhiteSpace(draft.ToEmail))
        {
            throw new InvalidInputException("draft.no_recipient", "Add who it goes to.");
        }

        var contact = draft.ContactId is { } contactId ? await contacts.FindAsync(contactId, ct) : null;
        var result = await outreach.SendAsync(new OutreachEmail(draft.Id, draft.ToEmail, contact?.Name, draft.Subject, draft.Body), ct);

        return await CompleteSendAsync(draft, Draft.ViaOutreachStudio, result.SendId, ct);
    }

    /// <summary>The user sent it themselves (copied it into their own mail). Approval isn't needed: it was their click.</summary>
    public async Task<DraftDto> MarkSentAsync(Guid id, CancellationToken ct)
    {
        var draft = await FindAsync(id, ct);
        if (draft.IsClosed) throw new ConflictException("draft.closed", $"This draft was {draft.Status} and can't change.");
        return await CompleteSendAsync(draft, Draft.ViaManual, null, ct);
    }

    public async Task DiscardAsync(Guid id, CancellationToken ct)
    {
        var draft = await FindAsync(id, ct);
        if (draft.Status == Draft.StatusDiscarded) return;
        Guarded(draft.Discard);
        audit.Add(FollowUpsAudit.DraftDiscarded, currentUser.UserId, "draft", id);
        await db.SaveChangesAsync(ct);
    }

    private async Task<DraftDto> CompleteSendAsync(Draft draft, string via, string? sendId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        Guarded(() => draft.MarkSent(via, sendId, now));
        if (draft.ContactId is { } contactId)
        {
            var subject = string.IsNullOrWhiteSpace(draft.Subject) ? "an email" : $"“{draft.Subject}”";
            var summary = via == Draft.ViaOutreachStudio ? $"Sent {subject} through Outreach Studio." : $"Sent {subject} yourself.";
            await interactionLog.LogSentEmailAsync(contactId, draft.OpportunityId, summary, sendId, now, ct);
        }
        audit.Add(FollowUpsAudit.DraftSent, currentUser.UserId, "draft", draft.Id, new { via, sendId });
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([draft], ct))[0];
    }

    /// <summary>A written draft that passes every guardrail, or none and a note saying why it's blank.</summary>
    private async Task<(WrittenDraft? Draft, string? Note)> WriteAsync(DraftRequest request, OpportunityFacts? opportunity, ContactFacts? contact, CancellationToken ct)
    {
        if (!writer.IsAvailable) return (null, "Drafting by AI isn't set up, so this one starts blank.");

        var brief = new DraftBrief(
            request.Kind, opportunity?.Title, opportunity?.CompanyName, contact?.Name, contact?.Role,
            await users.DisplayNameAsync(ct), (request.Facts ?? []).Select(f => f.Trim()).Where(f => f.Length > 0).ToList());

        try
        {
            var written = await writer.WriteAsync(brief, ct);
            var others = (await companies.AllNamesAsync(ct))
                .Where(name => !string.Equals(name, opportunity?.CompanyName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (DraftGuardrails.Check(brief, written, catalog, others) is { } violation)
            {
                logger.LogWarning("A written draft was rejected by the guardrails ({Violation})", violation);
                return (null, "The written draft said something your facts don't, so it was thrown away. This one starts blank.");
            }
            return (written, null);
        }
        catch (AiUnavailableException ex)
        {
            logger.LogWarning(ex, "Drafting by AI failed");
            return (null, "Couldn't write one just now, so this one starts blank.");
        }
    }

    private async Task<Draft> FindAsync(Guid id, CancellationToken ct) =>
        await db.Drafts.FirstOrDefaultAsync(d => d.Id == id && d.UserId == currentUser.UserId, ct) ?? throw new NotFoundException();

    private static void Guarded(Action action)
    {
        try
        {
            action();
        }
        catch (DomainException ex) when (ex.Code == "draft.incomplete")
        {
            throw new InvalidInputException(ex.Code, ex.Message);
        }
        catch (DomainException ex)
        {
            throw new ConflictException(ex.Code, ex.Message);
        }
    }

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<IReadOnlyList<DraftDto>> ToDtosAsync(IReadOnlyList<Draft> rows, CancellationToken ct)
    {
        var opportunityIds = rows.Where(d => d.OpportunityId != null).Select(d => d.OpportunityId!.Value).Distinct().ToList();
        var titles = opportunityIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await opportunities.ListAsync(opportunityIds, ct)).ToDictionary(o => o.Id, o => o.Title);

        var names = new Dictionary<Guid, string>();
        foreach (var contactId in rows.Where(d => d.ContactId != null).Select(d => d.ContactId!.Value).Distinct())
        {
            if (await contacts.FindAsync(contactId, ct) is { } contact) names[contactId] = contact.Name;
        }

        return rows.Select(d => new DraftDto(
            d.Id, d.Kind, d.OpportunityId, d.OpportunityId is { } o ? titles.GetValueOrDefault(o) : null,
            d.ContactId, d.ContactId is { } c ? names.GetValueOrDefault(c) : null, d.ToEmail, d.Subject, d.Body, d.Origin, d.Status,
            d.ApprovedAt, d.SentVia, d.OutreachSendId, d.SentAt, d.Version, d.CreatedAt, d.UpdatedAt)).ToList();
    }
}
