using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace JobHunter.IntegrationTests;

/// <summary>Phase 2 (spec 03, Profile): profile, skills, evidence and the skill catalog.</summary>
[Collection(ApiCollection.Name)]
public class ProfileTests(ApiFactory factory)
{
    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<string?> ErrorCode(HttpResponseMessage response) =>
        (await Json(response)).GetProperty("errorCode").GetString();

    private static async Task<JsonElement> AddSkillAsync(TestSession session, string name, string? category = null, int level = 4, decimal years = 3)
    {
        var response = await session.PostAsync("/api/v1/profile/skills", new { name, category, level, years, lastUsedYear = 2026 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Json(response);
    }

    private static object Evidence(string title, params string[] skillIds) => new
    {
        kind = "project",
        title,
        organization = "Personal",
        startDate = "2026-07-04",
        endDate = (string?)null,
        description = "Email templates and sending from your own mailbox.",
        outcome = "Used it to send my own applications.",
        links = new[] { new { label = "Repository", url = "https://github.com/example/smtp" } },
        skillIds,
    };

    [Fact]
    public async Task The_profile_starts_empty_and_saves_from_version_zero()
    {
        var session = await factory.SignedInAsync();

        var empty = await Json(await session.GetAsync("/api/v1/profile"));
        Assert.Equal(0u, empty.GetProperty("version").GetUInt32());
        Assert.Equal("", empty.GetProperty("headline").GetString());

        var saved = await session.SendAsync(HttpMethod.Put, "/api/v1/profile", new
        {
            headline = "Backend engineer (.NET)",
            summary = "I build APIs.",
            location = "Bogotá",
            targetRoles = new[] { "Backend Engineer", "backend engineer", " Platform Engineer " },
            seniority = "senior",
            workModes = new[] { "onsite", "remote" },
            languages = new[] { new { name = "Spanish", level = "native" }, new { name = "English", level = "professional" } },
            version = 0,
        });

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var profile = await Json(saved);
        Assert.Equal(["Backend Engineer", "Platform Engineer"], profile.GetProperty("targetRoles").EnumerateArray().Select(r => r.GetString()));
        Assert.Equal(["remote", "onsite"], profile.GetProperty("workModes").EnumerateArray().Select(r => r.GetString()));
        Assert.Equal("professional", profile.GetProperty("languages")[1].GetProperty("level").GetString());
        Assert.NotEqual(0u, profile.GetProperty("version").GetUInt32());

        var reread = await Json(await session.GetAsync("/api/v1/profile"));
        Assert.Equal("Backend engineer (.NET)", reread.GetProperty("headline").GetString());
    }

    [Fact]
    public async Task A_stale_version_is_a_conflict_not_an_overwrite()
    {
        var session = await factory.SignedInAsync();
        var first = await Json(await session.SendAsync(HttpMethod.Put, "/api/v1/profile", new { headline = "First", version = 0 }));
        var version = first.GetProperty("version").GetUInt32();

        var second = await session.SendAsync(HttpMethod.Put, "/api/v1/profile", new { headline = "Second", version });
        var stale = await session.SendAsync(HttpMethod.Put, "/api/v1/profile", new { headline = "Stale", version });
        var createAgain = await session.SendAsync(HttpMethod.Put, "/api/v1/profile", new { headline = "Again", version = 0 });

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency.conflict", await ErrorCode(stale));
        Assert.Equal(HttpStatusCode.Conflict, createAgain.StatusCode);
        Assert.Equal("Second", (await Json(await session.GetAsync("/api/v1/profile"))).GetProperty("headline").GetString());
    }

    [Fact]
    public async Task Profile_values_are_validated()
    {
        var session = await factory.SignedInAsync();

        var response = await session.SendAsync(HttpMethod.Put, "/api/v1/profile", new
        {
            seniority = "wizard",
            workModes = new[] { "moon" },
            languages = new[] { new { name = "Klingon", level = "fluent-ish" } },
            version = 0,
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var errors = (await Json(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("seniority", out _));
    }

    [Fact]
    public async Task Catalog_skills_get_their_key_and_category_and_aliases_count_as_duplicates()
    {
        var session = await factory.SignedInAsync();

        var csharp = await AddSkillAsync(session, "C#");
        Assert.Equal("csharp", csharp.GetProperty("key").GetString());
        Assert.Equal("language", csharp.GetProperty("category").GetString());
        Assert.Equal(0, csharp.GetProperty("evidenceCount").GetInt32());

        var duplicate = await session.PostAsync("/api/v1/profile/skills", new { name = "c sharp", level = 3, years = 1 });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("skill.duplicate", await ErrorCode(duplicate));

        var unknownWithoutCategory = await session.PostAsync("/api/v1/profile/skills", new { name = "Basket weaving", level = 2, years = 1 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknownWithoutCategory.StatusCode);
        Assert.True((await Json(unknownWithoutCategory)).GetProperty("errors").TryGetProperty("category", out _));

        var unknown = await AddSkillAsync(session, "Basket weaving", category: "domain");
        Assert.Equal("basketweaving", unknown.GetProperty("key").GetString());
    }

    [Fact]
    public async Task Skill_values_are_validated()
    {
        var session = await factory.SignedInAsync();

        var response = await session.PostAsync("/api/v1/profile/skills", new { name = "C#", level = 9, years = 2.25, lastUsedYear = 2100 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var errors = (await Json(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("level", out _));
        Assert.True(errors.TryGetProperty("years", out _));
        Assert.True(errors.TryGetProperty("lastUsedYear", out _));
    }

    [Fact]
    public async Task Renaming_a_skill_rekeys_it_and_checks_duplicates_and_versions()
    {
        var session = await factory.SignedInAsync();
        var postgres = await AddSkillAsync(session, "Postgres");
        await AddSkillAsync(session, "Docker");
        var id = postgres.GetProperty("id").GetString();
        var version = postgres.GetProperty("version").GetUInt32();

        var renamed = await session.SendAsync(HttpMethod.Put, $"/api/v1/profile/skills/{id}",
            new { name = "PostgreSQL", category = "data", level = 5, years = 6, version });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var renamedSkill = await Json(renamed);
        Assert.Equal("postgresql", renamedSkill.GetProperty("key").GetString());

        var clash = await session.SendAsync(HttpMethod.Put, $"/api/v1/profile/skills/{id}",
            new { name = "docker", category = "cloud", level = 5, years = 6, version = renamedSkill.GetProperty("version").GetUInt32() });
        Assert.Equal("skill.duplicate", await ErrorCode(clash));

        var stale = await session.SendAsync(HttpMethod.Put, $"/api/v1/profile/skills/{id}",
            new { name = "PostgreSQL", category = "data", level = 4, years = 6, version });
        Assert.Equal("concurrency.conflict", await ErrorCode(stale));
    }

    [Fact]
    public async Task Evidence_cites_skills_and_deleting_a_skill_unlinks_it()
    {
        var session = await factory.SignedInAsync();
        var csharp = (await AddSkillAsync(session, "C#")).GetProperty("id").GetString()!;
        var react = (await AddSkillAsync(session, "React")).GetProperty("id").GetString()!;

        var created = await session.PostAsync("/api/v1/profile/evidence", Evidence("Mail Template Hub", csharp, react));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = await Json(created);
        Assert.Equal("2026-07-04", item.GetProperty("startDate").GetString());
        Assert.Equal("https://github.com/example/smtp", item.GetProperty("links")[0].GetProperty("url").GetString());

        var skills = (await Json(await session.GetAsync("/api/v1/profile/skills"))).GetProperty("items").EnumerateArray().ToList();
        Assert.All(skills, s => Assert.Equal(1, s.GetProperty("evidenceCount").GetInt32()));

        Assert.Equal(HttpStatusCode.NoContent, (await session.DeleteAsync($"/api/v1/profile/skills/{react}")).StatusCode);

        var evidence = (await Json(await session.GetAsync("/api/v1/profile/evidence"))).GetProperty("items")[0];
        Assert.Equal([csharp], evidence.GetProperty("skillIds").EnumerateArray().Select(s => s.GetString()));
    }

    [Fact]
    public async Task Evidence_rejects_other_peoples_skills_insecure_links_and_backwards_dates()
    {
        var session = await factory.SignedInAsync();
        var stranger = await factory.SignedInAsync("Eve");
        var theirs = (await AddSkillAsync(stranger, "Rust")).GetProperty("id").GetString()!;

        var foreignSkill = await session.PostAsync("/api/v1/profile/evidence", Evidence("Borrowed", theirs));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, foreignSkill.StatusCode);
        Assert.Equal("evidence.unknown_skill", await ErrorCode(foreignSkill));

        var invalid = await session.PostAsync("/api/v1/profile/evidence", new
        {
            kind = "project",
            title = "Bad dates",
            startDate = "2026-07-04",
            endDate = "2025-01-01",
            links = new[] { new { label = "Site", url = "http://insecure.example" } },
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        var errors = (await Json(invalid)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("endDate", out _));
        Assert.Contains(errors.EnumerateObject(), e => e.Name.StartsWith("links", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Profiles_are_private()
    {
        var owner = await factory.SignedInAsync();
        var stranger = await factory.SignedInAsync("Eve");
        var skill = await AddSkillAsync(owner, "Kubernetes");
        var item = await Json(await owner.PostAsync("/api/v1/profile/evidence", Evidence("Cluster")));
        await owner.SendAsync(HttpMethod.Put, "/api/v1/profile", new { headline = "Mine", version = 0 });

        Assert.Empty((await Json(await stranger.GetAsync("/api/v1/profile/skills"))).GetProperty("items").EnumerateArray());
        Assert.Empty((await Json(await stranger.GetAsync("/api/v1/profile/evidence"))).GetProperty("items").EnumerateArray());
        Assert.Equal("", (await Json(await stranger.GetAsync("/api/v1/profile"))).GetProperty("headline").GetString());

        var skillId = skill.GetProperty("id").GetString();
        var itemId = item.GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/v1/profile/skills/{skillId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/v1/profile/evidence/{itemId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.SendAsync(HttpMethod.Put, $"/api/v1/profile/evidence/{itemId}",
            new { kind = "project", title = "Hijack", version = item.GetProperty("version").GetUInt32() })).StatusCode);
    }

    [Fact]
    public async Task Profile_changes_are_audited()
    {
        var session = await factory.SignedInAsync();
        var skill = await AddSkillAsync(session, "Docker");
        await session.DeleteAsync($"/api/v1/profile/skills/{skill.GetProperty("id").GetString()}");

        var actions = (await Json(await session.GetAsync("/api/v1/audit-logs"))).GetProperty("items").EnumerateArray()
            .Select(a => a.GetProperty("action").GetString()).ToList();

        Assert.Equal(["skill.deleted", "skill.created", "auth.register"], actions);
    }

    [Fact]
    public async Task The_catalog_autocompletes_by_name_and_alias()
    {
        var session = await factory.SignedInAsync();

        var items = (await Json(await session.GetAsync("/api/v1/skills/catalog?q=postg"))).GetProperty("items");
        var aliased = (await Json(await session.GetAsync("/api/v1/skills/catalog?q=k8s&limit=1"))).GetProperty("items");

        Assert.Equal("postgresql", items[0].GetProperty("key").GetString());
        Assert.Equal("PostgreSQL", items[0].GetProperty("name").GetString());
        Assert.Equal("relational-db", items[0].GetProperty("family").GetString());
        Assert.Equal("kubernetes", Assert.Single(aliased.EnumerateArray()).GetProperty("key").GetString());
    }
}
