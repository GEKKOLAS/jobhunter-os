using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace JobHunter.IntegrationTests;

/// <summary>Phase 0 (spec 04): the API boots against PostgreSQL and its baseline behaviours hold.</summary>
[Collection(ApiCollection.Name)]
public class FoundationTests(ApiFactory factory)
{
    [Fact]
    public async Task Healthz_is_healthy_with_the_database_up()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ping_answers_with_the_service_name()
    {
        using var client = factory.CreateClient();

        var ping = await client.GetFromJsonAsync<JsonElement>("/api/v1/ping");

        Assert.Equal("ok", ping.GetProperty("status").GetString());
        Assert.Equal("jobhunter-api", ping.GetProperty("service").GetString());
    }

    [Fact]
    public async Task Responses_carry_the_security_headers()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/ping");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("same-origin", response.Headers.GetValues("Cross-Origin-Opener-Policy").Single());
        // Plain HTTP in tests: HSTS is for HTTPS only.
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Unknown_routes_answer_with_problem_details()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
    }
}
