using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobHunter.Application.Common;
using JobHunter.Application.FollowUps;
using JobHunter.Domain.Opportunities.Events;
using JobHunter.Infrastructure.Outbox;
using JobHunter.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.IntegrationTests;

/// <summary>Phase 7 (spec 03, Follow-ups; spec 02 §4): events through the outbox, the rules they drive, and reminders coming due.</summary>
[Collection(ApiCollection.Name)]
public class FollowUpsTests(ApiFactory factory)
{
    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<string?> ErrorCode(HttpResponseMessage response) =>
        (await Json(response)).GetProperty("errorCode").GetString();

    private static string Id(JsonElement element) => element.GetProperty("id").GetString()!;

    private async Task DispatchAsync() =>
        await factory.Services.GetRequiredService<OutboxDispatcher>().DispatchAsync(10_000, default);

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>The outbox rows an opportunity's moves produced. The payload is jsonb, so it's read as JSON.</summary>
    private Task<List<Guid>> MessagesForAsync(string opportunityId) => WithDbAsync(db => db.Database
        .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM outbox_messages WHERE payload->>'OpportunityId' = {opportunityId}")
        .ToListAsync());

    private static async Task<string> OpportunityAsync(TestSession session, string stage = "saved", string title = "Backend Engineer")
    {
        var response = await session.PostAsync("/api/v1/opportunities", new { title, companyName = "Contoso", stage });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return Id(await Json(response));
    }

    private static async Task<string> ContactAsync(TestSession session, string name = "Ada Recruiter", string? email = null)
    {
        var response = await session.PostAsync("/api/v1/contacts", new { name, email });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return Id(await Json(response));
    }

    private static async Task<List<JsonElement>> RemindersAsync(TestSession session, string query = "") =>
        (await Json(await session.GetAsync($"/api/v1/reminders{query}"))).GetProperty("items").EnumerateArray().ToList();

    private static Task<HttpResponseMessage> LogAsync(TestSession session, string contactId, string? opportunityId, string direction = "inbound", DateTimeOffset? at = null) =>
        session.PostAsync($"/api/v1/contacts/{contactId}/interactions", new
        {
            channel = "email",
            direction,
            occurredAt = at ?? DateTimeOffset.UtcNow,
            summary = "They wrote back",
            opportunityId,
        });

    [Fact]
    public async Task Applying_creates_a_follow_up_a_week_later_once_the_outbox_delivers_it()
    {
        var session = await factory.SignedInAsync();
        var id = await OpportunityAsync(session);

        await session.PostAsync($"/api/v1/opportunities/{id}/stage", new { stage = "applied" });
        Assert.Empty(await RemindersAsync(session)); // nothing until the event is delivered

        await DispatchAsync();

        var reminder = Assert.Single(await RemindersAsync(session));
        Assert.Equal(("follow_up", "rule", "Follow up on Backend Engineer"), (
            reminder.GetProperty("kind").GetString(), reminder.GetProperty("origin").GetString(), reminder.GetProperty("title").GetString()));
        Assert.Equal(id, reminder.GetProperty("opportunityId").GetString());
        Assert.Equal("Backend Engineer", reminder.GetProperty("opportunityTitle").GetString());
        var due = reminder.GetProperty("dueAt").GetDateTimeOffset();
        Assert.InRange(due, DateTimeOffset.UtcNow.AddDays(7).AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(7).AddMinutes(5));
    }

    [Fact]
    public async Task An_opportunity_added_as_applied_gets_the_follow_up_too_and_a_rule_fires_only_once()
    {
        var session = await factory.SignedInAsync();
        var id = await OpportunityAsync(session, stage: "applied");

        // Forward and back by a one-step correction: two more "reached applied" events.
        await session.PostAsync($"/api/v1/opportunities/{id}/stage", new { stage = "screening" });
        await session.PostAsync($"/api/v1/opportunities/{id}/stage", new { stage = "applied" });
        await DispatchAsync();

        Assert.Single(await RemindersAsync(session));
    }

    [Fact]
    public async Task Interviewing_creates_a_thank_you_for_the_next_day()
    {
        var session = await factory.SignedInAsync();
        var id = await OpportunityAsync(session, stage: "applied");
        await session.PostAsync($"/api/v1/opportunities/{id}/stage", new { stage = "interviewing" });
        await DispatchAsync();

        var thanks = (await RemindersAsync(session)).Single(r => r.GetProperty("kind").GetString() == "thank_you");
        Assert.Equal("Send a thank-you for Backend Engineer", thanks.GetProperty("title").GetString());
        Assert.InRange(thanks.GetProperty("dueAt").GetDateTimeOffset(), DateTimeOffset.UtcNow.AddHours(23), DateTimeOffset.UtcNow.AddHours(25));
    }

    [Fact]
    public async Task An_inbound_reply_records_the_first_response_and_closes_the_follow_up()
    {
        var session = await factory.SignedInAsync();
        var contact = await ContactAsync(session);
        var id = await OpportunityAsync(session, stage: "applied");
        await DispatchAsync();
        Assert.Single(await RemindersAsync(session));

        Assert.Equal(HttpStatusCode.Created, (await LogAsync(session, contact, id)).StatusCode);
        await DispatchAsync();

        var opportunity = await Json(await session.GetAsync($"/api/v1/opportunities/{id}"));
        Assert.NotEqual(JsonValueKind.Null, opportunity.GetProperty("firstResponseAt").ValueKind);
        Assert.Empty(await RemindersAsync(session));
        Assert.Single(await RemindersAsync(session, "?status=done"));
    }

    [Fact]
    public async Task Only_a_reply_after_applying_counts_as_a_response_and_an_outbound_one_never_does()
    {
        var session = await factory.SignedInAsync();
        var contact = await ContactAsync(session);
        var saved = await OpportunityAsync(session);
        var applied = await OpportunityAsync(session, stage: "applied");

        await LogAsync(session, contact, saved); // not applied to yet
        await LogAsync(session, contact, applied, direction: "outbound");
        await LogAsync(session, contact, applied, at: DateTimeOffset.UtcNow.AddDays(-30)); // before applying
        await DispatchAsync();

        Assert.Equal(JsonValueKind.Null, (await Json(await session.GetAsync($"/api/v1/opportunities/{saved}"))).GetProperty("firstResponseAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, (await Json(await session.GetAsync($"/api/v1/opportunities/{applied}"))).GetProperty("firstResponseAt").ValueKind);
    }

    [Fact]
    public async Task The_same_message_delivered_twice_has_one_effect()
    {
        var session = await factory.SignedInAsync();
        var id = await OpportunityAsync(session, stage: "applied");
        await DispatchAsync();

        // A redelivery: what a crash between the effects and the processed mark would cause.
        var ids = await MessagesForAsync(id);
        var reset = await WithDbAsync(db => db.OutboxMessages
            .Where(m => ids.Contains(m.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.ProcessedAt, (DateTimeOffset?)null)));
        Assert.Equal(1, reset);
        await DispatchAsync();

        Assert.Single(await RemindersAsync(session));
        Assert.True(await WithDbAsync(db => db.OutboxMessages.AnyAsync(m => ids.Contains(m.Id) && m.ProcessedAt != null)));
    }

    [Fact]
    public async Task A_failing_handler_rolls_back_the_message_retries_later_and_parks_after_ten_attempts()
    {
        var session = await factory.SignedInAsync();
        var id = await OpportunityAsync(session, stage: "applied");
        await using var failing = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddScoped<IEventHandler<OpportunityStageChanged>, ThrowingHandler>()));
        var dispatcher = failing.Services.GetRequiredService<OutboxDispatcher>();
        var ids = await MessagesForAsync(id);

        try
        {
            await dispatcher.DispatchAsync(10_000, default);

            // The rules' reminder was in the same transaction as the failure, so it isn't there.
            Assert.Empty(await RemindersAsync(session));
            var message = await WithDbAsync(db => db.OutboxMessages.AsNoTracking().SingleAsync(m => ids.Contains(m.Id)));
            Assert.Equal(1, message.Attempts);
            Assert.Null(message.ProcessedAt);
            Assert.Contains("Handler broke", message.LastError);
            Assert.True(message.NextAttemptAt > DateTimeOffset.UtcNow.AddSeconds(20));

            // The tenth failure parks it, and health says someone should look.
            await WithDbAsync(db => db.OutboxMessages.Where(m => ids.Contains(m.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Attempts, 9).SetProperty(m => m.NextAttemptAt, DateTimeOffset.UtcNow.AddMinutes(-1))));
            await dispatcher.DispatchAsync(10_000, default);

            Assert.NotNull((await WithDbAsync(db => db.OutboxMessages.AsNoTracking().SingleAsync(m => ids.Contains(m.Id)))).ParkedAt);
            var health = await factory.CreateClient().GetAsync("/healthz");
            Assert.Equal("Degraded", await health.Content.ReadAsStringAsync());
        }
        finally
        {
            // Parked rows are shared state: leave health as it was for the other tests.
            await WithDbAsync(db => db.OutboxMessages.Where(m => ids.Contains(m.Id)).ExecuteDeleteAsync());
        }
    }

    [Fact]
    public async Task Reminders_are_made_listed_by_when_they_come_due_and_edited()
    {
        var session = await factory.SignedInAsync();
        var opportunity = await OpportunityAsync(session);
        var soon = DateTimeOffset.UtcNow.AddHours(2);
        var later = DateTimeOffset.UtcNow.AddDays(10);

        var created = await session.PostAsync("/api/v1/reminders", new { title = "Prep for the call", dueAt = later, kind = "prep", opportunityId = opportunity });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var first = await Json(created);
        Assert.Equal(("user", "open", "Backend Engineer"), (first.GetProperty("origin").GetString(), first.GetProperty("status").GetString(), first.GetProperty("opportunityTitle").GetString()));
        await session.PostAsync("/api/v1/reminders", new { title = "Check in", dueAt = soon, kind = "custom" });

        Assert.Equal(["Check in", "Prep for the call"], (await RemindersAsync(session)).Select(r => r.GetProperty("title").GetString()));
        var until = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));
        Assert.Equal(["Check in"], (await RemindersAsync(session, $"?until={until}")).Select(r => r.GetProperty("title").GetString()));
        Assert.Equal(["Prep for the call"], (await RemindersAsync(session, $"?opportunityId={opportunity}")).Select(r => r.GetProperty("title").GetString()));

        var edited = await session.SendAsync(HttpMethod.Put, $"/api/v1/reminders/{Id(first)}",
            new { title = "Prep notes", dueAt = DateTimeOffset.UtcNow.AddHours(1), kind = "prep", version = first.GetProperty("version").GetUInt32() });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal(["Prep notes", "Check in"], (await RemindersAsync(session)).Select(r => r.GetProperty("title").GetString()));

        var stale = await session.SendAsync(HttpMethod.Put, $"/api/v1/reminders/{Id(first)}",
            new { title = "Stale", dueAt = DateTimeOffset.UtcNow.AddHours(1), kind = "prep", version = first.GetProperty("version").GetUInt32() });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Reminders_are_done_snoozed_and_dismissed_with_their_rules_enforced()
    {
        var session = await factory.SignedInAsync();
        var id = Id(await Json(await session.PostAsync("/api/v1/reminders", new { title = "Call back", dueAt = DateTimeOffset.UtcNow.AddHours(1), kind = "custom" })));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await session.PostAsync($"/api/v1/reminders/{id}/snooze", new { until = DateTimeOffset.UtcNow.AddHours(-1) })).StatusCode);
        var snoozed = await Json(await session.PostAsync($"/api/v1/reminders/{id}/snooze", new { until = DateTimeOffset.UtcNow.AddDays(2) }));
        Assert.True(snoozed.GetProperty("effectiveDueAt").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddDays(1));

        Assert.Equal(HttpStatusCode.OK, (await session.PostAsync($"/api/v1/reminders/{id}/done", new { })).StatusCode);
        var again = await session.PostAsync($"/api/v1/reminders/{id}/done", new { });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("reminder.closed", await ErrorCode(again));

        var other = Id(await Json(await session.PostAsync("/api/v1/reminders", new { title = "Drop it", dueAt = DateTimeOffset.UtcNow.AddHours(1), kind = "custom" })));
        Assert.Equal(HttpStatusCode.NoContent, (await session.SendAsync(HttpMethod.Delete, $"/api/v1/reminders/{other}")).StatusCode);
        Assert.Empty(await RemindersAsync(session));
        Assert.Single(await RemindersAsync(session, "?status=dismissed"));
    }

    [Fact]
    public async Task Invalid_reminders_and_other_peoples_things_are_refused()
    {
        var session = await factory.SignedInAsync();
        var other = await factory.SignedInAsync();
        var theirs = await OpportunityAsync(other);
        var theirReminder = Id(await Json(await other.PostAsync("/api/v1/reminders", new { title = "Theirs", dueAt = DateTimeOffset.UtcNow.AddHours(1), kind = "custom" })));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await session.PostAsync("/api/v1/reminders", new { title = "", dueAt = DateTimeOffset.UtcNow, kind = "custom" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await session.PostAsync("/api/v1/reminders", new { title = "x", dueAt = DateTimeOffset.UtcNow, kind = "nag" })).StatusCode);
        var borrowed = await session.PostAsync("/api/v1/reminders", new { title = "x", dueAt = DateTimeOffset.UtcNow, kind = "custom", opportunityId = theirs });
        Assert.Equal("reminder.unknown_opportunity", await ErrorCode(borrowed));

        Assert.Equal(HttpStatusCode.NotFound, (await session.PostAsync($"/api/v1/reminders/{theirReminder}/done", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await session.SendAsync(HttpMethod.Delete, $"/api/v1/reminders/{theirReminder}")).StatusCode);
        Assert.Empty(await RemindersAsync(session));
    }

    [Fact]
    public async Task A_reminder_that_comes_due_notifies_once_until_snoozing_moves_it()
    {
        var session = await factory.SignedInAsync();
        var id = Id(await Json(await session.PostAsync("/api/v1/reminders", new { title = "Overdue thing", dueAt = DateTimeOffset.UtcNow.AddMinutes(-5), kind = "custom" })));
        await session.PostAsync("/api/v1/reminders", new { title = "Not yet", dueAt = DateTimeOffset.UtcNow.AddDays(3), kind = "custom" });

        async Task RunPassAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DueRemindersPass>().RunAsync(default);
        }

        await RunPassAsync();
        await RunPassAsync();

        var notifications = await Json(await session.GetAsync("/api/v1/notifications"));
        Assert.Equal(1, notifications.GetProperty("unreadCount").GetInt32());
        var notice = Assert.Single(notifications.GetProperty("items").EnumerateArray());
        Assert.Equal(("Overdue thing", id), (notice.GetProperty("title").GetString(), notice.GetProperty("reminderId").GetString()));

        Assert.Equal(HttpStatusCode.NoContent, (await session.PostAsync("/api/v1/notifications/read", new { })).StatusCode);
        Assert.Equal(0, (await Json(await session.GetAsync("/api/v1/notifications"))).GetProperty("unreadCount").GetInt32());

        // Snoozed, it comes due again later and notifies again then, not now.
        await session.PostAsync($"/api/v1/reminders/{id}/snooze", new { until = DateTimeOffset.UtcNow.AddHours(1) });
        await RunPassAsync();
        Assert.Single((await Json(await session.GetAsync("/api/v1/notifications"))).GetProperty("items").EnumerateArray());

        // Nobody else sees them.
        var other = await factory.SignedInAsync();
        Assert.Empty((await Json(await other.GetAsync("/api/v1/notifications"))).GetProperty("items").EnumerateArray());
    }

    private sealed class ThrowingHandler : IEventHandler<OpportunityStageChanged>
    {
        public Task HandleAsync(OpportunityStageChanged domainEvent, CancellationToken ct) => throw new InvalidOperationException("Handler broke");
    }
}
