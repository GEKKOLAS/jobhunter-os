using FluentValidation;
using JobHunter.Application.Common;
using JobHunter.Application.Opportunities.Contracts;
using JobHunter.Domain.Contacts;

namespace JobHunter.Application.Contacts;

public sealed record ContactCompanyDto(Guid Id, string Name);

/// <param name="Title">The opportunity's title; null only if it's no longer the user's to see.</param>
public sealed record LinkedOpportunityDto(Guid OpportunityId, string Title, string CompanyName, string Stage, bool Archived, string Relation)
{
    public static LinkedOpportunityDto From(ContactOpportunity link, OpportunityFacts facts) =>
        new(link.OpportunityId, facts.Title, facts.CompanyName, facts.Stage, facts.Archived, link.Relation);
}

/// <param name="Opportunities">Filled in when reading one contact; empty in lists.</param>
public sealed record ContactDto(
    Guid Id, string Name, ContactCompanyDto? Company, string? Role, string? Email, string? LinkedInUrl, string? Phone, string? Notes,
    DateTimeOffset? ArchivedAt, uint Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    int InteractionCount, DateTimeOffset? LastInteractionAt, int OpportunityCount, IReadOnlyList<LinkedOpportunityDto> Opportunities);

/// <param name="OpportunityTitle">Null when it isn't about one opportunity.</param>
public sealed record InteractionDto(
    Guid Id, Guid ContactId, string ContactName, Guid? OpportunityId, string? OpportunityTitle,
    string Channel, string Direction, DateTimeOffset OccurredAt, string Summary);

/// <summary>Someone linked to an opportunity, as its page lists them.</summary>
public sealed record PersonDto(Guid ContactId, string Name, string? Role, string Relation, DateTimeOffset? LastInteractionAt);

public sealed record PeopleDto(IReadOnlyList<PersonDto> Contacts, IReadOnlyList<InteractionDto> Interactions);

public sealed record ContactListQuery(string? Q, Guid? CompanyId, bool Archived, int Page, int PageSize);

/// <summary>What a user can set on a contact. The company is an id, a name matched or created, or neither.</summary>
public sealed record ContactInput(
    string Name, Guid? CompanyId, string? CompanyName, string? Role, string? Email, string? LinkedInUrl, string? Phone, string? Notes);

public sealed class ContactInputValidator : AbstractValidator<ContactInput>
{
    public ContactInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Add their name.").MaximumLength(ContactVocabulary.MaxName);
        RuleFor(x => x.CompanyName).MaximumLength(120);
        RuleFor(x => x.Role).MaximumLength(ContactVocabulary.MaxRole);
        RuleFor(x => x.Email).MaximumLength(ContactVocabulary.MaxEmail)
            .Matches(@"^\s*[^\s@]+@[^\s@]+\.[^\s@]+\s*$").When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("That doesn't look like an email address.");
        RuleFor(x => x.LinkedInUrl).MaximumLength(ContactVocabulary.MaxLinkedInUrl)
            .Must(url => ContactRules.IsLinkedInUrl(ContactRules.NormalizeLinkedInUrl(url)!)).When(x => !string.IsNullOrWhiteSpace(x.LinkedInUrl))
            .WithMessage("Use their https://linkedin.com profile address.");
        RuleFor(x => x.Phone).MaximumLength(ContactVocabulary.MaxPhone)
            .Matches(@"^\s*[+()\d][+()\d\s.-]{3,}\s*$").When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Use digits, spaces and + ( ) - only.");
        RuleFor(x => x.Notes).MaximumLength(ContactVocabulary.MaxNotes);
    }
}

public sealed record LinkOpportunityCommand(string Relation);

public sealed class LinkOpportunityValidator : AbstractValidator<LinkOpportunityCommand>
{
    public LinkOpportunityValidator()
    {
        RuleFor(x => x.Relation).Must(ContactVocabulary.Relations.Contains)
            .WithMessage($"Relation must be one of: {string.Join(", ", ContactVocabulary.Relations)}.");
    }
}

public sealed record InteractionInput(string Channel, string Direction, DateTimeOffset OccurredAt, string Summary, Guid? OpportunityId);

public sealed class InteractionInputValidator : AbstractValidator<InteractionInput>
{
    /// <summary>Clocks differ a little: "just now" on the user's device mustn't read as the future here.</summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    private static readonly DateTimeOffset Earliest = new(1990, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public InteractionInputValidator(IClock clock)
    {
        RuleFor(x => x.Channel).Must(ContactVocabulary.Channels.Contains)
            .WithMessage($"Channel must be one of: {string.Join(", ", ContactVocabulary.Channels)}.");
        RuleFor(x => x.Direction).Must(ContactVocabulary.Directions.Contains)
            .WithMessage($"Direction must be one of: {string.Join(", ", ContactVocabulary.Directions)}.");
        RuleFor(x => x.OccurredAt)
            .Must(at => at >= Earliest).WithMessage("Say when it happened.")
            .Must(at => at <= clock.UtcNow + ClockSkew).WithMessage("It can't be in the future. Planned conversations are reminders.");
        RuleFor(x => x.Summary).NotEmpty().WithMessage("Say what was said, even briefly.").MaximumLength(ContactVocabulary.MaxSummary);
    }
}

/// <summary>Audit action codes of the Contacts module. Only ids go in the metadata: these rows are about other people.</summary>
public static class ContactsAudit
{
    public const string Created = "contact.created";
    public const string Updated = "contact.updated";
    public const string Archived = "contact.archived";
    public const string Restored = "contact.restored";
    public const string Linked = "contact.linked";
    public const string Unlinked = "contact.unlinked";
    public const string InteractionLogged = "interaction.logged";
    public const string InteractionDeleted = "interaction.deleted";
}
