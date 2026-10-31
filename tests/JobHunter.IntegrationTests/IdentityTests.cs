using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JobHunter.IntegrationTests;

/// <summary>Phase 1 (spec 03, Identity): accounts, sessions, CSRF, password reset, audit.</summary>
[Collection(ApiCollection.Name)]
public class IdentityTests(ApiFactory factory)
{
    private const string Password = "correct horse battery staple";

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private async Task<(TestSession Session, string Email)> RegisterAsync(string displayName = "Ada Lovelace")
    {
        var session = factory.NewSession();
        var email = NewEmail();
        var response = await session.PostAsync("/api/v1/auth/register", new { email, password = Password, displayName });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (session, email);
    }

    private async Task<TestSession> LoginAsync(string email, string password = Password)
    {
        var session = factory.NewSession();
        var response = await session.PostAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return session;
    }

    private static async Task<string?> ErrorCode(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString();

    [Fact]
    public async Task Register_signs_in_and_me_returns_the_new_user()
    {
        var (session, email) = await RegisterAsync();

        Assert.True(session.HasSessionCookie);
        Assert.NotNull(session.CsrfToken);
        var me = await (await session.GetAsync("/api/v1/me")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, me.GetProperty("email").GetString());
        Assert.Equal("Ada Lovelace", me.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Registering_a_taken_email_looks_the_same_but_signs_nobody_in()
    {
        var (_, email) = await RegisterAsync();
        var second = factory.NewSession();

        // Different case: emails are case-insensitive.
        var response = await second.PostAsync("/api/v1/auth/register",
            new { email = email.ToUpperInvariant(), password = "another long password", displayName = "Mallory" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("user", out _));
        Assert.False(body.TryGetProperty("csrfToken", out _));
        Assert.False(second.HasSessionCookie);
    }

    [Fact]
    public async Task Short_passwords_are_rejected_with_field_errors()
    {
        var response = await factory.NewSession().PostAsync("/api/v1/auth/register",
            new { email = NewEmail(), password = "short", displayName = "Ada" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation_failed", problem.GetProperty("errorCode").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Login_rejects_a_wrong_password_and_an_unknown_email_the_same_way()
    {
        var (_, email) = await RegisterAsync();

        var wrong = await factory.NewSession().PostAsync("/api/v1/auth/login", new { email, password = "not the password" });
        var unknown = await factory.NewSession().PostAsync("/api/v1/auth/login", new { email = NewEmail(), password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal("auth.invalid_credentials", await ErrorCode(wrong));
        Assert.Equal("auth.invalid_credentials", await ErrorCode(unknown));
    }

    [Fact]
    public async Task Login_returns_the_csrf_token_for_native_clients()
    {
        var (_, email) = await RegisterAsync();
        var session = factory.NewSession();

        var body = await (await session.PostAsync("/api/v1/auth/login", new { email = $"  {email}  ", password = Password }))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(session.CsrfToken, body.GetProperty("csrfToken").GetString());
    }

    [Fact]
    public async Task Endpoints_require_a_session_by_default()
    {
        var anonymous = factory.NewSession();

        foreach (var path in new[] { "/api/v1/me", "/api/v1/auth/sessions", "/api/v1/audit-logs" })
        {
            var response = await anonymous.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
    }

    [Fact]
    public async Task Unsafe_requests_with_a_session_need_the_csrf_header()
    {
        var (session, _) = await RegisterAsync();

        var withoutHeader = await session.SendAsync(HttpMethod.Patch, "/api/v1/me", new { displayName = "Eve" }, includeCsrfHeader: false);
        var withHeader = await session.PatchAsync("/api/v1/me", new { displayName = "Ada King" });

        Assert.Equal(HttpStatusCode.Forbidden, withoutHeader.StatusCode);
        Assert.Equal("csrf.invalid", await ErrorCode(withoutHeader));
        Assert.Equal(HttpStatusCode.OK, withHeader.StatusCode);
        Assert.Equal("Ada King", (await withHeader.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var (session, _) = await RegisterAsync();
        var cookies = new Dictionary<string, string>(session.Cookies);

        Assert.Equal(HttpStatusCode.NoContent, (await session.PostAsync("/api/v1/auth/logout")).StatusCode);

        // Replaying the old cookie doesn't work either.
        var replay = factory.NewSession();
        foreach (var (name, value) in cookies) replay.Cookies[name] = value;
        Assert.Equal(HttpStatusCode.Unauthorized, (await replay.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Sessions_can_be_listed_and_revoked_but_only_your_own()
    {
        var (laptop, email) = await RegisterAsync();
        var phone = await LoginAsync(email);
        var (stranger, _) = await RegisterAsync("Eve");

        var sessions = (await (await laptop.GetAsync("/api/v1/auth/sessions")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, sessions.Count);
        Assert.Single(sessions, s => s.GetProperty("current").GetBoolean());
        var phoneSessionId = sessions.Single(s => !s.GetProperty("current").GetBoolean()).GetProperty("id").GetString();

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/v1/auth/sessions/{phoneSessionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync("/api/v1/me")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await laptop.DeleteAsync($"/api/v1/auth/sessions/{phoneSessionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await laptop.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Changing_the_password_keeps_this_session_and_ends_the_others()
    {
        var (laptop, email) = await RegisterAsync();
        var phone = await LoginAsync(email);

        var wrong = await laptop.PostAsync("/api/v1/me/password", new { currentPassword = "not it at all", newPassword = "a brand new long password" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal("auth.wrong_password", await ErrorCode(wrong));

        var ok = await laptop.PostAsync("/api/v1/me/password", new { currentPassword = Password, newPassword = "a brand new long password" });
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await laptop.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/v1/me")).StatusCode);
        await LoginAsync(email, "a brand new long password");
    }

    [Fact]
    public async Task Password_reset_works_once_and_signs_out_everywhere()
    {
        var (session, email) = await RegisterAsync();

        var unknown = await factory.NewSession().PostAsync("/api/v1/auth/password/forgot", new { email = NewEmail() });
        var known = await factory.NewSession().PostAsync("/api/v1/auth/password/forgot", new { email });
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);

        Assert.StartsWith("http://web.test/reset-password?token=", factory.ResetLinks.LastUrlFor(email));
        var token = factory.ResetLinks.LastTokenFor(email)!;

        var reset = await factory.NewSession().PostAsync("/api/v1/auth/password/reset", new { token, newPassword = "a fresh long password" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/v1/me")).StatusCode);
        await LoginAsync(email, "a fresh long password");

        var reuse = await factory.NewSession().PostAsync("/api/v1/auth/password/reset", new { token, newPassword = "yet another long password" });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal("auth.invalid_token", await ErrorCode(reuse));
    }

    [Fact]
    public async Task The_audit_log_shows_your_own_events_only()
    {
        var (session, email) = await RegisterAsync();
        await factory.NewSession().PostAsync("/api/v1/auth/login", new { email, password = "not the password" });
        var (stranger, _) = await RegisterAsync("Eve");

        var mine = await (await session.GetAsync("/api/v1/audit-logs")).Content.ReadFromJsonAsync<JsonElement>();
        var actions = mine.GetProperty("items").EnumerateArray().Select(a => a.GetProperty("action").GetString()).ToList();
        Assert.Equal(["auth.login_failed", "auth.register"], actions);
        Assert.Equal(2, mine.GetProperty("totalCount").GetInt32());

        var theirs = await (await stranger.GetAsync("/api/v1/audit-logs?action=auth.login_failed")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, theirs.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Sign_in_attempts_are_rate_limited_per_client()
    {
        using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:Auth:PermitLimit", "3"));
        var client = new TestSession(limited.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false }));

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            statuses.Add((await client.PostAsync("/api/v1/auth/login", new { email = NewEmail(), password = Password })).StatusCode);
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], statuses);
    }
}
