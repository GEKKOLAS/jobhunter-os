using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JobHunter.Application.Integrations;
using JobHunter.Infrastructure.Opportunities.Imports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobHunter.Infrastructure.Integrations;

/// <summary>
/// Outreach Studio's public API (spec 02 §7): <c>GET /api/v1/me</c> and <c>POST /api/v1/sends</c>,
/// with an API key as a bearer token. The base URL is the user's, so the connection goes through
/// the SSRF guard's connector unless development allows the private network.
/// </summary>
public sealed class OutreachStudioClient(
    IHttpClientFactory httpClients, IOptions<OutreachOptions> options, ILogger<OutreachStudioClient> logger) : IOutreachClient
{
    public const string HttpClientName = "outreach-studio";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<OutreachAccount> GetMeAsync(string baseUrl, string apiKey, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Get, baseUrl, "/api/v1/me", apiKey);
        using var response = await SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);

        var me = await ReadAsync<MeResponse>(response, ct);
        return new OutreachAccount(me.DisplayName ?? "", me.DefaultAccountId);
    }

    public async Task<string> SendAsync(string baseUrl, string apiKey, OutreachSendRequest send, string idempotencyKey, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Post, baseUrl, "/api/v1/sends", apiKey);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Content = JsonContent.Create(new
        {
            connectedEmailAccountId = send.AccountId,
            templateVersionId = send.TemplateVersionId,
            recipients = new[] { new { email = send.ToEmail, name = send.ToName } },
            variables = new Dictionary<string, string> { ["subject"] = send.Subject, ["body"] = send.Body },
        }, options: Json);

        using var response = await SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        var job = await ReadAsync<SendResponse>(response, ct);
        return job.Id?.ToString() ?? throw new OutreachClientException(OutreachFailure.Unavailable, "Outreach Studio accepted it but returned no send id.");
    }

    private HttpRequestMessage Request(HttpMethod method, string baseUrl, string path, string apiKey)
    {
        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + path, UriKind.Absolute, out var url))
        {
            throw new OutreachClientException(OutreachFailure.Rejected, "The Outreach Studio address isn't valid.");
        }
        if (!options.Value.AllowPrivateNetwork && SsrfGuard.RejectUrl(url, allowHttp: false) is { } reason)
        {
            throw new OutreachClientException(OutreachFailure.Rejected, reason);
        }

        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await httpClients.CreateClient(HttpClientName).SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or BlockedAddressException
            || (ex is OperationCanceledException && !ct.IsCancellationRequested))
        {
            // Never the key or the URL's path in the log: only what failed.
            logger.LogWarning("Outreach Studio call failed: {Error}", ex.GetType().Name);
            throw new OutreachClientException(OutreachFailure.Unavailable,
                ex is BlockedAddressException ? "That address isn't a public website." : "Couldn't reach Outreach Studio.");
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var status = (int)response.StatusCode;
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new OutreachClientException(OutreachFailure.Unauthorized, "Outreach Studio didn't accept this API key.");
        }
        if (status is >= 400 and < 500 && response.StatusCode != HttpStatusCode.TooManyRequests)
        {
            var problem = await TryReadProblemAsync(response, ct);
            throw new OutreachClientException(OutreachFailure.Rejected,
                problem is null ? $"Outreach Studio refused it ({status})." : $"Outreach Studio refused it: {problem}");
        }
        throw new OutreachClientException(OutreachFailure.Unavailable,
            response.StatusCode == HttpStatusCode.TooManyRequests ? "Outreach Studio is busy. Try again in a minute." : $"Outreach Studio answered {status}.");
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, ct)
                ?? throw new OutreachClientException(OutreachFailure.Unavailable, "Outreach Studio answered with nothing.");
        }
        catch (JsonException)
        {
            throw new OutreachClientException(OutreachFailure.Unavailable, "Outreach Studio answered with something unexpected. Is this its address?");
        }
    }

    /// <summary>Its ProblemDetails title, kept short: it's shown to the user as the reason.</summary>
    private static async Task<string?> TryReadProblemAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json, ct);
            var text = problem?.Title ?? problem?.Detail;
            return string.IsNullOrWhiteSpace(text) ? null : text.Length <= 200 ? text : text[..200];
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private sealed record MeResponse(Guid Id, string? DisplayName, Guid? DefaultAccountId);

    private sealed record SendResponse(Guid? Id, string? Status);

    private sealed record ProblemResponse(string? Title, string? Detail);
}
