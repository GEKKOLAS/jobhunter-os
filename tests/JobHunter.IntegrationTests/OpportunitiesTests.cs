using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace JobHunter.IntegrationTests;

/// <summary>Phase 3 (spec 03, Opportunities): capture, stages, history, board, companies.</summary>
[Collection(ApiCollection.Name)]
public class OpportunitiesTests(ApiFactory factory)
{
    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<string?> ErrorCode(HttpResponseMessage response) =>
        (await Json(response)).GetProperty("errorCode").GetString();

    private static async Task<JsonElement> CreateAsync(
        TestSession session, string title, string company = "Contoso", string? stage = null, string? priority = null)
    {
        var response = await session.PostAsync("/api/v1/opportunities", new
        {
            title,
            companyName = company,
            location = "Remote, LATAM",
            workMode = "remote",
            employmentType = "full_time",
            salaryMin = 4000,
            salaryMax = 5500,
            salaryCurrency = "usd",
            salaryPeriod = "month",
            sourceUrl = "https://jobs.example.com/123",
            description = "<b>Not HTML</b> — shown as text.",
            priority,
            stage,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Json(response);
    }

    private static Task<HttpResponseMessage> MoveAsync(TestSession session, string id, string stage, string? note = null) =>
        session.PostAsync($"/api/v1/opportunities/{id}/stage", new { stage, note });

    [Fact]
    public async Task Capturing_creates_the_company_once_and_starts_the_history()
    {
        var session = await factory.SignedInAsync();

        var first = await CreateAsync(session, "Backend Engineer", company: "Contoso");
        var second = await CreateAsync(session, "Platform Engineer", company: "contoso");

        Assert.Equal(first.GetProperty("company").GetProperty("id").GetString(), second.GetProperty("company").GetProperty("id").GetString());
        Assert.Equal("saved", first.GetProperty("stage").GetString());
        Assert.Equal("USD", first.GetProperty("salaryCurrency").GetString());
        Assert.Equal("normal", first.GetProperty("priority").GetString());
        Assert.Contains("applied", first.GetProperty("allowedMoves").EnumerateArray().Select(m => m.GetString()));

        var history = (await Json(await session.GetAsync($"/api/v1/opportunities/{first.GetProperty("id").GetString()}/history")))
            .GetProperty("items");
        var start = Assert.Single(history.EnumerateArray());
        Assert.Equal(JsonValueKind.Null, start.GetProperty("fromStage").ValueKind);
        Assert.Equal("saved", start.GetProperty("toStage").GetString());
    }

    [Fact]
    public async Task Stage_moves_follow_the_rules_and_stamp_the_dates()
    {
        var session = await factory.SignedInAsync();
        var id = (await CreateAsync(session, "Backend Engineer")).GetProperty("id").GetString()!;

        var illegal = await MoveAsync(session, id, "accepted");
        Assert.Equal(HttpStatusCode.Conflict, illegal.StatusCode);
        Assert.Equal("opportunity.invalid_stage", await ErrorCode(illegal));

        var applied = await Json(await MoveAsync(session, id, "applied", "Sent CV and cover letter"));
        Assert.NotEqual(JsonValueKind.Null, applied.GetProperty("appliedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, applied.GetProperty("firstResponseAt").ValueKind);

        var screening = await Json(await MoveAsync(session, id, "screening"));
        Assert.NotEqual(JsonValueKind.Null, screening.GetProperty("firstResponseAt").ValueKind);

        var rejected = await Json(await MoveAsync(session, id, "rejected"));
        Assert.NotEqual(JsonValueKind.Null, rejected.GetProperty("closedAt").ValueKind);
        Assert.Equal(["screening"], rejected.GetProperty("allowedMoves").EnumerateArray().Select(m => m.GetString()));

        var undone = await Json(await MoveAsync(session, id, "screening", "Rejected by mistake"));
        Assert.Equal("screening", undone.GetProperty("stage").GetString());
        Assert.Equal(JsonValueKind.Null, undone.GetProperty("closedAt").ValueKind);

        var history = (await Json(await session.GetAsync($"/api/v1/opportunities/{id}/history"))).GetProperty("items")
            .EnumerateArray().Select(h => h.GetProperty("toStage").GetString()).ToList();
        Assert.Equal(["saved", "applied", "screening", "rejected", "screening"], history);
    }

    [Fact]
    public async Task Editing_keeps_the_stage_and_checks_the_version()
    {
        var session = await factory.SignedInAsync();
        var created = await CreateAsync(session, "Backend Engineer", stage: "applied");
        var id = created.GetProperty("id").GetString();
        var version = created.GetProperty("version").GetUInt32();

        var edited = await session.SendAsync(HttpMethod.Put, $"/api/v1/opportunities/{id}", new
        {
            title = "Senior Backend Engineer",
            companyName = "Fabrikam",
            priority = "high",
            stage = "offer", // ignored: stages only move through /stage
            version,
        });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var body = await Json(edited);
        Assert.Equal("Senior Backend Engineer", body.GetProperty("title").GetString());
        Assert.Equal("Fabrikam", body.GetProperty("company").GetProperty("name").GetString());
        Assert.Equal("applied", body.GetProperty("stage").GetString());
        Assert.Equal("high", body.GetProperty("priority").GetString());
        Assert.Equal("user", body.GetProperty("prioritySetBy").GetString());

        var stale = await session.SendAsync(HttpMethod.Put, $"/api/v1/opportunities/{id}",
            new { title = "Stale edit", companyName = "Fabrikam", version });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency.conflict", await ErrorCode(stale));
    }

    [Fact]
    public async Task Input_is_validated()
    {
        var session = await factory.SignedInAsync();

        var response = await session.PostAsync("/api/v1/opportunities", new
        {
            title = "",
            salaryMin = 5000,
            salaryMax = 4000,
            workMode = "moon",
            sourceUrl = "ftp://example.com",
            stage = "accepted",
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var errors = (await Json(response)).GetProperty("errors");
        foreach (var field in new[] { "title", "companyName", "salaryMax", "salaryCurrency", "workMode", "sourceUrl", "stage" })
        {
            Assert.True(errors.TryGetProperty(field, out _), $"expected an error for {field}");
        }
    }

    [Fact]
    public async Task The_board_has_a_column_per_open_stage_ordered_by_priority_then_age()
    {
        var session = await factory.SignedInAsync();
        var oldNormal = await CreateAsync(session, "Old normal");
        var low = await CreateAsync(session, "Low", priority: "low");
        var high = await CreateAsync(session, "High", priority: "high");
        await CreateAsync(session, "Applied one", stage: "applied");
        var gone = (await CreateAsync(session, "Gone", stage: "applied")).GetProperty("id").GetString()!;
        await MoveAsync(session, gone, "rejected");
        var archived = (await CreateAsync(session, "Archived")).GetProperty("id").GetString();
        await session.DeleteAsync($"/api/v1/opportunities/{archived}");

        var board = await Json(await session.GetAsync("/api/v1/board"));

        var columns = board.GetProperty("columns").EnumerateArray().ToList();
        Assert.Equal(["saved", "preparing", "applied", "screening", "interviewing", "offer"],
            columns.Select(c => c.GetProperty("stage").GetString()));
        Assert.Equal(
            [high.GetProperty("id").GetString(), oldNormal.GetProperty("id").GetString(), low.GetProperty("id").GetString()],
            columns[0].GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()));
        Assert.Single(columns[2].GetProperty("items").EnumerateArray());
        Assert.Equal(1, board.GetProperty("closedCounts").GetProperty("rejected").GetInt32());
    }

    [Fact]
    public async Task Lists_filter_by_stage_priority_text_and_archive()
    {
        var session = await factory.SignedInAsync();
        await CreateAsync(session, "Backend Engineer", company: "Northwind", priority: "high");
        await CreateAsync(session, "Data Engineer", company: "Contoso", stage: "applied");
        var archived = (await CreateAsync(session, "Archived Engineer")).GetProperty("id").GetString();
        await session.DeleteAsync($"/api/v1/opportunities/{archived}");

        async Task<List<string?>> Titles(string query) =>
            (await Json(await session.GetAsync($"/api/v1/opportunities?{query}"))).GetProperty("items").EnumerateArray()
                .Select(i => i.GetProperty("title").GetString()).ToList();

        Assert.Equal(2, (await Titles("")).Count);
        Assert.Equal(["Data Engineer"], await Titles("stage=applied"));
        Assert.Equal(["Backend Engineer"], await Titles("priority=high"));
        Assert.Equal(["Backend Engineer"], await Titles("q=northW"));
        Assert.Equal(["Archived Engineer"], await Titles("archived=true"));

        var restored = await session.PostAsync($"/api/v1/opportunities/{archived}/restore");
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.Equal(3, (await Titles("")).Count);
    }

    [Fact]
    public async Task Archived_opportunities_cant_move()
    {
        var session = await factory.SignedInAsync();
        var id = (await CreateAsync(session, "Backend Engineer")).GetProperty("id").GetString()!;
        await session.DeleteAsync($"/api/v1/opportunities/{id}");

        var move = await MoveAsync(session, id, "applied");

        Assert.Equal(HttpStatusCode.Conflict, move.StatusCode);
        Assert.Equal("opportunity.archived", await ErrorCode(move));
    }

    [Fact]
    public async Task Companies_are_unique_by_name_and_domain()
    {
        var session = await factory.SignedInAsync();
        var created = await session.PostAsync("/api/v1/companies", new { name = "Contoso", website = "https://www.contoso.com/careers" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("contoso.com", (await Json(created)).GetProperty("domain").GetString());

        var sameName = await session.PostAsync("/api/v1/companies", new { name = "CONTOSO" });
        var sameDomain = await session.PostAsync("/api/v1/companies", new { name = "Contoso Ltd", website = "contoso.com" });
        Assert.Equal("company.duplicate", await ErrorCode(sameName));
        Assert.Equal("company.duplicate", await ErrorCode(sameDomain));

        var companies = (await Json(await session.GetAsync("/api/v1/companies?q=cont"))).GetProperty("items");
        Assert.Single(companies.EnumerateArray());
    }

    [Fact]
    public async Task Opportunities_and_companies_are_private()
    {
        var owner = await factory.SignedInAsync();
        var stranger = await factory.SignedInAsync("Eve");
        var created = await CreateAsync(owner, "Backend Engineer");
        var id = created.GetProperty("id").GetString();
        var companyId = created.GetProperty("company").GetProperty("id").GetString();

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/opportunities/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await MoveAsync(stranger, id!, "applied")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/opportunities/{id}/history")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/v1/opportunities/{id}")).StatusCode);
        Assert.Empty((await Json(await stranger.GetAsync("/api/v1/board"))).GetProperty("columns")[0].GetProperty("items").EnumerateArray());

        // Someone else's company id can't be borrowed either.
        var borrowed = await stranger.PostAsync("/api/v1/opportunities", new { title = "Mine now", companyId });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, borrowed.StatusCode);
        Assert.Equal("opportunity.unknown_company", await ErrorCode(borrowed));
    }

    [Fact]
    public async Task Opportunity_changes_are_audited()
    {
        var session = await factory.SignedInAsync();
        var id = (await CreateAsync(session, "Backend Engineer")).GetProperty("id").GetString()!;
        await MoveAsync(session, id, "applied");
        await session.DeleteAsync($"/api/v1/opportunities/{id}");

        var actions = (await Json(await session.GetAsync("/api/v1/audit-logs"))).GetProperty("items").EnumerateArray()
            .Select(a => a.GetProperty("action").GetString()).ToList();

        Assert.Equal(["opportunity.archived", "opportunity.stage_changed", "opportunity.created", "company.created", "auth.register"], actions);
    }
}
