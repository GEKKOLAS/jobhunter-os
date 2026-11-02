using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JobHunter.IntegrationTests;

/// <summary>Phase 4 (spec 03, Opportunities): imports never save; refs dedupe; job boards are read-only until you pick.</summary>
[Collection(ApiCollection.Name)]
public class ImportsTests(ApiFactory factory)
{
    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<string?> ErrorCode(HttpResponseMessage response) =>
        (await Json(response)).GetProperty("errorCode").GetString();

    private static string Unique() => Guid.NewGuid().ToString("N")[..12];

    private static string JobPage(string title = "Backend Engineer", string company = "Contoso") => $$$"""
        <html><head><title>Ignored</title><script type="application/ld+json">
        {"@type":"JobPosting","title":"{{{title}}}","hiringOrganization":{"name":"{{{company}}}"},"jobLocationType":"TELECOMMUTE",
         "employmentType":"FULL_TIME","baseSalary":{"currency":"USD","value":{"minValue":4000,"maxValue":5500,"unitText":"MONTH"}},
         "description":"&lt;p&gt;Build APIs.&lt;/p&gt;"}
        </script></head><body></body></html>
        """;

    private static object Save(JsonElement preview, string? priority = null) => new
    {
        title = preview.GetProperty("title").GetString(),
        companyName = preview.GetProperty("companyName").GetString(),
        location = preview.GetProperty("location").GetString(),
        workMode = preview.GetProperty("workMode").GetString(),
        description = preview.GetProperty("description").GetString(),
        sourceUrl = preview.GetProperty("sourceUrl").GetString(),
        source = preview.GetProperty("source").GetString(),
        sourceRef = preview.GetProperty("sourceRef").GetString(),
        priority,
    };

    private static async Task<int> BoardCount(TestSession session) =>
        (await Json(await session.GetAsync("/api/v1/board"))).GetProperty("columns").EnumerateArray()
            .Sum(c => c.GetProperty("items").GetArrayLength());

    [Fact]
    public async Task A_pasted_posting_previews_without_saving()
    {
        var session = await factory.SignedInAsync();

        var response = await session.PostAsync("/api/v1/imports/text", new
        {
            text = "Backend Engineer at Contoso\nLocation: Bogotá\nRemote, full-time.\nSalary: USD 5,000 per month\n\nBuild APIs.",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await Json(response);
        Assert.Equal("Backend Engineer", preview.GetProperty("title").GetString());
        Assert.Equal("Contoso", preview.GetProperty("companyName").GetString());
        Assert.Equal("Bogotá", preview.GetProperty("location").GetString());
        Assert.Equal("remote", preview.GetProperty("workMode").GetString());
        Assert.Equal(5000, preview.GetProperty("salaryMin").GetInt32());
        Assert.Equal("paste", preview.GetProperty("source").GetString());
        Assert.Equal("pasted_text", preview.GetProperty("parsedFrom").GetString());
        Assert.Equal(JsonValueKind.Null, preview.GetProperty("sourceRef").ValueKind);
        Assert.Equal(0, await BoardCount(session));
    }

    [Fact]
    public async Task A_link_previews_from_structured_data_and_saving_twice_updates_instead_of_duplicating()
    {
        var session = await factory.SignedInAsync();
        var url = $"https://careers.example.com/{Unique()}";
        factory.Outside.On(url, JobPage());

        var preview = await Json(await session.PostAsync("/api/v1/imports/url", new { url }));
        Assert.Equal("structured_data", preview.GetProperty("parsedFrom").GetString());
        Assert.Equal(("Backend Engineer", "Contoso"), (preview.GetProperty("title").GetString(), preview.GetProperty("companyName").GetString()));
        Assert.Equal("url", preview.GetProperty("source").GetString());
        Assert.Equal(url, preview.GetProperty("sourceUrl").GetString());
        Assert.Equal(JsonValueKind.Null, preview.GetProperty("existingOpportunityId").ValueKind);
        Assert.Equal(0, await BoardCount(session)); // imports never save

        var created = await session.PostAsync("/api/v1/opportunities", Save(preview, priority: "high"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var saved = await Json(created);
        var id = saved.GetProperty("id").GetString()!;
        await session.PostAsync($"/api/v1/opportunities/{id}/stage", new { stage = "applied" });

        // The company reworded the posting. The same link now says it's already saved…
        factory.Outside.On($"{url}?utm_source=newsletter", JobPage(title: "Senior Backend Engineer"));
        var again = await Json(await session.PostAsync("/api/v1/imports/url", new { url = $"{url}?utm_source=newsletter#apply" }));
        Assert.Equal(id, again.GetProperty("existingOpportunityId").GetString());
        Assert.Equal("Senior Backend Engineer", again.GetProperty("title").GetString());

        // …and saving updates the posting's fields, keeping the stage, priority and history.
        var updated = await session.PostAsync("/api/v1/opportunities", Save(again));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var body = await Json(updated);
        Assert.Equal(id, body.GetProperty("id").GetString());
        Assert.Equal("Senior Backend Engineer", body.GetProperty("title").GetString());
        Assert.Equal("applied", body.GetProperty("stage").GetString());
        Assert.Equal("high", body.GetProperty("priority").GetString());
        Assert.Equal(1, await BoardCount(session));

        var actions = (await Json(await session.GetAsync("/api/v1/audit-logs"))).GetProperty("items").EnumerateArray()
            .Select(a => a.GetProperty("action").GetString()).ToList();
        Assert.Contains("opportunity.reimported", actions);
    }

    [Theory]
    [InlineData("https://www.linkedin.com/jobs/view/123", "LinkedIn")]
    [InlineData("https://de.indeed.com/viewjob?jk=abc", "Indeed")]
    [InlineData("https://www.glassdoor.com/job-listing/x", "Glassdoor")]
    public async Task Sites_that_forbid_automated_reading_are_never_requested(string url, string site)
    {
        var session = await factory.SignedInAsync();

        var response = await session.PostAsync("/api/v1/imports/url", new { url });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await Json(response);
        Assert.Equal("import.not_allowed", problem.GetProperty("errorCode").GetString());
        Assert.Contains(site, problem.GetProperty("title").GetString());
        Assert.Contains("paste", problem.GetProperty("title").GetString());
        Assert.False(factory.Outside.WasRequested(url));
    }

    [Fact]
    public async Task A_redirect_onto_a_forbidden_site_is_refused_too()
    {
        var session = await factory.SignedInAsync();
        var url = $"https://short.example.com/{Unique()}";
        factory.Outside.Redirect(url, "https://www.linkedin.com/jobs/view/1");
        factory.Outside.On("https://www.linkedin.com/jobs/view/1", "<html></html>");

        Assert.Equal("import.not_allowed", await ErrorCode(await session.PostAsync("/api/v1/imports/url", new { url })));
    }

    [Theory]
    [InlineData("https://169.254.169.254/latest/meta-data/")]
    [InlineData("https://localhost/admin")]
    [InlineData("https://10.0.0.1/")]
    [InlineData("https://metadata.google.internal/")]
    [InlineData("https://example.com:8443/job")]
    public async Task Private_addresses_are_refused_without_a_request(string url)
    {
        var session = await factory.SignedInAsync();

        var response = await session.PostAsync("/api/v1/imports/url", new { url });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("import.unreachable", await ErrorCode(response));
        Assert.False(factory.Outside.WasRequested(url));
    }

    [Fact]
    public async Task A_redirect_to_a_private_address_is_refused_before_following_it()
    {
        var session = await factory.SignedInAsync();
        var url = $"https://careers.example.com/{Unique()}";
        factory.Outside.Redirect(url, "https://169.254.169.254/latest/meta-data/iam");

        var response = await session.PostAsync("/api/v1/imports/url", new { url });

        Assert.Equal("import.unreachable", await ErrorCode(response));
        Assert.False(factory.Outside.WasRequested("https://169.254.169.254/latest/meta-data/iam"));
    }

    [Fact]
    public async Task Files_and_bad_links_are_rejected()
    {
        var session = await factory.SignedInAsync();
        var pdf = $"https://careers.example.com/{Unique()}.pdf";
        factory.Outside.On(pdf, "%PDF", "application/pdf");

        Assert.Equal("import.unsupported", await ErrorCode(await session.PostAsync("/api/v1/imports/url", new { url = pdf })));
        Assert.Equal("validation_failed", await ErrorCode(await session.PostAsync("/api/v1/imports/url", new { url = "ftp://example.com/a" })));
        Assert.Equal("validation_failed", await ErrorCode(await session.PostAsync("/api/v1/imports/url", new { url = "not a url" })));
        Assert.Equal("validation_failed", await ErrorCode(await session.PostAsync("/api/v1/imports/text", new { text = "" })));
    }

    private const string GreenhouseBoard = """
        {"jobs":[{"id":101,"title":"Data Scientist","company_name":"Discord","location":{"name":"San Francisco"},
          "absolute_url":"https://job-boards.greenhouse.io/BOARD/jobs/101","content":"&lt;p&gt;Run experiments.&lt;/p&gt;"},
         {"id":102,"title":"Backend Engineer","company_name":"Discord","location":{"name":"Remote"},
          "absolute_url":"https://job-boards.greenhouse.io/BOARD/jobs/102","content":"&lt;p&gt;Build APIs.&lt;/p&gt;"}],"meta":{"total":2}}
        """;

    private static string GreenhouseApi(string board) => $"https://boards-api.greenhouse.io/v1/boards/{board}/jobs?content=true";

    private (string Board, string Json) NewGreenhouseBoard()
    {
        var board = $"co-{Unique()}";
        var json = GreenhouseBoard.Replace("BOARD", board);
        factory.Outside.OnJson(GreenhouseApi(board), json);
        return (board, json);
    }

    [Fact]
    public async Task A_job_board_link_uses_the_public_api_not_the_page()
    {
        var session = await factory.SignedInAsync();
        var (board, _) = NewGreenhouseBoard();
        var url = $"https://job-boards.greenhouse.io/{board}/jobs/102";

        var preview = await Json(await session.PostAsync("/api/v1/imports/url", new { url }));

        Assert.Equal("job_board", preview.GetProperty("parsedFrom").GetString());
        Assert.Equal("greenhouse", preview.GetProperty("source").GetString());
        Assert.Equal($"{board}:102", preview.GetProperty("sourceRef").GetString());
        Assert.Equal("Backend Engineer", preview.GetProperty("title").GetString());
        Assert.Equal("Discord", preview.GetProperty("companyName").GetString());
        Assert.Equal("Build APIs.", preview.GetProperty("description").GetString());
        Assert.False(factory.Outside.WasRequested(url));
    }

    [Fact]
    public async Task A_job_board_link_the_api_doesnt_know_falls_back_to_the_page()
    {
        var session = await factory.SignedInAsync();
        var (board, _) = NewGreenhouseBoard();
        var url = $"https://job-boards.greenhouse.io/{board}/jobs/999";
        factory.Outside.On(url, JobPage(title: "Only on the page"));

        var preview = await Json(await session.PostAsync("/api/v1/imports/url", new { url }));

        Assert.Equal("Only on the page", preview.GetProperty("title").GetString());
        Assert.Equal("url", preview.GetProperty("source").GetString());
    }

    [Fact]
    public async Task A_source_is_checked_named_and_unique()
    {
        var session = await factory.SignedInAsync();
        var (board, _) = NewGreenhouseBoard();

        var missing = await session.PostAsync("/api/v1/sources", new { provider = "greenhouse", board = $"nope-{Unique()}" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
        Assert.Equal("source.not_found", await ErrorCode(missing));

        var invalid = await session.PostAsync("/api/v1/sources", new { provider = "workday", board = "../admin" });
        Assert.Equal("validation_failed", await ErrorCode(invalid));

        var created = await session.PostAsync("/api/v1/sources", new { provider = "greenhouse", board });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var source = await Json(created);
        Assert.Equal("Discord", source.GetProperty("label").GetString()); // the provider names the company
        Assert.NotEqual(JsonValueKind.Null, source.GetProperty("lastCheckedAt").ValueKind);

        Assert.Equal("source.duplicate", await ErrorCode(await session.PostAsync("/api/v1/sources", new { provider = "greenhouse", board })));

        var listed = (await Json(await session.GetAsync("/api/v1/sources"))).GetProperty("items");
        Assert.Single(listed.EnumerateArray());

        Assert.Equal(HttpStatusCode.NoContent, (await session.DeleteAsync($"/api/v1/sources/{source.GetProperty("id").GetString()}")).StatusCode);
        Assert.Empty((await Json(await session.GetAsync("/api/v1/sources"))).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Browsing_a_source_saves_nothing_and_picking_one_saves_only_that()
    {
        var session = await factory.SignedInAsync();
        var (board, json) = NewGreenhouseBoard();
        var id = (await Json(await session.PostAsync("/api/v1/sources", new { provider = "greenhouse", board }))).GetProperty("id").GetString();

        var postings = (await Json(await session.GetAsync($"/api/v1/sources/{id}/postings"))).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["101", "102"], postings.Select(p => p.GetProperty("ref").GetString()));
        Assert.All(postings, p => Assert.False(p.GetProperty("alreadySaved").GetBoolean()));
        Assert.Equal(0, await BoardCount(session));

        var saved = await session.PostAsync($"/api/v1/sources/{id}/postings/102/save");
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        var opportunity = await Json(saved);
        Assert.Equal("greenhouse", opportunity.GetProperty("source").GetString());
        Assert.Equal("Discord", opportunity.GetProperty("company").GetProperty("name").GetString());
        Assert.Equal("Build APIs.", opportunity.GetProperty("description").GetString());
        Assert.Equal(1, await BoardCount(session));

        var flagged = (await Json(await session.GetAsync($"/api/v1/sources/{id}/postings"))).GetProperty("items").EnumerateArray().ToList();
        Assert.False(flagged[0].GetProperty("alreadySaved").GetBoolean());
        Assert.True(flagged[1].GetProperty("alreadySaved").GetBoolean());

        // Moved on the board, then saved again after the posting changed: same one, updated, same stage.
        var opportunityId = opportunity.GetProperty("id").GetString();
        await session.PostAsync($"/api/v1/opportunities/{opportunityId}/stage", new { stage = "applied" });
        factory.Outside.OnJson(GreenhouseApi(board), json.Replace("Build APIs.", "Build better APIs."));
        var again = await session.PostAsync($"/api/v1/sources/{id}/postings/102/save");
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var updated = await Json(again);
        Assert.Equal(opportunityId, updated.GetProperty("id").GetString());
        Assert.Equal("Build better APIs.", updated.GetProperty("description").GetString());
        Assert.Equal("applied", updated.GetProperty("stage").GetString());

        // A link to the same posting recognizes it too: one ref, however it came in.
        var preview = await Json(await session.PostAsync("/api/v1/imports/url", new { url = $"https://boards.greenhouse.io/{board}/jobs/102" }));
        Assert.Equal(opportunityId, preview.GetProperty("existingOpportunityId").GetString());

        var gone = await session.PostAsync($"/api/v1/sources/{id}/postings/999/save");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.Equal("source.posting_gone", await ErrorCode(gone));
    }

    [Fact]
    public async Task A_failing_provider_is_a_502_and_a_removed_board_is_explained()
    {
        var session = await factory.SignedInAsync();
        var (board, _) = NewGreenhouseBoard();
        var id = (await Json(await session.PostAsync("/api/v1/sources", new { provider = "greenhouse", board }))).GetProperty("id").GetString();

        factory.Outside.On(GreenhouseApi(board), "oops", "text/plain", HttpStatusCode.InternalServerError);
        var failing = await session.GetAsync($"/api/v1/sources/{id}/postings");
        Assert.Equal(HttpStatusCode.BadGateway, failing.StatusCode);
        Assert.Equal("source.unavailable", await ErrorCode(failing));

        factory.Outside.On(GreenhouseApi(board), "{}", "application/json", HttpStatusCode.NotFound);
        var removed = await session.GetAsync($"/api/v1/sources/{id}/postings");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, removed.StatusCode);
        Assert.Equal("source.not_found", await ErrorCode(removed));
    }

    [Fact]
    public async Task Sources_are_private_and_imports_need_a_session()
    {
        var owner = await factory.SignedInAsync();
        var stranger = await factory.SignedInAsync("Eve");
        var (board, _) = NewGreenhouseBoard();
        var id = (await Json(await owner.PostAsync("/api/v1/sources", new { provider = "greenhouse", board }))).GetProperty("id").GetString();

        Assert.Empty((await Json(await stranger.GetAsync("/api/v1/sources"))).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/sources/{id}/postings")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"/api/v1/sources/{id}/postings/102/save")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/v1/sources/{id}")).StatusCode);

        var anonymous = factory.NewSession();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/v1/imports/url", new { url = "https://example.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/sources")).StatusCode);

        // The same saved ref under another user doesn't count as saved for this one.
        await owner.PostAsync($"/api/v1/sources/{id}/postings/102/save");
        var strangerSource = (await Json(await stranger.PostAsync("/api/v1/sources", new { provider = "greenhouse", board }))).GetProperty("id").GetString();
        var postings = (await Json(await stranger.GetAsync($"/api/v1/sources/{strangerSource}/postings"))).GetProperty("items").EnumerateArray();
        Assert.All(postings, p => Assert.False(p.GetProperty("alreadySaved").GetBoolean()));
    }

    [Fact]
    public async Task Link_imports_are_rate_limited()
    {
        using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:Import:PermitLimit", "2"));
        var session = new TestSession(limited.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false }));
        await session.PostAsync("/api/v1/auth/register",
            new { email = $"user-{Unique()}@example.com", password = "correct horse battery staple", displayName = "Ada" });

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add((await session.PostAsync("/api/v1/imports/url", new { url = "https://www.linkedin.com/jobs/view/1" })).StatusCode);
        }

        Assert.Equal([HttpStatusCode.UnprocessableEntity, HttpStatusCode.UnprocessableEntity, HttpStatusCode.TooManyRequests], statuses);
    }
}
