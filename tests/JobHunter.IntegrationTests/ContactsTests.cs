using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace JobHunter.IntegrationTests;

/// <summary>Phase 6 (spec 03, Contacts): the people in a search, what links them to it, and what was said.</summary>
[Collection(ApiCollection.Name)]
public class ContactsTests(ApiFactory factory)
{
    private static readonly DateTimeOffset Yesterday = DateTimeOffset.UtcNow.AddDays(-1);

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<string?> ErrorCode(HttpResponseMessage response) =>
        (await Json(response)).GetProperty("errorCode").GetString();

    private static string Unique() => Guid.NewGuid().ToString("N")[..10];

    private static async Task<JsonElement> CreateAsync(TestSession session, string name = "Ada Recruiter", string? companyName = null, string? email = null, string? role = null)
    {
        var response = await session.PostAsync("/api/v1/contacts", new { name, companyName, email, role });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Json(response);
    }

    private static async Task<string> OpportunityAsync(TestSession session, string title = "Backend Engineer")
    {
        var response = await session.PostAsync("/api/v1/opportunities", new { title, companyName = "Contoso" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("id").GetString()!;
    }

    private static Task<HttpResponseMessage> LogAsync(
        TestSession session, string contactId, string summary = "Talked about the role", string channel = "email",
        string direction = "inbound", DateTimeOffset? occurredAt = null, string? opportunityId = null) =>
        session.PostAsync($"/api/v1/contacts/{contactId}/interactions", new { channel, direction, occurredAt = occurredAt ?? Yesterday, summary, opportunityId });

    private static string Id(JsonElement element) => element.GetProperty("id").GetString()!;

    private static object Valid(string name = "Ada", uint version = 0) => new { name, version };

    [Fact]
    public async Task A_contact_is_created_read_and_edited_with_a_version()
    {
        var session = await factory.SignedInAsync();
        var created = await Json(await session.PostAsync("/api/v1/contacts", new
        {
            name = "  Ada Lovelace ",
            companyName = "Contoso",
            role = "Technical Recruiter",
            email = " ada@example.com ",
            linkedInUrl = "linkedin.com/in/ada",
            phone = "+57 300 123 4567",
            notes = "Met at a meetup.",
        }));

        Assert.Equal("Ada Lovelace", created.GetProperty("name").GetString());
        Assert.Equal("Contoso", created.GetProperty("company").GetProperty("name").GetString());
        Assert.Equal("ada@example.com", created.GetProperty("email").GetString());
        Assert.Equal("https://linkedin.com/in/ada", created.GetProperty("linkedInUrl").GetString());
        Assert.Equal(0, created.GetProperty("interactionCount").GetInt32());

        var id = Id(created);
        var read = await Json(await session.GetAsync($"/api/v1/contacts/{id}"));
        var version = read.GetProperty("version").GetUInt32();
        Assert.Equal("Technical Recruiter", read.GetProperty("role").GetString());
        Assert.Empty(read.GetProperty("opportunities").EnumerateArray());

        var edited = await session.SendAsync(HttpMethod.Put, $"/api/v1/contacts/{id}", new { name = "Ada L.", role = "Head of Talent", version });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var body = await Json(edited);
        Assert.Equal("Head of Talent", body.GetProperty("role").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("company").ValueKind); // no company sent: it no longer has one
        Assert.Equal(JsonValueKind.Null, body.GetProperty("email").ValueKind);

        var stale = await session.SendAsync(HttpMethod.Put, $"/api/v1/contacts/{id}", new { name = "Stale", version });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency.conflict", await ErrorCode(stale));
    }

    [Fact]
    public async Task A_company_is_matched_by_name_or_created_and_shared_with_opportunities()
    {
        var session = await factory.SignedInAsync();
        await OpportunityAsync(session); // creates Contoso

        var contact = await CreateAsync(session, companyName: "CONTOSO");

        var companies = await Json(await session.GetAsync("/api/v1/companies"));
        var company = Assert.Single(companies.GetProperty("items").EnumerateArray());
        Assert.Equal("Contoso", contact.GetProperty("company").GetProperty("name").GetString());
        Assert.Equal(company.GetProperty("id").GetString(), contact.GetProperty("company").GetProperty("id").GetString());

        // Someone else's company id is refused, not borrowed.
        var other = await factory.SignedInAsync();
        var refused = await other.PostAsync("/api/v1/contacts", new { name = "Bob", companyId = company.GetProperty("id").GetString() });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
    }

    [Theory]
    [InlineData("name", "")]
    [InlineData("email", "not-an-email")]
    [InlineData("email", "two@@example.com")]
    [InlineData("email", "a b@example.com")]
    [InlineData("email", "ada@localhost")]
    [InlineData("linkedInUrl", "https://evil.example/in/ada")]
    [InlineData("linkedInUrl", "http://linkedin.com/in/ada")]
    [InlineData("linkedInUrl", "javascript:alert(1)")]
    [InlineData("phone", "call me")]
    [InlineData("phone", "12")]
    public async Task Invalid_fields_are_rejected_on_the_field(string field, string value)
    {
        var session = await factory.SignedInAsync();
        var payload = new Dictionary<string, object?> { ["name"] = "Ada", [field] = value };

        var response = await session.PostAsync("/api/v1/contacts", payload);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.True((await Json(response)).GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Limits_are_enforced()
    {
        var session = await factory.SignedInAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await session.PostAsync("/api/v1/contacts", new { name = new string('x', 121) })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await session.PostAsync("/api/v1/contacts", new { name = "A", role = new string('x', 121) })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await session.PostAsync("/api/v1/contacts", new { name = "A", notes = new string('x', 2001) })).StatusCode);
    }

    [Fact]
    public async Task An_email_is_one_person_ignoring_case_but_only_within_a_user()
    {
        var session = await factory.SignedInAsync();
        var email = $"{Unique()}@example.com";
        var first = await CreateAsync(session, "Ada", email: email);

        var duplicate = await session.PostAsync("/api/v1/contacts", new { name = "Also Ada", email = email.ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var clash = await Json(duplicate);
        Assert.Equal("contact.duplicate", clash.GetProperty("errorCode").GetString());
        Assert.Contains("Ada", clash.GetProperty("title").GetString());

        // Editing yourself isn't a clash.
        var version = first.GetProperty("version").GetUInt32();
        Assert.Equal(HttpStatusCode.OK, (await session.SendAsync(HttpMethod.Put, $"/api/v1/contacts/{Id(first)}", new { name = "Ada L.", email, version })).StatusCode);

        // An archived one still holds it, and says how to get it back.
        await session.SendAsync(HttpMethod.Delete, $"/api/v1/contacts/{Id(first)}");
        var archived = await Json(await session.PostAsync("/api/v1/contacts", new { name = "Ada again", email }));
        Assert.Equal("contact.duplicate", archived.GetProperty("errorCode").GetString());
        Assert.Contains("Restore", archived.GetProperty("title").GetString());

        // Someone else can know the same person.
        var other = await factory.SignedInAsync();
        await CreateAsync(other, "Their Ada", email: email);

        // Many contacts can have no email.
        await CreateAsync(session, "No email one");
        await CreateAsync(session, "No email two");
    }

    [Fact]
    public async Task The_list_searches_filters_and_orders_by_name()
    {
        var session = await factory.SignedInAsync();
        var zoe = await CreateAsync(session, "Zoe", companyName: "Fabrikam", email: $"zoe-{Unique()}@fabrikam.example");
        await CreateAsync(session, "Ada", companyName: "Contoso", role: "Hiring Manager");
        await CreateAsync(session, "Bob");

        static string[] Names(JsonElement list) => list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()!).ToArray();
        async Task<JsonElement> List(string query = "") => await Json(await session.GetAsync($"/api/v1/contacts{query}"));

        var all = await List();
        Assert.Equal(["Ada", "Bob", "Zoe"], Names(all));
        Assert.Equal(3, all.GetProperty("totalCount").GetInt32());

        Assert.Equal(["Bob"], Names(await List("?q=bo")));
        Assert.Equal(["Ada"], Names(await List("?q=hiring")));
        Assert.Equal(["Zoe"], Names(await List("?q=FABRIKAM.example")));
        Assert.Equal(["Ada"], Names(await List("?q=contoso")));
        Assert.Empty(Names(await List("?q=nobody")));

        var companyId = zoe.GetProperty("company").GetProperty("id").GetString();
        Assert.Equal(["Zoe"], Names(await List($"?companyId={companyId}")));

        var paged = await List("?page=2&pageSize=2");
        Assert.Equal(["Zoe"], Names(paged));
    }

    [Fact]
    public async Task Archiving_hides_a_contact_blocks_new_activity_and_restoring_brings_it_all_back()
    {
        var session = await factory.SignedInAsync();
        var contact = await CreateAsync(session, "Ada");
        var id = Id(contact);
        var opportunity = await OpportunityAsync(session);
        await session.PostAsync($"/api/v1/contacts/{id}/opportunities/{opportunity}", new { relation = "recruiter" });
        await LogAsync(session, id, opportunityId: opportunity);

        Assert.Equal(HttpStatusCode.NoContent, (await session.SendAsync(HttpMethod.Delete, $"/api/v1/contacts/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await session.SendAsync(HttpMethod.Delete, $"/api/v1/contacts/{id}")).StatusCode); // again: no harm

        Assert.Empty((await Json(await session.GetAsync("/api/v1/contacts"))).GetProperty("items").EnumerateArray());
        Assert.Single((await Json(await session.GetAsync("/api/v1/contacts?archived=true"))).GetProperty("items").EnumerateArray());

        var log = await LogAsync(session, id);
        Assert.Equal(HttpStatusCode.Conflict, log.StatusCode);
        Assert.Equal("contact.archived", await ErrorCode(log));
        var link = await session.PostAsync($"/api/v1/contacts/{id}/opportunities/{await OpportunityAsync(session, "Another")}", new { relation = "other" });
        Assert.Equal("contact.archived", await ErrorCode(link));

        // The opportunity's page no longer lists them as someone to talk to, but what was said stays.
        var people = await Json(await session.GetAsync($"/api/v1/opportunities/{opportunity}/people"));
        Assert.Empty(people.GetProperty("contacts").EnumerateArray());
        Assert.Single(people.GetProperty("interactions").EnumerateArray());

        var restored = await Json(await session.PostAsync($"/api/v1/contacts/{id}/restore", new { }));
        Assert.Equal(JsonValueKind.Null, restored.GetProperty("archivedAt").ValueKind);
        Assert.Equal(1, restored.GetProperty("interactionCount").GetInt32());
        Assert.Single(restored.GetProperty("opportunities").EnumerateArray());
        Assert.Equal(HttpStatusCode.Created, (await LogAsync(session, id)).StatusCode);
    }

    [Fact]
    public async Task Links_connect_a_contact_to_an_opportunity_and_linking_again_changes_the_relation()
    {
        var session = await factory.SignedInAsync();
        var id = Id(await CreateAsync(session, "Ada", role: "Recruiter"));
        var opportunity = await OpportunityAsync(session);

        var linked = await session.PostAsync($"/api/v1/contacts/{id}/opportunities/{opportunity}", new { relation = "recruiter" });
        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        Assert.Equal("Backend Engineer", (await Json(linked)).GetProperty("title").GetString());

        var again = await Json(await session.PostAsync($"/api/v1/contacts/{id}/opportunities/{opportunity}", new { relation = "hiring_manager" }));
        Assert.Equal("hiring_manager", again.GetProperty("relation").GetString());

        var contact = await Json(await session.GetAsync($"/api/v1/contacts/{id}"));
        var link = Assert.Single(contact.GetProperty("opportunities").EnumerateArray());
        Assert.Equal(("Backend Engineer", "Contoso", "saved", "hiring_manager"), (
            link.GetProperty("title").GetString(), link.GetProperty("companyName").GetString(),
            link.GetProperty("stage").GetString(), link.GetProperty("relation").GetString()));
        Assert.Equal(1, contact.GetProperty("opportunityCount").GetInt32());

        var people = await Json(await session.GetAsync($"/api/v1/opportunities/{opportunity}/people"));
        var person = Assert.Single(people.GetProperty("contacts").EnumerateArray());
        Assert.Equal(("Ada", "Recruiter", "hiring_manager"), (person.GetProperty("name").GetString(), person.GetProperty("role").GetString(), person.GetProperty("relation").GetString()));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await session.PostAsync($"/api/v1/contacts/{id}/opportunities/{opportunity}", new { relation = "friend" })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await session.SendAsync(HttpMethod.Delete, $"/api/v1/contacts/{id}/opportunities/{opportunity}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await session.SendAsync(HttpMethod.Delete, $"/api/v1/contacts/{id}/opportunities/{opportunity}")).StatusCode); // already gone
        Assert.Empty((await Json(await session.GetAsync($"/api/v1/opportunities/{opportunity}/people"))).GetProperty("contacts").EnumerateArray());
    }

    [Fact]
    public async Task Interactions_are_logged_newest_first_and_counted()
    {
        var session = await factory.SignedInAsync();
        var id = Id(await CreateAsync(session, "Ada"));
        var opportunity = await OpportunityAsync(session);

        var older = Yesterday.AddDays(-5);
        var first = await LogAsync(session, id, "  Replied to the outreach ", "linkedin", "outbound", older);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal("Replied to the outreach", (await Json(first)).GetProperty("summary").GetString());
        await LogAsync(session, id, "Phone screen", "phone", "inbound", Yesterday, opportunity);

        var items = (await Json(await session.GetAsync($"/api/v1/contacts/{id}/interactions"))).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["Phone screen", "Replied to the outreach"], items.Select(i => i.GetProperty("summary").GetString()));
        Assert.Equal("Backend Engineer", items[0].GetProperty("opportunityTitle").GetString());
        Assert.Equal(JsonValueKind.Null, items[1].GetProperty("opportunityTitle").ValueKind);
        Assert.Equal(("phone", "inbound", "Ada"), (items[0].GetProperty("channel").GetString(), items[0].GetProperty("direction").GetString(), items[0].GetProperty("contactName").GetString()));

        var row = (await Json(await session.GetAsync("/api/v1/contacts"))).GetProperty("items")[0];
        Assert.Equal(2, row.GetProperty("interactionCount").GetInt32());
        Assert.Equal(Yesterday.ToUnixTimeSeconds(), row.GetProperty("lastInteractionAt").GetDateTimeOffset().ToUnixTimeSeconds());

        var people = await Json(await session.GetAsync($"/api/v1/opportunities/{opportunity}/people"));
        Assert.Equal("Phone screen", Assert.Single(people.GetProperty("interactions").EnumerateArray()).GetProperty("summary").GetString());
    }

    [Fact]
    public async Task Invalid_interactions_are_rejected()
    {
        var session = await factory.SignedInAsync();
        var id = Id(await CreateAsync(session));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await LogAsync(session, id, channel: "carrier_pigeon")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await LogAsync(session, id, direction: "sideways")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await LogAsync(session, id, summary: "")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await LogAsync(session, id, summary: new string('x', 2001))).StatusCode);

        // A planned call is a reminder, but a clock a minute ahead of ours is fine.
        var future = await LogAsync(session, id, occurredAt: DateTimeOffset.UtcNow.AddHours(1));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, future.StatusCode);
        Assert.True((await Json(future)).GetProperty("errors").TryGetProperty("occurredAt", out _));
        Assert.Equal(HttpStatusCode.Created, (await LogAsync(session, id, occurredAt: DateTimeOffset.UtcNow.AddMinutes(1))).StatusCode);

        // Missing altogether reads as the year 1, which isn't when it happened.
        var missing = await session.PostAsync($"/api/v1/contacts/{id}/interactions", new { channel = "email", direction = "inbound", summary = "Hi" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
    }

    [Fact]
    public async Task An_interaction_can_be_about_any_of_your_opportunities_but_not_someone_elses()
    {
        var session = await factory.SignedInAsync();
        var id = Id(await CreateAsync(session));
        var other = await factory.SignedInAsync();
        var theirs = await OpportunityAsync(other);

        var response = await LogAsync(session, id, opportunityId: theirs);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("interaction.unknown_opportunity", await ErrorCode(response));
    }

    [Fact]
    public async Task An_interaction_keeps_its_opportunity_after_the_link_is_removed_and_can_be_deleted()
    {
        var session = await factory.SignedInAsync();
        var id = Id(await CreateAsync(session));
        var opportunity = await OpportunityAsync(session);
        await session.PostAsync($"/api/v1/contacts/{id}/opportunities/{opportunity}", new { relation = "recruiter" });
        var logged = await Json(await LogAsync(session, id, opportunityId: opportunity));

        await session.SendAsync(HttpMethod.Delete, $"/api/v1/contacts/{id}/opportunities/{opportunity}");
        var items = (await Json(await session.GetAsync($"/api/v1/contacts/{id}/interactions"))).GetProperty("items");
        Assert.Equal("Backend Engineer", items[0].GetProperty("opportunityTitle").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await session.SendAsync(HttpMethod.Delete, $"/api/v1/interactions/{Id(logged)}")).StatusCode);
        Assert.Empty((await Json(await session.GetAsync($"/api/v1/contacts/{id}/interactions"))).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await session.SendAsync(HttpMethod.Delete, $"/api/v1/interactions/{Id(logged)}")).StatusCode);
    }

    [Fact]
    public async Task Someone_elses_data_is_a_404_on_every_endpoint()
    {
        var owner = await factory.SignedInAsync();
        var id = Id(await CreateAsync(owner, "Secret Person"));
        var opportunity = await OpportunityAsync(owner);
        await owner.PostAsync($"/api/v1/contacts/{id}/opportunities/{opportunity}", new { relation = "recruiter" });
        var interaction = Id(await Json(await LogAsync(owner, id, opportunityId: opportunity)));

        var other = await factory.SignedInAsync();
        var own = Id(await CreateAsync(other, "Mine"));

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/contacts/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.SendAsync(HttpMethod.Put, $"/api/v1/contacts/{id}", Valid())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.SendAsync(HttpMethod.Delete, $"/api/v1/contacts/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/v1/contacts/{id}/restore", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/contacts/{id}/interactions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await LogAsync(other, id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.SendAsync(HttpMethod.Delete, $"/api/v1/interactions/{interaction}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/opportunities/{opportunity}/people")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.SendAsync(HttpMethod.Delete, $"/api/v1/contacts/{id}/opportunities/{opportunity}")).StatusCode);

        // Neither side of a link can belong to someone else.
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/v1/contacts/{id}/opportunities/{await OpportunityAsync(other)}", new { relation = "other" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/v1/contacts/{own}/opportunities/{opportunity}", new { relation = "other" })).StatusCode);

        Assert.Empty((await Json(await other.GetAsync("/api/v1/contacts?q=Secret"))).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.NewSession().GetAsync("/api/v1/contacts")).StatusCode);
    }

    [Fact]
    public async Task Changes_are_audited_without_what_was_said_or_how_to_reach_anyone()
    {
        var session = await factory.SignedInAsync();
        var email = $"{Unique()}@example.com";
        var id = Id(await CreateAsync(session, "Ada Secretname", email: email));
        var opportunity = await OpportunityAsync(session);
        await session.PostAsync($"/api/v1/contacts/{id}/opportunities/{opportunity}", new { relation = "recruiter" });
        await session.SendAsync(HttpMethod.Delete, $"/api/v1/contacts/{id}/opportunities/{opportunity}");
        var logged = Id(await Json(await LogAsync(session, id, "A very private conversation")));
        await session.SendAsync(HttpMethod.Delete, $"/api/v1/interactions/{logged}");
        await session.SendAsync(HttpMethod.Delete, $"/api/v1/contacts/{id}");
        await session.PostAsync($"/api/v1/contacts/{id}/restore", new { });
        var version = (await Json(await session.GetAsync($"/api/v1/contacts/{id}"))).GetProperty("version").GetUInt32();
        await session.SendAsync(HttpMethod.Put, $"/api/v1/contacts/{id}", new { name = "Ada Secretname", email, version });

        var logs = await session.GetAsync("/api/v1/audit-logs");
        var text = await logs.Content.ReadAsStringAsync();
        foreach (var action in new[] { "contact.created", "contact.updated", "contact.archived", "contact.restored", "contact.linked", "contact.unlinked", "interaction.logged", "interaction.deleted" })
        {
            Assert.Contains(action, text);
        }
        Assert.DoesNotContain("private conversation", text);
        Assert.DoesNotContain(email, text);
        Assert.DoesNotContain("Secretname", text);
    }
}
