using FluentValidation;
using JobHunter.Application.Common;
using JobHunter.Application.Contacts.Contracts;
using JobHunter.Application.Opportunities.Contracts;
using JobHunter.Domain.Common;
using JobHunter.Domain.FollowUps;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.FollowUps;

public sealed class RemindersHandler(
    IFollowUpsDbContext db,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock,
    IOpportunityFacts opportunities,
    IContactFacts contacts,
    IValidator<ReminderInput> inputValidator,
    IValidator<SnoozeCommand> snoozeValidator)
{
    private const int MaxListed = 200;

    public async Task<IReadOnlyList<ReminderDto>> ListAsync(ReminderListQuery query, CancellationToken ct)
    {
        var q = db.Reminders.AsNoTracking().Where(r => r.UserId == currentUser.UserId);
        q = q.Where(r => r.Status == (string.IsNullOrWhiteSpace(query.Status) ? FollowUpVocabulary.Open : query.Status));
        if (query.Until is { } until) q = q.Where(r => (r.SnoozedUntil ?? r.DueAt) <= until);
        if (query.OpportunityId is { } opportunityId) q = q.Where(r => r.OpportunityId == opportunityId);
        if (query.ContactId is { } contactId) q = q.Where(r => r.ContactId == contactId);

        var rows = await q.OrderBy(r => r.SnoozedUntil ?? r.DueAt).ThenBy(r => r.Id).Take(MaxListed).ToListAsync(ct);
        return await ToDtosAsync(rows, ct);
    }

    public async Task<ReminderDto> CreateAsync(ReminderInput input, CancellationToken ct)
    {
        await inputValidator.ValidateAndThrowAsync(input, ct);
        if (input.OpportunityId is { } opportunityId && await opportunities.FindAsync(opportunityId, ct) is null)
        {
            throw new InvalidInputException("reminder.unknown_opportunity", "Pick one of your opportunities.");
        }
        if (input.ContactId is { } contactId && await contacts.FindAsync(contactId, ct) is null)
        {
            throw new InvalidInputException("reminder.unknown_contact", "Pick one of your contacts.");
        }

        var reminder = new Reminder
        {
            UserId = currentUser.UserId,
            Title = input.Title.Trim(),
            DueAt = input.DueAt,
            Kind = input.Kind,
            OpportunityId = input.OpportunityId,
            ContactId = input.ContactId,
            Origin = FollowUpVocabulary.OriginUser,
        };
        db.Reminders.Add(reminder);
        audit.Add(FollowUpsAudit.ReminderCreated, currentUser.UserId, "reminder", reminder.Id);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([reminder], ct))[0];
    }

    /// <summary>The title, kind and time change; what it's about doesn't.</summary>
    public async Task<ReminderDto> UpdateAsync(Guid id, ReminderInput input, uint version, CancellationToken ct)
    {
        await inputValidator.ValidateAndThrowAsync(input, ct);
        var reminder = await FindAsync(id, ct);
        db.Entry(reminder).Property(r => r.Version).OriginalValue = version;

        reminder.Title = input.Title.Trim();
        reminder.Kind = input.Kind;
        reminder.Reschedule(input.DueAt);
        audit.Add(FollowUpsAudit.ReminderUpdated, currentUser.UserId, "reminder", id);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([reminder], ct))[0];
    }

    public async Task<ReminderDto> DoneAsync(Guid id, CancellationToken ct)
    {
        var reminder = await FindAsync(id, ct);
        Guarded(() => reminder.Complete(clock.UtcNow));
        audit.Add(FollowUpsAudit.ReminderDone, currentUser.UserId, "reminder", id);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([reminder], ct))[0];
    }

    public async Task<ReminderDto> SnoozeAsync(Guid id, SnoozeCommand command, CancellationToken ct)
    {
        await snoozeValidator.ValidateAndThrowAsync(command, ct);
        var reminder = await FindAsync(id, ct);
        Guarded(() => reminder.Snooze(command.Until));
        audit.Add(FollowUpsAudit.ReminderSnoozed, currentUser.UserId, "reminder", id);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([reminder], ct))[0];
    }

    public async Task DismissAsync(Guid id, CancellationToken ct)
    {
        var reminder = await FindAsync(id, ct);
        if (reminder.Status == FollowUpVocabulary.Dismissed) return;
        reminder.Dismiss(clock.UtcNow);
        audit.Add(FollowUpsAudit.ReminderDismissed, currentUser.UserId, "reminder", id);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Reminder> FindAsync(Guid id, CancellationToken ct) =>
        await db.Reminders.FirstOrDefaultAsync(r => r.Id == id && r.UserId == currentUser.UserId, ct) ?? throw new NotFoundException();

    private static void Guarded(Action action)
    {
        try
        {
            action();
        }
        catch (DomainException ex)
        {
            throw new ConflictException(ex.Code, ex.Message);
        }
    }

    private async Task<IReadOnlyList<ReminderDto>> ToDtosAsync(IReadOnlyList<Reminder> rows, CancellationToken ct)
    {
        var opportunityIds = rows.Where(r => r.OpportunityId != null).Select(r => r.OpportunityId!.Value).Distinct().ToList();
        var titles = opportunityIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await opportunities.ListAsync(opportunityIds, ct)).ToDictionary(o => o.Id, o => o.Title);

        var names = new Dictionary<Guid, string>();
        foreach (var contactId in rows.Where(r => r.ContactId != null).Select(r => r.ContactId!.Value).Distinct())
        {
            if (await contacts.FindAsync(contactId, ct) is { } contact) names[contactId] = contact.Name;
        }

        return rows.Select(r => new ReminderDto(
            r.Id, r.Title, r.DueAt, r.SnoozedUntil, r.EffectiveDueAt, r.Kind, r.Status, r.Origin,
            r.OpportunityId, r.OpportunityId is { } o ? titles.GetValueOrDefault(o) : null,
            r.ContactId, r.ContactId is { } c ? names.GetValueOrDefault(c) : null, r.Version)).ToList();
    }
}

public sealed class NotificationsHandler(IFollowUpsDbContext db, ICurrentUser currentUser, IClock clock)
{
    private const int MaxListed = 50;

    public async Task<NotificationsDto> ListAsync(CancellationToken ct)
    {
        var mine = db.Notifications.AsNoTracking().Where(n => n.UserId == currentUser.UserId);
        var items = await mine.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Take(MaxListed)
            .Select(n => new NotificationDto(n.Id, n.Title, n.ReminderId, n.CreatedAt, n.ReadAt))
            .ToListAsync(ct);
        var unread = await mine.CountAsync(n => n.ReadAt == null, ct);
        return new NotificationsDto(items, unread);
    }

    /// <summary>The given ones, or all of them when none are given.</summary>
    public async Task MarkReadAsync(IReadOnlyList<Guid>? ids, CancellationToken ct)
    {
        var q = db.Notifications.Where(n => n.UserId == currentUser.UserId && n.ReadAt == null);
        if (ids is { Count: > 0 }) q = q.Where(n => ids.Contains(n.Id));
        var now = clock.UtcNow;
        await q.ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
    }
}

/// <summary>
/// Turns reminders that came due into notifications, once per due time (spec 03, Follow-ups).
/// Runs for everyone from the background worker, so it works across users by design.
/// </summary>
public sealed class DueRemindersPass(IFollowUpsDbContext db, IClock clock)
{
    private const int Batch = 500;

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var due = await db.Reminders
            .Where(r => r.Status == FollowUpVocabulary.Open && r.NotifiedAt == null && (r.SnoozedUntil ?? r.DueAt) <= now)
            .OrderBy(r => r.SnoozedUntil ?? r.DueAt)
            .Take(Batch)
            .ToListAsync(ct);

        foreach (var reminder in due)
        {
            reminder.NotifiedAt = now;
            db.Notifications.Add(new Notification { UserId = reminder.UserId, ReminderId = reminder.Id, Title = reminder.Title, CreatedAt = now });
        }
        if (due.Count > 0) await db.SaveChangesAsync(ct);
        return due.Count;
    }
}
