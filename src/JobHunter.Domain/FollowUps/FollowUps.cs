using JobHunter.Domain.Common;

namespace JobHunter.Domain.FollowUps;

/// <summary>The closed value sets and limits of the FollowUps module (spec 03, Follow-ups). Stored as text.</summary>
public static class FollowUpVocabulary
{
    public const string FollowUp = "follow_up";
    public const string ThankYou = "thank_you";
    public static readonly IReadOnlyList<string> ReminderKinds = [FollowUp, ThankYou, "prep", "custom"];
    public static readonly IReadOnlyList<string> DraftKinds = [FollowUp, ThankYou, "outreach"];

    public const string Open = "open";
    public const string Done = "done";
    public const string Dismissed = "dismissed";

    public const string OriginUser = "user";
    public const string OriginRule = "rule";
    public const string OriginAi = "ai";

    public const string RuleAfterApply = "after_apply";
    public const string RuleAfterInterview = "after_interview";

    public const int MaxTitle = 160;
    public const int MaxSubject = 200;
    public const int MaxBody = 10_000;
    public const int MaxFacts = 8;
    public const int MaxFact = 300;
}

/// <summary>Something to do, at a time. Rules make some of them; the user makes the rest.</summary>
public sealed class Reminder : IHasTimestamps
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }

    public required string Title { get; set; }
    public required DateTimeOffset DueAt { get; set; }
    public Guid? OpportunityId { get; init; }
    public Guid? ContactId { get; init; }

    /// <summary>One of <see cref="FollowUpVocabulary.ReminderKinds"/>.</summary>
    public required string Kind { get; set; }

    public string Status { get; private set; } = FollowUpVocabulary.Open;
    public DateTimeOffset? SnoozedUntil { get; private set; }
    public required string Origin { get; init; }

    /// <summary>Which rule made it; unique with the opportunity, so a rule fires once.</summary>
    public string? RuleKey { get; init; }

    /// <summary>When its notification went out; cleared by anything that moves the due time.</summary>
    public DateTimeOffset? NotifiedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public uint Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>When it's due now: a snooze pushes it, a reschedule replaces it.</summary>
    public DateTimeOffset EffectiveDueAt => SnoozedUntil ?? DueAt;

    public void Reschedule(DateTimeOffset dueAt)
    {
        if (dueAt == DueAt) return;
        DueAt = dueAt;
        SnoozedUntil = null;
        NotifiedAt = null;
    }

    public void Snooze(DateTimeOffset until)
    {
        EnsureOpen();
        SnoozedUntil = until;
        NotifiedAt = null;
    }

    public void Complete(DateTimeOffset now)
    {
        EnsureOpen();
        Status = FollowUpVocabulary.Done;
        CompletedAt = now;
    }

    public void Dismiss(DateTimeOffset now)
    {
        if (Status == FollowUpVocabulary.Dismissed) return;
        Status = FollowUpVocabulary.Dismissed;
        CompletedAt = now;
    }

    private void EnsureOpen()
    {
        if (Status != FollowUpVocabulary.Open) throw new DomainException("reminder.closed", "This reminder is already closed.");
    }
}

/// <summary>An email waiting for the user's approval. Nothing leaves without it (PRD principle 3).</summary>
public sealed class Draft : IHasTimestamps
{
    public const string StatusDraft = "draft";
    public const string StatusApproved = "approved";
    public const string StatusSent = "sent";
    public const string StatusDiscarded = "discarded";

    public const string ViaOutreachStudio = "outreach_studio";
    public const string ViaManual = "manual";

    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }

    /// <summary>One of <see cref="FollowUpVocabulary.DraftKinds"/>.</summary>
    public required string Kind { get; init; }

    public Guid? OpportunityId { get; init; }
    public Guid? ContactId { get; init; }
    public string? ToEmail { get; private set; }
    public string Subject { get; private set; } = "";
    public string Body { get; private set; } = "";
    public required string Origin { get; init; }

    public string Status { get; private set; } = StatusDraft;
    public DateTimeOffset? ApprovedAt { get; private set; }
    public string? SentVia { get; private set; }
    public string? OutreachSendId { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }

    public uint Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsClosed => Status is StatusSent or StatusDiscarded;

    /// <summary>Any edit sends an approved draft back for approval: what was approved is what goes out.</summary>
    public void Edit(string? toEmail, string subject, string body)
    {
        EnsureOpen();
        var changed = toEmail != ToEmail || subject != Subject || body != Body;
        ToEmail = toEmail;
        Subject = subject;
        Body = body;
        if (changed && Status == StatusApproved)
        {
            Status = StatusDraft;
            ApprovedAt = null;
        }
    }

    public void Approve(DateTimeOffset now)
    {
        EnsureOpen();
        if (string.IsNullOrWhiteSpace(Subject) || string.IsNullOrWhiteSpace(Body))
        {
            throw new DomainException("draft.incomplete", "Write a subject and a body before approving it.");
        }
        Status = StatusApproved;
        ApprovedAt = now;
    }

    public void MarkSent(string via, string? outreachSendId, DateTimeOffset now)
    {
        EnsureOpen();
        if (via == ViaOutreachStudio && Status != StatusApproved)
        {
            throw new DomainException("draft.not_approved", "Approve it first: nothing is sent without your approval.");
        }
        Status = StatusSent;
        SentVia = via;
        OutreachSendId = outreachSendId;
        SentAt = now;
    }

    public void Discard()
    {
        EnsureOpen();
        Status = StatusDiscarded;
    }

    private void EnsureOpen()
    {
        if (IsClosed) throw new DomainException("draft.closed", $"This draft was {Status} and can't change.");
    }
}

/// <summary>An in-app notice. The only kind in v1: a reminder became due.</summary>
public sealed class Notification
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public Guid? ReminderId { get; init; }
    public required string Title { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ReadAt { get; set; }
}
