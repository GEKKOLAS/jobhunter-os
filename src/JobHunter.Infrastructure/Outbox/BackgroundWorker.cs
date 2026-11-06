using JobHunter.Application.FollowUps;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobHunter.Infrastructure.Outbox;

public sealed class WorkerOptions
{
    public const string SectionName = "Workers";

    /// <summary>Off in tests, which run the passes themselves so nothing happens behind their back.</summary>
    public bool Enabled { get; init; } = true;

    public int OutboxSeconds { get; init; } = 10;
    public int DueRemindersMinutes { get; init; } = 5;
}

/// <summary>
/// The API's background work (spec 02 §4): outbox delivery every 10 seconds, due reminders every
/// 5 minutes, pruning daily. A failing pass is logged and the loop keeps going.
/// </summary>
public sealed class BackgroundWorker(
    IServiceScopeFactory scopes, OutboxDispatcher outbox, IOptions<WorkerOptions> options, ILogger<BackgroundWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan KeepProcessed = TimeSpan.FromDays(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;

        var nextReminders = DateTimeOffset.MinValue;
        var nextPrune = DateTimeOffset.UtcNow.AddMinutes(1);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.OutboxSeconds));

        do
        {
            await RunAsync("outbox", () => outbox.DispatchAsync(100, stoppingToken), stoppingToken);

            if (DateTimeOffset.UtcNow >= nextReminders)
            {
                nextReminders = DateTimeOffset.UtcNow.AddMinutes(settings.DueRemindersMinutes);
                await RunAsync("due reminders", async () =>
                {
                    await using var scope = scopes.CreateAsyncScope();
                    return await scope.ServiceProvider.GetRequiredService<DueRemindersPass>().RunAsync(stoppingToken);
                }, stoppingToken);
            }

            if (DateTimeOffset.UtcNow >= nextPrune)
            {
                nextPrune = DateTimeOffset.UtcNow.AddDays(1);
                await RunAsync("outbox pruning", () => outbox.PruneAsync(KeepProcessed, stoppingToken), stoppingToken);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunAsync(string pass, Func<Task<int>> run, CancellationToken stoppingToken)
    {
        try
        {
            var count = await run();
            if (count > 0) logger.LogInformation("Background {Pass}: {Count} handled", pass, count);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Background {Pass} failed; the next tick will try again", pass);
        }
    }
}
