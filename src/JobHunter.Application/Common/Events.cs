using JobHunter.Domain.Common;

namespace JobHunter.Application.Common;

/// <summary>
/// Reacts to one kind of event, delivered at least once by the outbox (spec 02 §4). It runs
/// outside any request, so it takes the user from the event, never from <see cref="ICurrentUser"/>,
/// and it saves nothing itself: the dispatcher commits its changes together with its receipt.
/// </summary>
public interface IEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct);
}
