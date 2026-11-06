using FluentValidation;
using JobHunter.Application.Common;
using JobHunter.Domain.FollowUps;

namespace JobHunter.Application.FollowUps;

/// <param name="OpportunityTitle">For display; null when it isn't about one, or it's gone.</param>
public sealed record ReminderDto(
    Guid Id, string Title, DateTimeOffset DueAt, DateTimeOffset? SnoozedUntil, DateTimeOffset EffectiveDueAt, string Kind, string Status,
    string Origin, Guid? OpportunityId, string? OpportunityTitle, Guid? ContactId, string? ContactName, uint Version);

public sealed record DraftDto(
    Guid Id, string Kind, Guid? OpportunityId, string? OpportunityTitle, Guid? ContactId, string? ContactName, string? ToEmail,
    string Subject, string Body, string Origin, string Status, DateTimeOffset? ApprovedAt, string? SentVia, string? OutreachSendId,
    DateTimeOffset? SentAt, uint Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

/// <param name="GenerationNote">Why it's blank, when a written one was asked for and couldn't be had.</param>
public sealed record CreatedDraftDto(DraftDto Draft, bool Generated, string? GenerationNote);

public sealed record NotificationDto(Guid Id, string Title, Guid? ReminderId, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);

public sealed record NotificationsDto(IReadOnlyList<NotificationDto> Items, int UnreadCount);

public sealed record ReminderListQuery(string? Status, DateTimeOffset? Until, Guid? OpportunityId, Guid? ContactId);

public sealed record DraftListQuery(string? Status, Guid? OpportunityId, Guid? ContactId);

public sealed record ReminderInput(string Title, DateTimeOffset DueAt, string Kind, Guid? OpportunityId, Guid? ContactId);

public sealed class ReminderInputValidator : AbstractValidator<ReminderInput>
{
    public ReminderInputValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Say what to do.").MaximumLength(FollowUpVocabulary.MaxTitle);
        RuleFor(x => x.Kind).Must(FollowUpVocabulary.ReminderKinds.Contains)
            .WithMessage($"Kind must be one of: {string.Join(", ", FollowUpVocabulary.ReminderKinds)}.");
        RuleFor(x => x.DueAt).Must(d => d.Year >= 2000).WithMessage("Say when it's due.");
    }
}

public sealed record SnoozeCommand(DateTimeOffset Until);

public sealed class SnoozeValidator : AbstractValidator<SnoozeCommand>
{
    public SnoozeValidator(IClock clock)
    {
        RuleFor(x => x.Until).Must(u => u > clock.UtcNow).WithMessage("Snooze it to a time that hasn't passed.");
    }
}

public sealed record DraftRequest(string Kind, Guid? OpportunityId, Guid? ContactId, bool Generate, IReadOnlyList<string>? Facts);

public sealed class DraftRequestValidator : AbstractValidator<DraftRequest>
{
    public DraftRequestValidator()
    {
        RuleFor(x => x.Kind).Must(FollowUpVocabulary.DraftKinds.Contains)
            .WithMessage($"Kind must be one of: {string.Join(", ", FollowUpVocabulary.DraftKinds)}.");
        RuleFor(x => x.Facts).Must(f => f is null || f.Count <= FollowUpVocabulary.MaxFacts)
            .WithMessage($"Pick up to {FollowUpVocabulary.MaxFacts} facts.");
        RuleForEach(x => x.Facts).NotEmpty().MaximumLength(FollowUpVocabulary.MaxFact);
    }
}

public sealed record DraftEdit(string? ToEmail, string? Subject, string? Body);

public sealed class DraftEditValidator : AbstractValidator<DraftEdit>
{
    public DraftEditValidator()
    {
        RuleFor(x => x.ToEmail).MaximumLength(254)
            .Matches(@"^\s*[^\s@]+@[^\s@]+\.[^\s@]+\s*$").When(x => !string.IsNullOrWhiteSpace(x.ToEmail))
            .WithMessage("That doesn't look like an email address.");
        RuleFor(x => x.Subject).MaximumLength(FollowUpVocabulary.MaxSubject);
        RuleFor(x => x.Body).MaximumLength(FollowUpVocabulary.MaxBody);
    }
}

/// <summary>Audit action codes of the FollowUps module. Only ids and states go in: drafts are private words.</summary>
public static class FollowUpsAudit
{
    public const string ReminderCreated = "reminder.created";
    public const string ReminderUpdated = "reminder.updated";
    public const string ReminderDone = "reminder.done";
    public const string ReminderSnoozed = "reminder.snoozed";
    public const string ReminderDismissed = "reminder.dismissed";
    public const string DraftCreated = "draft.created";
    public const string DraftApproved = "draft.approved";
    public const string DraftSent = "draft.sent";
    public const string DraftDiscarded = "draft.discarded";
}
