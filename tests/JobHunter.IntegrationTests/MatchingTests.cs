using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JobHunter.IntegrationTests;

/// <summary>Phase 5 (spec 03, Matching): confirmed requirements, a fit that is always current, a suggestion that never overrides you.</summary>
[Collection(ApiCollection.Name)]
public class MatchingTests(ApiFactory factory)
{
    private const string Posting = """
        About the role
        You'll keep Kubernetes clusters happy.

        Requirements
        - 5+ years of C#
        - PostgreSQL

        Nice to have
        - GraphQL
        """;

    private const string MessagesUrl = "https://api.anthropic.com/v1/messages";

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<string?> ErrorCode(HttpResponseMessage response) =>
        (await Json(response)).GetProperty("errorCode").GetString();

    private static async Task<string> CreateAsync(
        TestSession session, string title = "Backend Engineer", string? description = Posting, string? priority = "normal",
        string? location = null, string? workMode = null)
    {
        var response = await session.PostAsync("/api/v1/opportunities", new
        {
            title,
            companyName = "Contoso",
            description,
            priority,
            location,
            workMode,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("id").GetString()!;
    }

    private static async Task<string> AddSkillAsync(TestSession session, string name, int level = 4, decimal years = 6)
    {
        var response = await session.PostAsync("/api/v1/profile/skills", new { name, level, years });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("id").GetString()!;
    }

    private static Task<HttpResponseMessage> PutRequirements(TestSession session, string id, params object[] items) =>
        session.SendAsync(HttpMethod.Put, $"/api/v1/opportunities/{id}/requirements", new { items });

    private static object Must(string skill, int? yearsMin = null) => new { text = $"Needs {skill}", kind = "must", skill, yearsMin };

    private static object Nice(string skill) => new { text = $"Likes {skill}", kind = "nice", skill };

    private static async Task<JsonElement> Fit(TestSession session, string id) =>
        await Json(await session.GetAsync($"/api/v1/opportunities/{id}/fit"));

    private static async Task<JsonElement> Opportunity(TestSession session, string id) =>
        await Json(await session.GetAsync($"/api/v1/opportunities/{id}"));

    private static async Task<JsonElement> BoardCard(TestSession session, string id) =>
        (await Json(await session.GetAsync("/api/v1/board"))).GetProperty("columns").EnumerateArray()
            .SelectMany(c => c.GetProperty("items").EnumerateArray())
            .Single(card => card.GetProperty("id").GetString() == id);

    private static string[] Actions(JsonElement logs) =>
        logs.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("action").GetString()!).ToArray();

    [Fact]
    public async Task Someone_elses_opportunity_is_a_404_on_every_endpoint()
    {
        var owner = await factory.SignedInAsync();
        var id = await CreateAsync(owner);
        var other = await factory.SignedInAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/opportunities/{id}/requirements")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PutRequirements(other, id, Must("C#"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/v1/opportunities/{id}/requirements/extract", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/opportunities/{id}/fit")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_callers_get_401()
    {
        var owner = await factory.SignedInAsync();
        var id = await CreateAsync(owner);

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.NewSession().GetAsync($"/api/v1/opportunities/{id}/fit")).StatusCode);
    }

    [Fact]
    public async Task Without_a_key_the_rules_extract_unconfirmed_rows_in_the_postings_order()
    {
        var session = await factory.SignedInAsync();
        var id = await CreateAsync(session);

        Assert.Equal("rules", (await Json(await session.GetAsync($"/api/v1/opportunities/{id}/requirements"))).GetProperty("extractor").GetString());

        var response = await session.PostAsync($"/api/v1/opportunities/{id}/requirements/extract", new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var extraction = await Json(response);
        Assert.Equal("rules", extraction.GetProperty("extractor").GetString());
        Assert.False(extraction.GetProperty("fellBackToRules").GetBoolean());

        var items = extraction.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["csharp", "postgresql", "graphql"], items.Select(i => i.GetProperty("skillKey").GetString()));
        Assert.Equal(["C#", "PostgreSQL", "GraphQL"], items.Select(i => i.GetProperty("skillName").GetString()));
        Assert.Equal(["must", "must", "nice"], items.Select(i => i.GetProperty("kind").GetString()));
        Assert.All(items, i => Assert.False(i.GetProperty("confirmed").GetBoolean()));
        Assert.All(items, i => Assert.Equal("rules", i.GetProperty("origin").GetString()));
        Assert.Equal(5, items[0].GetProperty("yearsMin").GetInt32());

        var reread = await Json(await session.GetAsync($"/api/v1/opportunities/{id}/requirements"));
        Assert.Equal(3, reread.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Extracting_again_replaces_unconfirmed_rows_and_never_touches_or_repeats_confirmed_ones()
    {
        var session = await factory.SignedInAsync();
        var id = await CreateAsync(session);
        await PutRequirements(session, id, new { text = "My own words about C#", kind = "nice", skill = "csharp", yearsMin = 2 });

        await session.PostAsync($"/api/v1/opportunities/{id}/requirements/extract", new { });
        var second = await Json(await session.PostAsync($"/api/v1/opportunities/{id}/requirements/extract", new { }));

        var items = second.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["csharp", "postgresql", "graphql"], items.Select(i => i.GetProperty("skillKey").GetString()));
        Assert.Equal("My own words about C#", items[0].GetProperty("text").GetString());
        Assert.Equal("nice", items[0].GetProperty("kind").GetString());
        Assert.True(items[0].GetProperty("confirmed").GetBoolean());
        Assert.False(items[1].GetProperty("confirmed").GetBoolean());
    }

    [Fact]
    public async Task A_posting_with_no_text_cant_be_read()
    {
        var session = await factory.SignedInAsync();
        var id = await CreateAsync(session, description: null);

        var response = await session.PostAsync($"/api/v1/opportunities/{id}/requirements/extract", new { });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("requirements.no_posting", await ErrorCode(response));
    }

    [Fact]
    public async Task Saving_the_list_confirms_it_and_folds_skill_names_through_the_catalog()
    {
        var session = await factory.SignedInAsync();
        var id = await CreateAsync(session);

        var response = await PutRequirements(session, id,
            new { text = "Solid .NET Core", kind = "must", skill = ".NET Core", yearsMin = 3 },
            new { text = "A degree", kind = "nice", skill = (string?)null, yearsMin = (int?)null },
            new { text = "Salesforce admin", kind = "nice", skill = "Salesforce Admin", yearsMin = (int?)null });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await Json(response)).GetProperty("items").EnumerateArray().ToList();
        Assert.All(items, i => Assert.True(i.GetProperty("confirmed").GetBoolean()));
        Assert.All(items, i => Assert.Equal("user", i.GetProperty("origin").GetString()));
        Assert.Equal("dotnet", items[0].GetProperty("skillKey").GetString());
        Assert.Equal(".NET", items[0].GetProperty("skillName").GetString());
        Assert.Equal(JsonValueKind.Null, items[1].GetProperty("skillKey").ValueKind);
        Assert.Equal("salesforceadmin", items[2].GetProperty("skillKey").GetString());
    }

    [Fact]
    public async Task Invalid_lists_are_rejected()
    {
        var session = await factory.SignedInAsync();
        var id = await CreateAsync(session);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PutRequirements(session, id, new { text = "", kind = "must" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PutRequirements(session, id, new { text = "x", kind = "maybe" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PutRequirements(session, id, new { text = "x", kind = "must", yearsMin = 41 })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PutRequirements(session, id, new { text = new string('x', 301), kind = "must" })).StatusCode);

        // C# and c-sharp are one skill, and would count twice.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PutRequirements(session, id, Must("C#"), Nice("C Sharp"))).StatusCode);

        var tooMany = Enumerable.Range(0, 51).Select(i => (object)new { text = $"Row {i}", kind = "must" }).ToArray();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PutRequirements(session, id, tooMany)).StatusCode);
    }

    [Fact]
    public async Task No_score_until_there_is_something_honest_to_say()
    {
        var session = await factory.SignedInAsync();
        var id = await CreateAsync(session);

        var nothing = await Fit(session, id);
        Assert.Equal(JsonValueKind.Null, nothing.GetProperty("score").ValueKind);
        Assert.Equal("no_requirements", nothing.GetProperty("missingReason").GetString());

        // Extracted rows wait for confirmation: a parser's guess moves no number.
        await session.PostAsync($"/api/v1/opportunities/{id}/requirements/extract", new { });
        Assert.Equal("no_requirements", (await Fit(session, id)).GetProperty("missingReason").GetString());

        await PutRequirements(session, id, new { text = "A degree", kind = "must" });
        Assert.Equal("no_scored_requirements", (await Fit(session, id)).GetProperty("missingReason").GetString());

        await PutRequirements(session, id, Must("C#"));
        Assert.Equal("no_skills", (await Fit(session, id)).GetProperty("missingReason").GetString());
        Assert.Equal(JsonValueKind.Null, (await BoardCard(session, id)).GetProperty("fitScore").ValueKind);
    }

    [Fact]
    public async Task The_score_follows_the_algorithm_and_says_why_for_each_requirement()
    {
        var session = await factory.SignedInAsync();
        await AddSkillAsync(session, "C#", level: 4, years: 6);
        await AddSkillAsync(session, "Docker", level: 2, years: 1);
        await AddSkillAsync(session, "MySQL", level: 4, years: 3);
        var id = await CreateAsync(session);

        await PutRequirements(session, id,
            Must("C#", yearsMin: 5), // covered: 3 of 3
            Must("Docker"), // under the level: 1.5 of 3
            Must("PostgreSQL"), // only a relative, MySQL: 1.5 of 3
            Nice("Rust"), // missing: 0 of 1
            new { text = "A degree", kind = "must" }); // not scored

        var fit = await Fit(session, id);

        // 6 of 10.
        Assert.Equal(60, fit.GetProperty("score").GetInt32());
        Assert.Equal("good", fit.GetProperty("band").GetString());
        Assert.Equal(1, fit.GetProperty("algorithmVersion").GetInt32());

        var outcomes = fit.GetProperty("outcomes").EnumerateArray().ToList();
        Assert.Equal(["covered", "partial", "partial", "gap", "not_scored"], outcomes.Select(o => o.GetProperty("coverage").GetString()));
        Assert.Equal(["meets", "under_level", "related", "missing", "no_skill"], outcomes.Select(o => o.GetProperty("reason").GetString()));
        Assert.Equal("MySQL", outcomes[2].GetProperty("relatedSkill").GetString());
        Assert.Equal(2, outcomes[1].GetProperty("yourLevel").GetInt32());
        Assert.Equal("PostgreSQL", outcomes[2].GetProperty("skillName").GetString());

        var card = await BoardCard(session, id);
        Assert.Equal(60, card.GetProperty("fitScore").GetInt32());
        Assert.Equal("good", card.GetProperty("fitBand").GetString());

        var list = await Json(await session.GetAsync("/api/v1/opportunities"));
        Assert.Equal(60, list.GetProperty("items")[0].GetProperty("fitScore").GetInt32());
    }

    [Fact]
    public async Task The_fit_is_always_current_after_the_profile_changes_even_when_a_skill_is_deleted()
    {
        var session = await factory.SignedInAsync();
        var skill = await AddSkillAsync(session, "C#");
        var other = await AddSkillAsync(session, "Rust");
        var id = await CreateAsync(session);
        await PutRequirements(session, id, Must("C#"));
        Assert.Equal(100, (await Fit(session, id)).GetProperty("score").GetInt32());

        // A delete leaves no timestamp behind to compare.
        Assert.Equal(HttpStatusCode.NoContent, (await session.SendAsync(HttpMethod.Delete, $"/api/v1/profile/skills/{skill}")).StatusCode);

        var fit = await Fit(session, id);
        Assert.Equal(0, fit.GetProperty("score").GetInt32());
        Assert.Equal("weak", fit.GetProperty("band").GetString());
        Assert.Equal(0, (await BoardCard(session, id)).GetProperty("fitScore").GetInt32());

        // Deleting the last one means an empty profile again, not a low score.
        await session.SendAsync(HttpMethod.Delete, $"/api/v1/profile/skills/{other}");
        Assert.Equal("no_skills", (await Fit(session, id)).GetProperty("missingReason").GetString());
    }

    [Fact]
    public async Task Reads_that_compute_the_same_answer_dont_write()
    {
        var session = await factory.SignedInAsync();
        await AddSkillAsync(session, "C#");
        var id = await CreateAsync(session);
        await PutRequirements(session, id, Must("C#"), Nice("Rust"));

        var first = await Fit(session, id);
        var version = (await Opportunity(session, id)).GetProperty("version").GetUInt32();
        var second = await Fit(session, id);
        await session.GetAsync("/api/v1/board");

        Assert.False(string.IsNullOrEmpty(first.GetProperty("computedAt").GetString()));
        Assert.Equal(first.GetProperty("computedAt").GetString(), second.GetProperty("computedAt").GetString());
        Assert.Equal(version, (await Opportunity(session, id)).GetProperty("version").GetUInt32());

        await AddSkillAsync(session, "Rust");
        var changed = await Fit(session, id);
        Assert.Equal(100, changed.GetProperty("score").GetInt32());
        Assert.NotEqual(first.GetProperty("computedAt").GetString(), changed.GetProperty("computedAt").GetString());
    }

    [Fact]
    public async Task Reads_racing_each_other_all_succeed_and_agree()
    {
        var session = await factory.SignedInAsync();
        var skill = await AddSkillAsync(session, "C#");
        var id = await CreateAsync(session);
        await PutRequirements(session, id, Must("C#"));

        // What the page does: several reads at once, right after the result changed.
        await session.SendAsync(HttpMethod.Delete, $"/api/v1/profile/skills/{skill}");
        await AddSkillAsync(session, "Rust");
        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(i => session.GetAsync(i % 2 == 0
            ? $"/api/v1/opportunities/{id}/fit"
            : $"/api/v1/opportunities/{id}")));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var settled = await Fit(session, id);
        Assert.Equal(0, settled.GetProperty("score").GetInt32());
        Assert.Equal("low", (await Opportunity(session, id)).GetProperty("priority").GetString());
    }

    [Fact]
    public async Task Clearing_the_list_ends_the_score_and_a_suggestion_with_it()
    {
        var session = await factory.SignedInAsync();
        await AddSkillAsync(session, "C#");
        var id = await CreateAsync(session);
        await PutRequirements(session, id, Must("C#"));
        await Fit(session, id);
        Assert.Equal("high", (await Opportunity(session, id)).GetProperty("priority").GetString());

        Assert.Equal(HttpStatusCode.OK, (await PutRequirements(session, id)).StatusCode);

        var fit = await Fit(session, id);
        Assert.Equal("no_requirements", fit.GetProperty("missingReason").GetString());
        Assert.Equal(JsonValueKind.Null, fit.GetProperty("computedAt").ValueKind);
        Assert.Equal("normal", (await Opportunity(session, id)).GetProperty("priority").GetString());
        Assert.Equal(JsonValueKind.Null, (await BoardCard(session, id)).GetProperty("fitScore").ValueKind);
    }

    [Fact]
    public async Task A_strong_fit_suggests_high_priority_which_moves_it_up_the_board()
    {
        var session = await factory.SignedInAsync();
        await AddSkillAsync(session, "C#");
        var earlier = await CreateAsync(session, title: "Older, no requirements");
        var id = await CreateAsync(session, title: "Newer, strong fit");
        await PutRequirements(session, id, Must("C#"));

        var board = await Json(await session.GetAsync("/api/v1/board"));

        var saved = board.GetProperty("columns").EnumerateArray().Single(c => c.GetProperty("stage").GetString() == "saved");
        Assert.Equal([id, earlier], saved.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()));
        Assert.Equal("high", saved.GetProperty("items")[0].GetProperty("priority").GetString());

        var opportunity = await Opportunity(session, id);
        Assert.Equal("high", opportunity.GetProperty("priority").GetString());
        Assert.Equal("suggested", opportunity.GetProperty("prioritySetBy").GetString());
        Assert.Contains("opportunity.priority_suggested", Actions(await Json(await session.GetAsync("/api/v1/audit-logs"))));
    }

    [Fact]
    public async Task A_weak_fit_suggests_low_and_moving_on_from_preparing_ends_a_high_suggestion()
    {
        var session = await factory.SignedInAsync();
        await AddSkillAsync(session, "C#");

        var weak = await CreateAsync(session);
        await PutRequirements(session, weak, Must("Rust"));
        Assert.Equal("low", (await Fit(session, weak)).GetProperty("suggestedPriority").GetString());
        Assert.Equal("low", (await Opportunity(session, weak)).GetProperty("priority").GetString());

        var strong = await CreateAsync(session);
        await PutRequirements(session, strong, Must("C#"));
        Assert.Equal("high", (await Fit(session, strong)).GetProperty("suggestedPriority").GetString());

        await session.PostAsync($"/api/v1/opportunities/{strong}/stage", new { stage = "applied" });

        Assert.Equal("normal", (await Fit(session, strong)).GetProperty("suggestedPriority").GetString());
        Assert.Equal("normal", (await Opportunity(session, strong)).GetProperty("priority").GetString());
    }

    [Fact]
    public async Task A_priority_you_chose_is_never_overridden_though_the_suggestion_is_still_shown()
    {
        var session = await factory.SignedInAsync();
        await AddSkillAsync(session, "C#");

        var chosen = await CreateAsync(session, priority: "low");
        await PutRequirements(session, chosen, Must("C#"));
        var fit = await Fit(session, chosen);
        Assert.Equal("high", fit.GetProperty("suggestedPriority").GetString());
        var opportunity = await Opportunity(session, chosen);
        Assert.Equal(("low", "user"), (opportunity.GetProperty("priority").GetString(), opportunity.GetProperty("prioritySetBy").GetString()));

        // Changing a suggested one by hand makes it yours, and it stays.
        var suggested = await CreateAsync(session);
        await PutRequirements(session, suggested, Must("C#"));
        await Fit(session, suggested);
        var version = (await Opportunity(session, suggested)).GetProperty("version").GetUInt32();
        var edit = await session.SendAsync(HttpMethod.Put, $"/api/v1/opportunities/{suggested}", new
        {
            title = "Backend Engineer",
            companyName = "Contoso",
            description = Posting,
            priority = "normal",
            version,
        });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        await Fit(session, suggested);
        var edited = await Opportunity(session, suggested);
        Assert.Equal(("normal", "user"), (edited.GetProperty("priority").GetString(), edited.GetProperty("prioritySetBy").GetString()));
    }

    [Fact]
    public async Task An_edit_that_leaves_a_suggested_priority_as_it_is_doesnt_claim_it()
    {
        var session = await factory.SignedInAsync();
        await AddSkillAsync(session, "C#");
        var id = await CreateAsync(session);
        await PutRequirements(session, id, Must("C#"));
        await Fit(session, id);
        var before = await Opportunity(session, id);

        await session.SendAsync(HttpMethod.Put, $"/api/v1/opportunities/{id}", new
        {
            title = "Renamed",
            companyName = "Contoso",
            description = Posting,
            priority = before.GetProperty("priority").GetString(),
            version = before.GetProperty("version").GetUInt32(),
        });

        Assert.Equal("suggested", (await Opportunity(session, id)).GetProperty("prioritySetBy").GetString());
    }

    [Fact]
    public async Task Archived_opportunities_get_a_fit_but_no_suggestion_applied()
    {
        var session = await factory.SignedInAsync();
        await AddSkillAsync(session, "C#");
        var id = await CreateAsync(session);
        await session.SendAsync(HttpMethod.Delete, $"/api/v1/opportunities/{id}");
        await PutRequirements(session, id, Must("C#"));

        Assert.Equal(100, (await Fit(session, id)).GetProperty("score").GetInt32());
        Assert.Equal("normal", (await Opportunity(session, id)).GetProperty("priority").GetString());
    }

    [Fact]
    public async Task Mismatches_are_notes_and_never_change_the_score()
    {
        var session = await factory.SignedInAsync();
        await AddSkillAsync(session, "C#");
        var version = (await Json(await session.GetAsync("/api/v1/profile"))).GetProperty("version").GetUInt32();
        await session.SendAsync(HttpMethod.Put, "/api/v1/profile", new
        {
            headline = "Engineer",
            location = "Bogotá, Colombia",
            seniority = "mid",
            workModes = new[] { "remote" },
            targetRoles = Array.Empty<string>(),
            languages = Array.Empty<object>(),
            version,
        });
        var id = await CreateAsync(session, title: "Senior Backend Engineer", location: "Berlin", workMode: "onsite");
        await PutRequirements(session, id, Must("C#"));

        var fit = await Fit(session, id);

        Assert.Equal(100, fit.GetProperty("score").GetInt32());
        var notes = fit.GetProperty("notes").EnumerateArray().ToList();
        Assert.Equal(["work_mode", "location", "seniority"], notes.Select(n => n.GetProperty("code").GetString()));
        Assert.Equal("senior", notes[2].GetProperty("posting").GetString());
        Assert.Equal("mid", notes[2].GetProperty("profile").GetString());
    }

    [Fact]
    public async Task Requirement_changes_are_audited_and_the_list_belongs_to_the_opportunity()
    {
        var session = await factory.SignedInAsync();
        var id = await CreateAsync(session);

        await session.PostAsync($"/api/v1/opportunities/{id}/requirements/extract", new { });
        await PutRequirements(session, id, Must("C#"));

        var actions = Actions(await Json(await session.GetAsync("/api/v1/audit-logs")));
        Assert.Contains("requirements.extracted", actions);
        Assert.Contains("requirements.updated", actions);
    }

    private TestSession WithAiKey(Action<IWebHostBuilder>? configure = null) => new(factory
        .WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Ai:ApiKey", "test-key");
            configure?.Invoke(builder);
        })
        .CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false }));

    private static async Task RegisterAsync(TestSession session)
    {
        var response = await session.PostAsync("/api/v1/auth/register", new
        {
            email = $"ai-{Guid.NewGuid():N}@example.com",
            password = "correct horse battery staple",
            displayName = "Ada",
        });
        response.EnsureSuccessStatusCode();
    }

    private static string ClaudeAnswer(string json, string stopReason = "end_turn") => JsonSerializer.Serialize(new
    {
        id = "msg_1",
        type = "message",
        role = "assistant",
        model = "claude-opus-5-5",
        content = new object[] { new { type = "text", text = json } },
        stop_reason = stopReason,
        stop_sequence = (string?)null,
        usage = new { input_tokens = 10, output_tokens = 20 },
    });

    // These share one stub route, so they run as one test rather than racing each other.
    [Fact]
    public async Task With_a_key_Claude_reads_the_posting_and_any_failure_falls_back_to_the_rules()
    {
        var session = WithAiKey();
        await RegisterAsync(session);
        var id = await CreateAsync(session);
        Assert.Equal("ai", (await Json(await session.GetAsync($"/api/v1/opportunities/{id}/requirements"))).GetProperty("extractor").GetString());

        factory.Outside.OnJson(MessagesUrl, ClaudeAnswer("""
            {"requirements":[
              {"text":"5+ years of C#","kind":"must","skill":"C#","years_min":5},
              {"text":"Invented","kind":"must","skill":"Fortran","years_min":null}]}
            """));
        var read = await Json(await session.PostAsync($"/api/v1/opportunities/{id}/requirements/extract", new { }));
        Assert.True(factory.Outside.WasRequested(MessagesUrl), string.Join(", ", factory.Outside.Requested));
        Assert.Equal("ai", read.GetProperty("extractor").GetString());
        Assert.False(read.GetProperty("fellBackToRules").GetBoolean());
        var row = Assert.Single(read.GetProperty("items").EnumerateArray());
        Assert.Equal(("csharp", "ai", false), (row.GetProperty("skillKey").GetString(), row.GetProperty("origin").GetString(), row.GetProperty("confirmed").GetBoolean()));

        // A refusal, invalid output and an API error all end the same way: the rules, and the response says so.
        foreach (var (body, status) in new[]
        {
            (ClaudeAnswer("""{"requirements":[]}""", "refusal"), HttpStatusCode.OK),
            (ClaudeAnswer("not json"), HttpStatusCode.OK),
            ("""{"type":"error","error":{"type":"invalid_request_error","message":"nope"}}""", HttpStatusCode.BadRequest),
        })
        {
            factory.Outside.OnJson(MessagesUrl, body, status);

            var response = await session.PostAsync($"/api/v1/opportunities/{id}/requirements/extract", new { });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var fallback = await Json(response);
            Assert.True(fallback.GetProperty("fellBackToRules").GetBoolean());
            Assert.Equal("rules", fallback.GetProperty("extractor").GetString());
            Assert.Equal(["csharp", "postgresql", "graphql"], fallback.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("skillKey").GetString()));
        }
    }

    [Fact]
    public async Task The_ai_rate_limit_applies_only_when_a_key_is_configured()
    {
        var limited = WithAiKey(builder => builder.UseSetting("RateLimiting:Ai:PermitLimit", "1"));
        await RegisterAsync(limited);
        var id = await CreateAsync(limited, description: "");

        // Both reach the limiter before the handler: the first gets as far as "no posting".
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await limited.PostAsync($"/api/v1/opportunities/{id}/requirements/extract", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await limited.PostAsync($"/api/v1/opportunities/{id}/requirements/extract", new { })).StatusCode);

        var unlimited = new TestSession(factory
            .WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:Ai:PermitLimit", "1"))
            .CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false }));
        await RegisterAsync(unlimited);
        var own = await CreateAsync(unlimited, description: "");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await unlimited.PostAsync($"/api/v1/opportunities/{own}/requirements/extract", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await unlimited.PostAsync($"/api/v1/opportunities/{own}/requirements/extract", new { })).StatusCode);
    }
}
