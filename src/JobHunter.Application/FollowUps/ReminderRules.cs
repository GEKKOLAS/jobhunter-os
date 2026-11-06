using JobHunter.Application.Common;
using JobHunter.Domain.Contacts.Events;
using JobHunter.Domain.FollowUps;
using JobHunter.Domain.Opportunities.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobHunter.Application.FollowUps;

/// <summary>Rules v1 (spec 03, Follow-ups): reminders that follow from what happened, and that stop when they answer.</summary>
public sealed class ReminderRules(IFollowUpsDbContext db, IClock clock, IOptions<FollowUpsOptions> options)
    : IEventHandler<OpportunityStageChanged>, IEventHandler<InteractionLogged>
{
    // The Opportunities and Contacts vocabularies, as published in their events.
    private const string Applied = "applied";
    private const string Interviewing = "interviewing";
    private const string Inbound = "inbound";

    public async Task HandleAsync(OpportunityStageChanged e, CancellationToken ct)
    {
        // A move that records their answer (screening, a rejection…) ends the chase the apply rule started.
        // Only that rule's reminder: a follow-up the user added themselves is theirs to close.
        if (e.FirstResponseAt is not null)
        {
            var chase = await db.Reminders.FirstOrDefaultAsync(r => r.UserId == e.UserId && r.OpportunityId == e.OpportunityId
                && r.RuleKey == FollowUpVocabulary.RuleAfterApply && r.Status == FollowUpVocabulary.Open, ct);
            chase?.Complete(clock.UtcNow);
        }

        if (e.ToStage == Applied && e.FirstResponseAt is null)
        {
            await AddOnceAsync(e, FollowUpVocabulary.RuleAfterApply, FollowUpVocabulary.FollowUp,
                $"Follow up on {e.Title}", e.OccurredAt.AddDays(options.Value.AfterApplyDays), ct);
        }
        else if (e.ToStage == Interviewing)
        {
            await AddOnceAsync(e, FollowUpVocabulary.RuleAfterInterview, FollowUpVocabulary.ThankYou,
                $"Send a thank-you for {e.Title}", e.OccurredAt.AddDays(1), ct);
        }
    }

    /// <summary>They answered, so there's nothing left to chase: by the opportunity, or by the person.</summary>
    public async Task HandleAsync(InteractionLogged e, CancellationToken ct)
    {
        if (e.Direction != Inbound) return;

        var open = await db.Reminders
            .Where(r => r.UserId == e.UserId && r.Status == FollowUpVocabulary.Open && r.Kind == FollowUpVocabulary.FollowUp
                && ((e.OpportunityId != null && r.OpportunityId == e.OpportunityId) || r.ContactId == e.ContactId))
            .ToListAsync(ct);
        foreach (var reminder in open) reminder.Complete(clock.UtcNow);
    }

    private async Task AddOnceAsync(OpportunityStageChanged e, string ruleKey, string kind, string title, DateTimeOffset dueAt, CancellationToken ct)
    {
        // A correction back and forth through a stage must not stack reminders: a rule fires once.
        if (await db.Reminders.AnyAsync(r => r.OpportunityId == e.OpportunityId && r.RuleKey == ruleKey, ct)) return;

        db.Reminders.Add(new Reminder
        {
            UserId = e.UserId,
            OpportunityId = e.OpportunityId,
            Title = title.Length <= FollowUpVocabulary.MaxTitle ? title : title[..FollowUpVocabulary.MaxTitle],
            DueAt = dueAt,
            Kind = kind,
            Origin = FollowUpVocabulary.OriginRule,
            RuleKey = ruleKey,
        });
    }
}
