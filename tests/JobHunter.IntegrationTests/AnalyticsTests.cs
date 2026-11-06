using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobHunter.Domain.Matching;
using JobHunter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.IntegrationTests;

/// <summary>Phase 8 (spec 03, Analytics): the summary, skills and dashboard queries against the database.</summary>
[Collection(ApiCollection.Name)]
public class AnalyticsTests(ApiFactory factory)
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<string?> ErrorCode(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString();

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static async Task<string> OpportunityAsync(
        TestSession session, string title = "Backend Engineer", string stage = "saved", string? priority = null)
    {
        var response = await session.PostAsync("/api/v1/opportunities", new { title, companyName = "Contoso", stage, priority });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("id").GetString()!;
    }

    private static async Task MoveAsync(TestSession session, string id, params string[] stages)
    {
        foreach (var stage in stages)
        {
            var response = await session.PostAsync($"/api/v1/opportunities/{id}/stage", new { stage });
            Assert.True(response.IsSuccessStatusCode, $"move to {stage}: {(int)response.StatusCode}");
        }
    }

    /// <summary>The API stamps every date with "now"; analytics is about time, so the tests move them into the past.</summary>
    private Task SetDatesAsync(string id, DateTimeOffset created, DateTimeOffset? applied = null, DateTimeOffset? answered = null) =>
        WithDbAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE opportunities SET created_at = {created}, applied_at = {applied}, first_response_at = {answered} WHERE id = {Guid.Parse(id)}"));

    private static DateTimeOffset DaysAgo(double days) => Now.AddDays(-days);

    private static int Count(JsonElement element, string property) => element.GetProperty(property).GetInt32();

    private static (int Numerator, int Denominator, double? Value) RateOf(JsonElement rate) => (
        rate.GetProperty("numerator").GetInt32(),
        rate.GetProperty("denominator").GetInt32(),
        rate.GetProperty("value").ValueKind == JsonValueKind.Null ? null : rate.GetProperty("value").GetDouble());

    // Summary

    [Fact]
    public async Task The_summary_counts_this_users_unarchived_applications_by_when_they_were_sent()
    {
        var session = await factory.SignedInAsync();

        var interviewed = await OpportunityAsync(session);
        await MoveAsync(session, interviewed, "applied", "screening", "interviewing");
        await SetDatesAsync(interviewed, DaysAgo(21), applied: DaysAgo(20), answered: DaysAgo(17));

        var unanswered = await OpportunityAsync(session, stage: "applied");
        await SetDatesAsync(unanswered, DaysAgo(41), applied: DaysAgo(40));

        var longAgo = await OpportunityAsync(session, stage: "applied");
        await SetDatesAsync(longAgo, DaysAgo(201), applied: DaysAgo(200));

        await OpportunityAsync(session); // saved today, not sent

        var archived = await OpportunityAsync(session, stage: "applied");
        await SetDatesAsync(archived, DaysAgo(11), applied: DaysAgo(10));
        Assert.Equal(HttpStatusCode.NoContent, (await session.DeleteAsync($"/api/v1/opportunities/{archived}")).StatusCode);

        var other = await factory.SignedInAsync();
        await OpportunityAsync(other, stage: "applied");

        var summary = await Json(await session.GetAsync("/api/v1/analytics/summary?days=90"));

        Assert.Equal(90, Count(summary, "days"));
        Assert.Equal(3, Count(summary, "saved"));
        Assert.Equal(2, Count(summary, "applications"));
        var rates = summary.GetProperty("rates");
        Assert.Equal((1, 2, null), RateOf(rates.GetProperty("response"))); // too few to say
        Assert.Equal((1, 2, null), RateOf(rates.GetProperty("interview")));
        Assert.Equal((0, 2, null), RateOf(rates.GetProperty("offer")));
        Assert.Equal((1, 1, null), RateOf(rates.GetProperty("noResponse"))); // only the one sent 40 days ago is old enough
        Assert.Equal([3, 2, 1, 1, 0], summary.GetProperty("funnel").EnumerateArray().Select(s => Count(s, "count")));
        Assert.Equal(1, Count(summary.GetProperty("timeToFirstResponse"), "count"));

        var weekly = summary.GetProperty("weekly").EnumerateArray().ToList();
        Assert.Equal(2, weekly.Sum(w => Count(w, "applications")));
        Assert.Equal(1, weekly.Sum(w => Count(w, "responses")));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", weekly[0].GetProperty("weekStart").GetString()!);

        var source = Assert.Single(summary.GetProperty("bySource").EnumerateArray());
        Assert.Equal(("manual", 2), (source.GetProperty("key").GetString(), Count(source, "applications")));

        var year = await Json(await session.GetAsync("/api/v1/analytics/summary?days=365"));
        Assert.Equal(3, Count(year, "applications"));
    }

    [Fact]
    public async Task Rates_appear_once_there_are_five_applications()
    {
        var session = await factory.SignedInAsync();
        for (var i = 0; i < 5; i++)
        {
            var id = await OpportunityAsync(session, stage: "applied");
            if (i < 2) await MoveAsync(session, id, "screening");
            await SetDatesAsync(id, DaysAgo(10 + i), applied: DaysAgo(9 + i), answered: i < 2 ? DaysAgo(8 + i) : null);
        }

        var summary = await Json(await session.GetAsync("/api/v1/analytics/summary?days=30"));

        Assert.Equal((2, 5, 0.4), RateOf(summary.GetProperty("rates").GetProperty("response")));
        Assert.Equal((0, 5, 0.0), RateOf(summary.GetProperty("rates").GetProperty("interview")));
    }

    [Fact]
    public async Task Applications_are_grouped_by_the_profiles_target_roles()
    {
        var session = await factory.SignedInAsync();
        var version = (await Json(await session.GetAsync("/api/v1/profile"))).GetProperty("version").GetUInt32();
        var saved = await session.SendAsync(HttpMethod.Put, "/api/v1/profile", new
        {
            headline = "Engineer",
            targetRoles = new[] { "Backend Engineer", "Data Engineer" },
            workModes = Array.Empty<string>(),
            languages = Array.Empty<object>(),
            version,
        });
        Assert.True(saved.IsSuccessStatusCode);
        await OpportunityAsync(session, title: "Senior Backend Engineer", stage: "applied");
        await OpportunityAsync(session, title: "Product Manager", stage: "applied");

        var byRole = (await Json(await session.GetAsync("/api/v1/analytics/summary"))).GetProperty("byRole").EnumerateArray()
            .Select(r => (r.GetProperty("key").GetString(), Count(r, "applications")))
            .ToList();

        Assert.Equal([("Backend Engineer", 1), ("Data Engineer", 0), (null, 1)], byRole);
    }

    [Theory]
    [InlineData("days=7", "analytics.invalid_range")]
    [InlineData("days=0", "analytics.invalid_range")]
    [InlineData("tz=Mars%2FOlympus_Mons", "analytics.unknown_time_zone")]
    public async Task A_range_or_time_zone_it_doesnt_know_is_refused(string query, string code)
    {
        var session = await factory.SignedInAsync();

        var response = await session.GetAsync($"/api/v1/analytics/summary?{query}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(code, await ErrorCode(response));
    }

    [Fact]
    public async Task An_iana_time_zone_is_accepted_and_analytics_need_a_session()
    {
        var session = await factory.SignedInAsync();

        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/v1/analytics/summary?days=30&tz=Europe%2FMadrid")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.NewSession().GetAsync("/api/v1/analytics/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.NewSession().GetAsync("/api/v1/analytics/skills")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.NewSession().GetAsync("/api/v1/dashboard")).StatusCode);
    }

    // Skills

    [Fact]
    public async Task Skills_count_confirmed_requirements_of_postings_saved_in_the_range_and_gaps_from_the_fits()
    {
        var session = await factory.SignedInAsync();
        Assert.Equal(HttpStatusCode.Created, (await session.PostAsync("/api/v1/profile/skills", new { name = "C#", level = 4, years = 6 })).StatusCode);

        object Must(string skill) => new { text = $"Needs {skill}", kind = "must", skill };
        object Nice(string skill) => new { text = $"Likes {skill}", kind = "nice", skill };
        async Task<string> PostingAsync(params object[] items)
        {
            var id = await OpportunityAsync(session);
            var put = await session.SendAsync(HttpMethod.Put, $"/api/v1/opportunities/{id}/requirements", new { items });
            Assert.True(put.IsSuccessStatusCode);
            return id;
        }

        var first = await PostingAsync(Must("C#"), Must("PostgreSQL"), Nice("Kubernetes"));
        await PostingAsync(Must("PostgreSQL"));
        var old = await PostingAsync(Must("Rust"));
        await SetDatesAsync(old, DaysAgo(200));

        // A row a parser suggested and nobody confirmed counts for nothing.
        await WithDbAsync(async db =>
        {
            var opportunity = await db.Opportunities.SingleAsync(o => o.Id == Guid.Parse(first));
            db.OpportunityRequirements.Add(new OpportunityRequirement
            {
                UserId = opportunity.UserId, OpportunityId = opportunity.Id, Text = "Go would be nice", Kind = "must",
                SkillKey = "go", Origin = RequirementVocabulary.OriginRules, Confirmed = false, Position = 9,
            });
            return await db.SaveChangesAsync();
        });

        var skills = await Json(await session.GetAsync("/api/v1/analytics/skills?days=90"));

        Assert.Equal(2, Count(skills, "postings"));
        var requested = skills.GetProperty("requested").EnumerateArray()
            .Select(s => (s.GetProperty("key").GetString(), Count(s, "postings"), Count(s, "must"), Count(s, "nice"), s.GetProperty("inProfile").GetBoolean()))
            .ToList();
        Assert.Equal(("postgresql", 2, 2, 0, false), requested[0]);
        Assert.Equal(("csharp", 1, 1, 0, true), requested[1]);
        Assert.Equal(("kubernetes", 1, 0, 1, false), requested[2]);
        Assert.Equal(3, requested.Count);
        Assert.Equal("C#", skills.GetProperty("requested")[1].GetProperty("name").GetString());

        var gaps = skills.GetProperty("gaps").EnumerateArray().Select(g => (g.GetProperty("key").GetString(), Count(g, "postings"))).ToList();
        Assert.Equal([("postgresql", 2), ("kubernetes", 1)], gaps);

        Assert.Equal(3, Count(await Json(await session.GetAsync("/api/v1/analytics/skills?days=365")), "postings"));
        Assert.Equal(0, Count(await Json(await (await factory.SignedInAsync()).GetAsync("/api/v1/analytics/skills")), "postings"));
    }

    // Dashboard

    [Fact]
    public async Task Gone_quiet_is_applied_ten_days_or_more_unanswered_with_no_open_reminder()
    {
        var session = await factory.SignedInAsync();
        async Task<string> AppliedAsync(string title, double daysAgo)
        {
            var id = await OpportunityAsync(session, title: title, stage: "applied");
            await SetDatesAsync(id, DaysAgo(daysAgo + 1), applied: DaysAgo(daysAgo));
            return id;
        }
        async Task<string> ReminderAsync(string opportunityId)
        {
            var response = await session.PostAsync("/api/v1/reminders", new
            {
                title = "Chase them", dueAt = Now.AddDays(2), kind = "follow_up", opportunityId,
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await Json(response)).GetProperty("id").GetString()!;
        }

        await AppliedAsync("Quiet for twelve days", 12.1);
        var chased = await AppliedAsync("Has a reminder", 12);
        await ReminderAsync(chased);
        await AppliedAsync("Only five days", 5);
        var dismissed = await AppliedAsync("Reminder dismissed", 20);
        await session.DeleteAsync($"/api/v1/reminders/{await ReminderAsync(dismissed)}");
        var answered = await OpportunityAsync(session, stage: "applied");
        await MoveAsync(session, answered, "screening");
        await SetDatesAsync(answered, DaysAgo(16), applied: DaysAgo(15), answered: DaysAgo(14));

        var quiet = (await Json(await session.GetAsync("/api/v1/dashboard"))).GetProperty("quiet");

        Assert.Equal(2, Count(quiet, "total"));
        var items = quiet.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["Reminder dismissed", "Quiet for twelve days"], items.Select(i => i.GetProperty("title").GetString()));
        Assert.Equal([20, 12], items.Select(i => Count(i, "daysWaiting")));
        Assert.Equal("Contoso", items[0].GetProperty("companyName").GetString());
    }

    [Fact]
    public async Task Next_up_and_the_pipeline_counts_cover_this_users_unarchived_opportunities()
    {
        var session = await factory.SignedInAsync();
        await OpportunityAsync(session, title: "Low one", priority: "low");
        await OpportunityAsync(session, title: "Normal one");
        await OpportunityAsync(session, title: "High one", stage: "preparing", priority: "high");
        await OpportunityAsync(session, title: "Sent", stage: "applied", priority: "high");
        var gone = await OpportunityAsync(session, title: "Archived", priority: "high");
        await session.DeleteAsync($"/api/v1/opportunities/{gone}");
        var closed = await OpportunityAsync(session, stage: "applied");
        await MoveAsync(session, closed, "rejected");

        var dashboard = await Json(await session.GetAsync("/api/v1/dashboard"));

        var nextUp = dashboard.GetProperty("nextUp");
        Assert.Equal(3, Count(nextUp, "total"));
        Assert.Equal(["High one", "Normal one", "Low one"], nextUp.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("title").GetString()));

        var stages = dashboard.GetProperty("stages");
        Assert.Equal(11, stages.EnumerateObject().Count());
        Assert.Equal("saved", stages.EnumerateObject().First().Name);
        Assert.Equal((2, 1, 1, 1, 0), (Count(stages, "saved"), Count(stages, "preparing"), Count(stages, "applied"), Count(stages, "rejected"), Count(stages, "offer")));

        var empty = await Json(await (await factory.SignedInAsync()).GetAsync("/api/v1/dashboard"));
        Assert.Equal(0, Count(empty.GetProperty("nextUp"), "total"));
        Assert.All(empty.GetProperty("stages").EnumerateObject(), s => Assert.Equal(0, s.Value.GetInt32()));
    }
}
