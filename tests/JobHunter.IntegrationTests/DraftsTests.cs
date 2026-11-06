using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobHunter.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.IntegrationTests;

/// <summary>Phase 7 (spec 03, Follow-ups and Integrations): drafts through approval to sent, and the Outreach Studio connection.</summary>
[Collection(ApiCollection.Name)]
public class DraftsTests(ApiFactory factory)
{
    private const string Studio = "https://mth.outreach-studio.app";
    private const string MeUrl = Studio + "/api/v1/me";
    private const string SendsUrl = Studio + "/api/v1/sends";
    private const string MessagesUrl = "https://api.anthropic.com/v1/messages";
    private static readonly Guid TemplateVersion = Guid.Parse("0199a0f0-0000-7000-8000-000000000001");
    private static readonly Guid Mailbox = Guid.Parse("0199a0f0-0000-7000-8000-0000000000aa");

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string Id(JsonElement element) => element.GetProperty("id").GetString()!;

    private static async Task<(string Opportunity, string Contact)> SetUpAsync(TestSession session, string email = "ada@example.com")
    {
        var opportunity = Id(await Json(await session.PostAsync("/api/v1/opportunities", new { title = "Backend Engineer", companyName = "Contoso", stage = "applied" })));
        var contact = Id(await Json(await session.PostAsync("/api/v1/contacts", new { name = "Ada Recruiter", email = $"{Guid.NewGuid():N}-{email}" })));
        return (opportunity, contact);
    }

    private static async Task<JsonElement> DraftAsync(TestSession session, string? opportunityId, string? contactId, bool generate = false, string[]? facts = null)
    {
        var response = await session.PostAsync("/api/v1/drafts", new { kind = "follow_up", opportunityId, contactId, generate, facts });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Json(response);
    }

    private static Task<HttpResponseMessage> EditAsync(TestSession session, JsonElement draft, string subject, string body, string? toEmail = null) =>
        session.SendAsync(HttpMethod.Put, $"/api/v1/drafts/{Id(draft)}", new
        {
            toEmail = toEmail ?? draft.GetProperty("toEmail").GetString(),
            subject,
            body,
            version = draft.GetProperty("version").GetUInt32(),
        });

    private static async Task<JsonElement> ReadyToSendAsync(TestSession session, string opportunity, string contact)
    {
        var draft = (await DraftAsync(session, opportunity, contact)).GetProperty("draft");
        var edited = await Json(await EditAsync(session, draft, "Following up on Backend Engineer", "Hi Ada,\nAny news?\nGrace"));
        return await Json(await session.PostAsync($"/api/v1/drafts/{Id(edited)}/approve", new { }));
    }

    private static Task<HttpResponseMessage> ConnectAsync(TestSession session, string? apiKey = "mth_test_key_1234abcd", string baseUrl = Studio) =>
        session.SendAsync(HttpMethod.Put, "/api/v1/integrations/outreach", new { baseUrl, apiKey, templateVersionId = TemplateVersion });

    private void StudioAnswers(Guid? defaultMailbox = null, HttpStatusCode meStatus = HttpStatusCode.OK)
    {
        factory.Outside.OnJson(MeUrl, JsonSerializer.Serialize(new
        {
            id = Guid.NewGuid(),
            email = "grace@example.com",
            displayName = "Grace",
            defaultAccountId = defaultMailbox,
            createdAt = DateTimeOffset.UtcNow,
        }), meStatus);
    }

    [Fact]
    public async Task Without_AI_a_draft_starts_blank_addressed_to_the_contact_and_says_why()
    {
        var session = await factory.SignedInAsync();
        var (opportunity, contact) = await SetUpAsync(session);

        var created = await DraftAsync(session, opportunity, contact, generate: true);

        Assert.False(created.GetProperty("generated").GetBoolean());
        Assert.Contains("isn't set up", created.GetProperty("generationNote").GetString());
        var draft = created.GetProperty("draft");
        Assert.Equal(("draft", "user", ""), (draft.GetProperty("status").GetString(), draft.GetProperty("origin").GetString(), draft.GetProperty("subject").GetString()));
        Assert.EndsWith("ada@example.com", draft.GetProperty("toEmail").GetString());
        Assert.Equal(("Backend Engineer", "Ada Recruiter"), (draft.GetProperty("opportunityTitle").GetString(), draft.GetProperty("contactName").GetString()));

        var capabilities = await Json(await session.GetAsync("/api/v1/ai/capabilities"));
        Assert.Equal(("rules", false), (capabilities.GetProperty("extraction").GetString(), capabilities.GetProperty("drafts").GetBoolean()));
    }

    [Fact]
    public async Task Approval_needs_words_an_edit_takes_it_back_and_closed_drafts_stay_closed()
    {
        var session = await factory.SignedInAsync();
        var (opportunity, contact) = await SetUpAsync(session);
        var draft = (await DraftAsync(session, opportunity, contact)).GetProperty("draft");

        var empty = await session.PostAsync($"/api/v1/drafts/{Id(draft)}/approve", new { });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, empty.StatusCode);
        Assert.Equal("draft.incomplete", (await Json(empty)).GetProperty("errorCode").GetString());

        var written = await Json(await EditAsync(session, draft, "Following up", "Hi Ada"));
        var approved = await Json(await session.PostAsync($"/api/v1/drafts/{Id(draft)}/approve", new { }));
        Assert.Equal("approved", approved.GetProperty("status").GetString());

        // What was approved is what goes out: any change needs approving again.
        var changed = await Json(await EditAsync(session, approved, "Following up", "Hi Ada, any news?"));
        Assert.Equal("draft", changed.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, changed.GetProperty("approvedAt").ValueKind);

        Assert.Equal(HttpStatusCode.NoContent, (await session.SendAsync(HttpMethod.Delete, $"/api/v1/drafts/{Id(draft)}")).StatusCode);
        var late = await EditAsync(session, await Json(await session.GetAsync($"/api/v1/drafts?status=discarded")) is var list ? list.GetProperty("items")[0] : written, "x", "y");
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Equal("draft.closed", (await Json(late)).GetProperty("errorCode").GetString());
        Assert.Empty((await Json(await session.GetAsync("/api/v1/drafts"))).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Sending_needs_approval_a_recipient_and_a_connection()
    {
        var session = await factory.SignedInAsync();
        var (opportunity, contact) = await SetUpAsync(session);
        var draft = (await DraftAsync(session, opportunity, contact)).GetProperty("draft");
        await EditAsync(session, draft, "Following up", "Hi Ada");

        var unapproved = await session.PostAsync($"/api/v1/drafts/{Id(draft)}/send", new { });
        Assert.Equal(HttpStatusCode.Conflict, unapproved.StatusCode);
        Assert.Equal("draft.not_approved", (await Json(unapproved)).GetProperty("errorCode").GetString());

        var approved = await Json(await session.PostAsync($"/api/v1/drafts/{Id(draft)}/approve", new { }));
        var notConnected = await session.PostAsync($"/api/v1/drafts/{Id(draft)}/send", new { });
        Assert.Equal(HttpStatusCode.Conflict, notConnected.StatusCode);
        Assert.Equal("outreach.not_configured", (await Json(notConnected)).GetProperty("errorCode").GetString());

        var noRecipient = (await DraftAsync(session, opportunity, null)).GetProperty("draft");
        await EditAsync(session, noRecipient, "Hi", "There", toEmail: "");
        await session.PostAsync($"/api/v1/drafts/{Id(noRecipient)}/approve", new { });
        var nobody = await session.PostAsync($"/api/v1/drafts/{Id(noRecipient)}/send", new { });
        Assert.Equal("draft.no_recipient", (await Json(nobody)).GetProperty("errorCode").GetString());
        Assert.Equal("approved", approved.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Marking_sent_by_hand_logs_the_email_on_the_contact()
    {
        var session = await factory.SignedInAsync();
        var (opportunity, contact) = await SetUpAsync(session);
        var draft = (await DraftAsync(session, opportunity, contact)).GetProperty("draft");
        await EditAsync(session, draft, "Following up on Backend Engineer", "Hi Ada");

        var sent = await Json(await session.PostAsync($"/api/v1/drafts/{Id(draft)}/mark-sent", new { }));

        Assert.Equal(("sent", "manual"), (sent.GetProperty("status").GetString(), sent.GetProperty("sentVia").GetString()));
        var log = (await Json(await session.GetAsync($"/api/v1/contacts/{contact}/interactions"))).GetProperty("items");
        var entry = Assert.Single(log.EnumerateArray());
        Assert.Equal(("email", "outbound", opportunity), (entry.GetProperty("channel").GetString(), entry.GetProperty("direction").GetString(), entry.GetProperty("opportunityId").GetString()));
        Assert.Contains("Following up on Backend Engineer", entry.GetProperty("summary").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await session.PostAsync($"/api/v1/drafts/{Id(draft)}/mark-sent", new { })).StatusCode);
    }

    [Fact]
    public async Task The_connection_keeps_its_key_encrypted_and_never_shows_it()
    {
        var session = await factory.SignedInAsync();

        Assert.False((await Json(await session.GetAsync("/api/v1/integrations/outreach"))).GetProperty("configured").GetBoolean());
        var noKey = await ConnectAsync(session, apiKey: null);
        Assert.Equal("outreach.key_required", (await Json(noKey)).GetProperty("errorCode").GetString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ConnectAsync(session, apiKey: "sk-not-ours")).StatusCode);
        // Outside Development the address has to be a public https site.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ConnectAsync(session, baseUrl: "http://localhost:5001")).StatusCode);

        var saved = await ConnectAsync(session, apiKey: "mth_secret_value_wxyz");
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var text = await saved.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret_value", text);
        var settings = JsonDocument.Parse(text).RootElement;
        Assert.Equal((true, "wxyz", Studio), (settings.GetProperty("configured").GetBoolean(), settings.GetProperty("keyHint").GetString(), settings.GetProperty("baseUrl").GetString()));

        // At rest: no trace of the key in the row.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.OutreachConnections.AsNoTracking().OrderByDescending(c => c.CreatedAt).FirstAsync();
            Assert.DoesNotContain("secret_value", System.Text.Encoding.UTF8.GetString(row.ApiKeyCiphertext));
            Assert.Equal(12, row.ApiKeyNonce.Length);
        }

        // Saving again without a key keeps the one there.
        var kept = await Json(await ConnectAsync(session, apiKey: null));
        Assert.Equal("wxyz", kept.GetProperty("keyHint").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await session.SendAsync(HttpMethod.Delete, "/api/v1/integrations/outreach")).StatusCode);
        Assert.False((await Json(await session.GetAsync("/api/v1/integrations/outreach"))).GetProperty("configured").GetBoolean());
    }

    // These share Outreach Studio's stub routes, so they run as one test rather than racing each other.
    [Fact]
    public async Task Through_Outreach_Studio_a_send_is_tested_sent_once_and_logged()
    {
        var session = await factory.SignedInAsync();
        await ConnectAsync(session, apiKey: "mth_live_key_9876");

        // Testing the connection: a wrong key, no default mailbox, then all good.
        StudioAnswers(meStatus: HttpStatusCode.Unauthorized);
        var refused = await Json(await session.PostAsync("/api/v1/integrations/outreach/test", new { }));
        Assert.False(refused.GetProperty("ok").GetBoolean());
        Assert.Contains("didn't accept", refused.GetProperty("message").GetString());
        StudioAnswers(defaultMailbox: null);
        var noMailbox = await Json(await session.PostAsync("/api/v1/integrations/outreach/test", new { }));
        Assert.True(noMailbox.GetProperty("ok").GetBoolean());
        Assert.False(noMailbox.GetProperty("hasDefaultMailbox").GetBoolean());
        StudioAnswers(defaultMailbox: Mailbox);
        var ok = await Json(await session.PostAsync("/api/v1/integrations/outreach/test", new { }));
        Assert.Equal((true, "Grace", true), (ok.GetProperty("ok").GetBoolean(), ok.GetProperty("displayName").GetString(), ok.GetProperty("hasDefaultMailbox").GetBoolean()));
        Assert.Equal("Bearer mth_live_key_9876", factory.Outside.RequestsTo(MeUrl).Last().Headers["Authorization"]);

        var (opportunity, contact) = await SetUpAsync(session);
        var draft = await ReadyToSendAsync(session, opportunity, contact);

        // Outreach Studio refusing it, then failing, leaves it approved and unsent.
        factory.Outside.OnJson(SendsUrl, """{"title":"The template is missing a variable: body.","errorCode":"template.missing_variable"}""", HttpStatusCode.UnprocessableEntity);
        var rejected = await session.PostAsync($"/api/v1/drafts/{Id(draft)}/send", new { });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        var rejection = await Json(rejected);
        Assert.Equal("outreach.rejected", rejection.GetProperty("errorCode").GetString());
        Assert.Contains("missing a variable", rejection.GetProperty("title").GetString());
        factory.Outside.OnJson(SendsUrl, "{}", HttpStatusCode.ServiceUnavailable);
        Assert.Equal(HttpStatusCode.BadGateway, (await session.PostAsync($"/api/v1/drafts/{Id(draft)}/send", new { })).StatusCode);

        var sendId = Guid.NewGuid();
        factory.Outside.OnJson(SendsUrl, JsonSerializer.Serialize(new { id = sendId, status = "queued" }), HttpStatusCode.Accepted);
        var sent = await session.PostAsync($"/api/v1/drafts/{Id(draft)}/send", new { });
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        var body = await Json(sent);
        Assert.Equal(("sent", "outreach_studio", sendId.ToString()), (body.GetProperty("status").GetString(), body.GetProperty("sentVia").GetString(), body.GetProperty("outreachSendId").GetString()));

        // Every attempt carried the same key, so Outreach Studio would send it once however many retries.
        var attempts = factory.Outside.RequestsTo(SendsUrl).Where(r => r.Body!.Contains(Id(draft)) || r.Headers["Idempotency-Key"].EndsWith(Id(draft))).ToList();
        Assert.Equal(3, attempts.Count);
        Assert.All(attempts, a => Assert.Equal($"jobhunter-draft-{Id(draft)}", a.Headers["Idempotency-Key"]));
        using var request = JsonDocument.Parse(attempts.Last().Body!);
        Assert.Equal(Mailbox.ToString(), request.RootElement.GetProperty("connectedEmailAccountId").GetString());
        Assert.Equal(TemplateVersion.ToString(), request.RootElement.GetProperty("templateVersionId").GetString());
        Assert.Equal("Following up on Backend Engineer", request.RootElement.GetProperty("variables").GetProperty("subject").GetString());
        Assert.EndsWith("ada@example.com", request.RootElement.GetProperty("recipients")[0].GetProperty("email").GetString());

        var entry = Assert.Single((await Json(await session.GetAsync($"/api/v1/contacts/{contact}/interactions"))).GetProperty("items").EnumerateArray());
        Assert.Contains("through Outreach Studio", entry.GetProperty("summary").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await session.PostAsync($"/api/v1/drafts/{Id(draft)}/send", new { })).StatusCode);

        // A default mailbox removed in Outreach Studio stops the next one before it's sent.
        StudioAnswers(defaultMailbox: null);
        var next = await ReadyToSendAsync(session, opportunity, contact);
        var stopped = await session.PostAsync($"/api/v1/drafts/{Id(next)}/send", new { });
        Assert.Equal("outreach.no_mailbox", (await Json(stopped)).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Someone_elses_drafts_and_connection_are_out_of_reach()
    {
        var owner = await factory.SignedInAsync();
        var (opportunity, contact) = await SetUpAsync(owner);
        var draft = (await DraftAsync(owner, opportunity, contact)).GetProperty("draft");
        await ConnectAsync(owner);

        var other = await factory.SignedInAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/v1/drafts/{Id(draft)}/approve", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/v1/drafts/{Id(draft)}/mark-sent", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.SendAsync(HttpMethod.Delete, $"/api/v1/drafts/{Id(draft)}")).StatusCode);
        Assert.Equal("draft.unknown_contact", (await Json(await other.PostAsync("/api/v1/drafts", new { kind = "follow_up", contactId = contact }))).GetProperty("errorCode").GetString());
        Assert.Empty((await Json(await other.GetAsync("/api/v1/drafts"))).GetProperty("items").EnumerateArray());
        Assert.False((await Json(await other.GetAsync("/api/v1/integrations/outreach"))).GetProperty("configured").GetBoolean());
    }

    private TestSession WithAiKey() => new(factory
        .WithWebHostBuilder(builder => builder.UseSetting("Ai:ApiKey", "test-key"))
        .CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false }));

    private static string ClaudeDraft(string subject, string body) => JsonSerializer.Serialize(new
    {
        id = "msg_1",
        type = "message",
        role = "assistant",
        model = "claude-opus-5-5",
        content = new object[] { new { type = "text", text = JsonSerializer.Serialize(new { subject, body }) } },
        stop_reason = "end_turn",
        stop_sequence = (string?)null,
        usage = new { input_tokens = 10, output_tokens = 20 },
    });

    // One stub route for the Messages API, so the written and the rejected cases share a test.
    [Fact]
    public async Task With_a_key_Claude_writes_from_the_facts_and_a_draft_that_invents_one_is_thrown_away()
    {
        var session = WithAiKey();
        await session.PostAsync("/api/v1/auth/register", new { email = $"ai-{Guid.NewGuid():N}@example.com", password = "correct horse battery staple", displayName = "Grace" });
        var (opportunity, contact) = await SetUpAsync(session);
        var facts = new[] { "I built the payments API at my last job." };

        factory.Outside.OnJson(MessagesUrl, ClaudeDraft("Following up on Backend Engineer", "Hi Ada,\n\nI applied for Backend Engineer at Contoso and remain interested. I built the payments API at my last job.\n\nGrace"));
        var written = await Json(await session.PostAsync("/api/v1/drafts", new { kind = "follow_up", opportunityId = opportunity, contactId = contact, generate = true, facts }));
        Assert.True(written.GetProperty("generated").GetBoolean());
        Assert.Equal(("ai", "draft"), (written.GetProperty("draft").GetProperty("origin").GetString(), written.GetProperty("draft").GetProperty("status").GetString()));
        Assert.Contains("payments API", written.GetProperty("draft").GetProperty("body").GetString());

        // The brief went in as delimited data, with the facts and nothing about anyone else.
        using var sentPrompt = JsonDocument.Parse(factory.Outside.RequestsTo(MessagesUrl).Last().Body!);
        var prompt = sentPrompt.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!;
        Assert.StartsWith("<brief>", prompt);
        Assert.Contains("Fact: I built the payments API at my last job.", prompt);
        Assert.Contains("Writing to: Ada Recruiter", prompt);

        // "5 years" and Kubernetes are in no fact: the draft is rejected, not repaired.
        factory.Outside.OnJson(MessagesUrl, ClaudeDraft("Following up", "Hi Ada, with 5 years of Kubernetes I'd be a great fit. Grace"));
        var rejected = await Json(await session.PostAsync("/api/v1/drafts", new { kind = "follow_up", opportunityId = opportunity, contactId = contact, generate = true, facts }));
        Assert.False(rejected.GetProperty("generated").GetBoolean());
        Assert.Contains("thrown away", rejected.GetProperty("generationNote").GetString());
        Assert.Equal(("", "user"), (rejected.GetProperty("draft").GetProperty("body").GetString(), rejected.GetProperty("draft").GetProperty("origin").GetString()));
    }
}
