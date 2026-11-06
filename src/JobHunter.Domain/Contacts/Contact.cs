using JobHunter.Domain.Common;
using JobHunter.Domain.Contacts.Events;

namespace JobHunter.Domain.Contacts;

/// <summary>Someone involved in the user's search: a recruiter, a hiring manager, a referral.</summary>
public sealed class Contact : IHasTimestamps
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }

    public required string Name { get; set; }
    public Guid? CompanyId { get; set; }
    public string? Role { get; set; }

    /// <summary>Unique per user when set, ignoring case.</summary>
    public string? Email { get; set; }

    /// <summary>Always an https linkedin.com address; see <see cref="ContactRules"/>. It ends up in an href.</summary>
    public string? LinkedInUrl { get; set; }

    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }

    public uint Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>A contact's part in one opportunity.</summary>
public sealed class ContactOpportunity : IHasTimestamps
{
    public required Guid ContactId { get; init; }
    public required Guid OpportunityId { get; init; }
    public required Guid UserId { get; init; }

    /// <summary>One of <see cref="ContactVocabulary.Relations"/>.</summary>
    public required string Relation { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>One conversation that happened. A log entry: it's added or removed, never edited.</summary>
public sealed class Interaction : IHasTimestamps, IHasDomainEvents
{
    private readonly List<IDomainEvent> _events = [];

    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required Guid ContactId { get; init; }
    public Guid? OpportunityId { get; init; }

    /// <summary>One of <see cref="ContactVocabulary.Channels"/>.</summary>
    public required string Channel { get; init; }

    /// <summary>One of <see cref="ContactVocabulary.Directions"/>.</summary>
    public required string Direction { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
    public required string Summary { get; init; }

    /// <summary>The Outreach Studio send it records, when it was sent from a draft there.</summary>
    public string? OutreachSendId { get; init; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _events;

    public void ClearDomainEvents() => _events.Clear();

    /// <summary>A new entry in the log, announced to the modules that react to conversations.</summary>
    public static Interaction Log(
        Guid userId, Guid contactId, Guid? opportunityId, string channel, string direction,
        DateTimeOffset occurredAt, string summary, string? outreachSendId = null)
    {
        var interaction = new Interaction
        {
            UserId = userId,
            ContactId = contactId,
            OpportunityId = opportunityId,
            Channel = channel,
            Direction = direction,
            OccurredAt = occurredAt,
            Summary = summary,
            OutreachSendId = outreachSendId,
        };
        interaction._events.Add(new InteractionLogged(interaction.Id, userId, contactId, opportunityId, channel, direction, occurredAt));
        return interaction;
    }
}
