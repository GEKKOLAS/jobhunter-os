using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using JobHunter.Application.Common;
using JobHunter.Domain.Common;
using JobHunter.Domain.Contacts.Events;
using JobHunter.Domain.Opportunities.Events;
using JobHunter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace JobHunter.Infrastructure.Outbox;

/// <summary>An event waiting to be handled, saved in the same transaction as the change that raised it (spec 02 §4).</summary>
public sealed class OutboxMessage
{
    public required Guid Id { get; init; }
    public required string Type { get; init; }
    public required string Payload { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>Set after too many failures: it stops being retried until someone looks.</summary>
    public DateTimeOffset? ParkedAt { get; set; }
}

/// <summary>That one handler has handled one message, so a redelivery is skipped.</summary>
public sealed class InboxReceipt
{
    public required Guid MessageId { get; init; }
    public required string Handler { get; init; }
    public required DateTimeOffset ProcessedAt { get; init; }
}

/// <summary>
/// The stable names events are stored under. A name never changes once used: rows written by
/// an older build must still be read by a newer one.
/// </summary>
public static class EventTypes
{
    private static readonly Dictionary<string, Type> ByName = new()
    {
        ["opportunity.stage_changed"] = typeof(OpportunityStageChanged),
        ["interaction.logged"] = typeof(InteractionLogged),
    };

    private static readonly Dictionary<Type, string> ByType = ByName.ToDictionary(kv => kv.Value, kv => kv.Key);

    public static string NameOf(IDomainEvent domainEvent) =>
        ByType.TryGetValue(domainEvent.GetType(), out var name)
            ? name
            : throw new InvalidOperationException($"{domainEvent.GetType().Name} has no stored name in {nameof(EventTypes)}.");

    public static Type? TypeOf(string name) => ByName.GetValueOrDefault(name);
}

/// <summary>Moves events raised by tracked entities into <c>outbox_messages</c> as part of the same save.</summary>
public sealed class OutboxInterceptor(IClock clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context is null) return;

        var sources = context.ChangeTracker.Entries<IHasDomainEvents>()
            .Where(e => e.State != EntityState.Detached && e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var now = clock.UtcNow;
        foreach (var source in sources)
        {
            foreach (var domainEvent in source.DomainEvents)
            {
                context.Set<OutboxMessage>().Add(new OutboxMessage
                {
                    Id = Guid.CreateVersion7(),
                    Type = EventTypes.NameOf(domainEvent),
                    Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                    OccurredAt = domainEvent.OccurredAt,
                    CreatedAt = now,
                    NextAttemptAt = now,
                });
            }
            source.ClearDomainEvents();
        }
    }
}

/// <summary>
/// Delivers outbox messages to their handlers (spec 02 §4): one message per transaction, claimed
/// with <c>FOR UPDATE SKIP LOCKED</c> so instances never share one; the handlers' changes, their
/// receipts and the processed mark commit together.
/// </summary>
public sealed class OutboxDispatcher(IServiceScopeFactory scopes, IClock clock, ILogger<OutboxDispatcher> logger)
{
    public const int MaxAttempts = 10;
    private static readonly TimeSpan FirstRetry = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LongestRetry = TimeSpan.FromHours(1);

    /// <summary>Handles what's due, up to <paramref name="max"/> messages. Returns how many it handled.</summary>
    public async Task<int> DispatchAsync(int max, CancellationToken ct)
    {
        var handled = 0;
        while (handled < max && !ct.IsCancellationRequested)
        {
            var outcome = await DispatchOneAsync(ct);
            if (outcome is null) break;
            handled++;
        }
        return handled;
    }

    /// <summary>True when a message was handled, false when it failed, null when none was due.</summary>
    private async Task<bool?> DispatchOneAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = clock.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var message = await db.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT * FROM outbox_messages
                WHERE processed_at IS NULL AND parked_at IS NULL AND next_attempt_at <= {now}
                ORDER BY occurred_at, id
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .FirstOrDefaultAsync(ct);
        if (message is null) return null;

        try
        {
            await HandleAsync(scope.ServiceProvider, db, message, ct);
            message.ProcessedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            await RecordFailureAsync(message.Id, message.Attempts + 1, ex, CancellationToken.None);
            return false;
        }
    }

    private static async Task HandleAsync(IServiceProvider services, AppDbContext db, OutboxMessage message, CancellationToken ct)
    {
        var type = EventTypes.TypeOf(message.Type) ?? throw new InvalidOperationException($"Unknown event type '{message.Type}'.");
        var domainEvent = JsonSerializer.Deserialize(message.Payload, type) ?? throw new InvalidOperationException("Empty event payload.");

        var handlerType = typeof(IEventHandler<>).MakeGenericType(type);
        var handle = handlerType.GetMethod(nameof(IEventHandler<IDomainEvent>.HandleAsync))!;
        foreach (var handler in services.GetServices(handlerType))
        {
            var name = handler!.GetType().FullName!;
            if (await db.InboxReceipts.AnyAsync(r => r.MessageId == message.Id && r.Handler == name, ct)) continue;

            try
            {
                await (Task)handle.Invoke(handler, [domainEvent, ct])!;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is { } inner)
            {
                // A handler that throws before its first await surfaces wrapped; the stored error should say what broke.
                ExceptionDispatchInfo.Capture(inner).Throw();
            }
            db.InboxReceipts.Add(new InboxReceipt { MessageId = message.Id, Handler = name, ProcessedAt = DateTimeOffset.UtcNow });
        }
    }

    private async Task RecordFailureAsync(Guid id, int attempts, Exception ex, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = clock.UtcNow;
        var parked = attempts >= MaxAttempts;
        var delay = TimeSpan.FromTicks(Math.Min(LongestRetry.Ticks, FirstRetry.Ticks * (1L << Math.Min(attempts - 1, 20))));
        var error = $"{ex.GetType().Name}: {ex.Message}";
        if (error.Length > 2000) error = error[..2000];

        await db.OutboxMessages.Where(m => m.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(m => m.Attempts, attempts)
            .SetProperty(m => m.LastError, error)
            .SetProperty(m => m.NextAttemptAt, now + delay)
            .SetProperty(m => m.ParkedAt, parked ? now : null), ct);

        if (parked) logger.LogError(ex, "Outbox message {MessageId} parked after {Attempts} attempts", id, attempts);
        else logger.LogWarning(ex, "Outbox message {MessageId} failed (attempt {Attempts}); retrying in {Delay}", id, attempts, delay);
    }

    /// <summary>Processed messages and their receipts only matter for spotting redeliveries; after a month they go.</summary>
    public async Task<int> PruneAsync(TimeSpan olderThan, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cutoff = clock.UtcNow - olderThan;
        var old = db.OutboxMessages.Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff).Select(m => m.Id);
        await db.InboxReceipts.Where(r => old.Contains(r.MessageId)).ExecuteDeleteAsync(ct);
        return await db.OutboxMessages.Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff).ExecuteDeleteAsync(ct);
    }
}

/// <summary>Degraded while any message is parked: something needs a person to look at it.</summary>
public sealed class OutboxHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var parked = await db.OutboxMessages.CountAsync(m => m.ParkedAt != null, cancellationToken);
        return parked == 0
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Degraded($"{parked} outbox message(s) parked after repeated failures.");
    }
}
