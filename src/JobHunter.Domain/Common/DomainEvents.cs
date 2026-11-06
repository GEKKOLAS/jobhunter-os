namespace JobHunter.Domain.Common;

/// <summary>
/// Something that happened in one module that others may react to (spec 02 §4). It carries
/// the user it belongs to, because handlers run outside any request.
/// </summary>
public interface IDomainEvent
{
    Guid UserId { get; }
    DateTimeOffset OccurredAt { get; }
}

/// <summary>An entity that raises events. They go to the outbox in the same transaction as the change.</summary>
public interface IHasDomainEvents
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
