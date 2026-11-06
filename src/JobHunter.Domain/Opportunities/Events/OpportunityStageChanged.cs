using JobHunter.Domain.Common;

namespace JobHunter.Domain.Opportunities.Events;

/// <param name="FromStage">Null when the opportunity was just added at <paramref name="ToStage"/>.</param>
/// <param name="FirstResponseAt">As it stood at the move, so a handler knows whether they already answered.</param>
public sealed record OpportunityStageChanged(
    Guid OpportunityId, Guid UserId, string Title, string? FromStage, string ToStage,
    DateTimeOffset? FirstResponseAt, DateTimeOffset OccurredAt) : IDomainEvent;
