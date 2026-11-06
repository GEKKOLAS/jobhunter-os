using JobHunter.Domain.Common;

namespace JobHunter.Domain.Contacts.Events;

/// <param name="OccurredAt">When the conversation happened, which is what responses are measured by.</param>
public sealed record InteractionLogged(
    Guid InteractionId, Guid UserId, Guid ContactId, Guid? OpportunityId,
    string Channel, string Direction, DateTimeOffset OccurredAt) : IDomainEvent;
