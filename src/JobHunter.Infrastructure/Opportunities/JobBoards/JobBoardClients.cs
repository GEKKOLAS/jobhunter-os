using System.Net;
using System.Text;
using System.Text.Json;
using JobHunter.Application.Opportunities.Imports;
using JobHunter.Domain.Opportunities;
using JobHunter.Infrastructure.Opportunities.Imports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobHunter.Infrastructure.Opportunities.JobBoards;

/// <summary>What all three public job-board APIs share: a GET that may 404, and tolerant JSON reading.</summary>
public abstract class JobBoardClient(IHttpClientFactory httpClients, ILogger logger) : IJobBoardClient
{
    public const string HttpClientName = "job-boards";

    public abstract string Provider { get; }

    protected abstract string UrlFor(string board);

    /// <summary>The postings in the provider's answer, leaving out unlisted ones.</summary>
    protected abstract IEnumerable<BoardPosting> Read(JsonElement root);

    public async Task<IReadOnlyList<BoardPosting>?> ListAsync(string board, CancellationToken cancellationToken)
    {
        if (!ImportRules.IsValidBoardToken(board)) return null;

        try
        {
            var http = httpClients.CreateClient(HttpClientName);
            using var response = await GetWithOneRetryAsync(http, UrlFor(board), cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("{Provider} answered {Status} for board {Board}", Provider, (int)response.StatusCode, board);
                throw SourceErrors.Unavailable(ProviderName);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return Read(json.RootElement).ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "{Provider} failed for board {Board}", Provider, board);
            throw SourceErrors.Unavailable(ProviderName);
        }
    }

    /// <summary>The providers' gateways do hiccup (seen live from Lever), and one quick retry usually clears it.</summary>
    private static async Task<HttpResponseMessage> GetWithOneRetryAsync(HttpClient http, string url, CancellationToken ct)
    {
        var response = await http.GetAsync(url, ct);
        if (response.StatusCode is not (HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout))
        {
            return response;
        }

        response.Dispose();
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        return await http.GetAsync(url, ct);
    }

    private string ProviderName => char.ToUpperInvariant(Provider[0]) + Provider[1..];

    protected static string? Text(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? (string.IsNullOrWhiteSpace(value.GetString()) ? null : value.GetString()!.Trim())
            : null;

    protected static JsonElement Child(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) ? value : default;

    protected static int? Whole(JsonElement parent, string name)
    {
        var value = Child(parent, name);
        return value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) && number is > 0 and < 100_000_000
            ? (int)number
            : null;
    }

    /// <summary>A provider's link, only if it's a web address: it ends up in an href.</summary>
    protected static string? WebUrl(JsonElement parent, string name)
    {
        var url = Text(parent, name);
        return url is { Length: <= 2048 }
            && Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                ? url
                : null;
    }

    /// <summary>Provider text fields can be long; these match the opportunity's limits.</summary>
    protected static string Limit(string? value, int max)
    {
        var text = value ?? "";
        return text.Length <= max ? text : text[..max].TrimEnd();
    }

    protected const int MaxDescription = 50_000;
    protected const int MaxTitle = 160;
    protected const int MaxLocation = 120;
}

/// <summary><c>GET /v1/boards/{token}/jobs?content=true</c>. <c>content</c> is entity-encoded HTML.</summary>
public sealed class GreenhouseClient(IHttpClientFactory http, IOptions<ImportOptions> options, ILogger<GreenhouseClient> logger)
    : JobBoardClient(http, logger)
{
    public override string Provider => ImportRules.Greenhouse;

    protected override string UrlFor(string board) =>
        $"{options.Value.GreenhouseBaseUrl.TrimEnd('/')}/v1/boards/{Uri.EscapeDataString(board)}/jobs?content=true";

    protected override IEnumerable<BoardPosting> Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("jobs", out var jobs) || jobs.ValueKind != JsonValueKind.Array) yield break;

        foreach (var job in jobs.EnumerateArray())
        {
            var id = Child(job, "id");
            var title = Text(job, "title");
            if (id.ValueKind != JsonValueKind.Number || title is null) continue;

            var location = Text(Child(job, "location"), "name");
            var description = HtmlText.From(WebUtility.HtmlDecode(Text(job, "content") ?? ""));
            // Saving from a board has no preview to check an assumption in, so a bare "$" isn't taken as USD.
            var salary = TextFacts.Salary(description) is { CurrencyAssumed: false } stated ? stated : null;
            yield return new BoardPosting(
                id.GetRawText(), Limit(title, MaxTitle), Text(job, "company_name"), location is null ? null : Limit(location, MaxLocation),
                WebUrl(job, "absolute_url"), TextFacts.WorkMode($"{location}\n{description}"), TextFacts.EmploymentType(description),
                salary?.Min, salary?.Max, salary?.Currency, salary?.Period, Limit(description, MaxDescription));
        }
    }
}

/// <summary><c>GET /v0/postings/{company}?mode=json</c>. The requirements live in <c>lists</c>, apart from the description.</summary>
public sealed class LeverClient(IHttpClientFactory http, IOptions<ImportOptions> options, ILogger<LeverClient> logger)
    : JobBoardClient(http, logger)
{
    public override string Provider => ImportRules.Lever;

    protected override string UrlFor(string board) =>
        $"{options.Value.LeverBaseUrl.TrimEnd('/')}/v0/postings/{Uri.EscapeDataString(board)}?mode=json";

    protected override IEnumerable<BoardPosting> Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array) yield break;

        foreach (var job in root.EnumerateArray())
        {
            var id = Text(job, "id");
            var title = Text(job, "text");
            if (id is null || title is null) continue;

            var categories = Child(job, "categories");
            var location = Text(categories, "location");
            var salary = Child(job, "salaryRange");
            yield return new BoardPosting(
                id, Limit(title, MaxTitle), null, location is null ? null : Limit(location, MaxLocation), WebUrl(job, "hostedUrl"),
                Text(job, "workplaceType") switch { "remote" => "remote", "hybrid" => "hybrid", "on-site" or "onsite" => "onsite", _ => null },
                TypeOf(Text(categories, "commitment")),
                Whole(salary, "min"), Whole(salary, "max"), Text(salary, "currency")?.ToUpperInvariant(), PeriodOf(Text(salary, "interval")),
                Limit(Assemble(job), MaxDescription));
        }
    }

    /// <summary>"Regular Full Time (Salary)", "Contract", "Intern 2026": whatever the company typed.</summary>
    private static string? TypeOf(string? commitment)
    {
        var text = commitment?.ToLowerInvariant().Replace("-", " ") ?? "";
        if (text.Contains("full time")) return "full_time";
        if (text.Contains("part time")) return "part_time";
        if (text.Contains("intern")) return "internship";
        if (text.Contains("contract") || text.Contains("temporary")) return "contract";
        return null;
    }

    private static string? PeriodOf(string? interval) => interval switch
    {
        not null when interval.Contains("year") => "year",
        not null when interval.Contains("month") => "month",
        not null when interval.Contains("hour") => "hour",
        _ => null,
    };

    private static string Assemble(JsonElement job)
    {
        var text = new StringBuilder(Text(job, "descriptionPlain") ?? "");
        var lists = Child(job, "lists");
        if (lists.ValueKind == JsonValueKind.Array)
        {
            foreach (var list in lists.EnumerateArray())
            {
                text.Append("\n\n").Append(Text(list, "text")).Append('\n');
                // <li> items without a parent list: wrap them so they read as one.
                text.Append(HtmlText.From($"<ul>{Text(list, "content")}</ul>"));
            }
        }
        if (Text(job, "additionalPlain") is { } additional) text.Append("\n\n").Append(additional);
        return text.ToString().Trim();
    }
}

/// <summary><c>GET /posting-api/job-board/{name}?includeCompensation=true</c>.</summary>
public sealed class AshbyClient(IHttpClientFactory http, IOptions<ImportOptions> options, ILogger<AshbyClient> logger)
    : JobBoardClient(http, logger)
{
    public override string Provider => ImportRules.Ashby;

    protected override string UrlFor(string board) =>
        $"{options.Value.AshbyBaseUrl.TrimEnd('/')}/posting-api/job-board/{Uri.EscapeDataString(board)}?includeCompensation=true";

    protected override IEnumerable<BoardPosting> Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("jobs", out var jobs) || jobs.ValueKind != JsonValueKind.Array) yield break;

        foreach (var job in jobs.EnumerateArray())
        {
            var id = Text(job, "id");
            var title = Text(job, "title");
            if (id is null || title is null) continue;
            if (Child(job, "isListed").ValueKind == JsonValueKind.False) continue;

            var location = Text(job, "location");
            var (min, max, currency, period) = SalaryOf(job);
            yield return new BoardPosting(
                id, Limit(title, MaxTitle), null, location is null ? null : Limit(location, MaxLocation), WebUrl(job, "jobUrl"),
                WorkModeOf(job),
                Text(job, "employmentType") switch
                {
                    "FullTime" => "full_time",
                    "PartTime" => "part_time",
                    "Intern" => "internship",
                    "Contract" or "Temporary" => "contract",
                    _ => null,
                },
                min, max, currency, period, Limit(Text(job, "descriptionPlain"), MaxDescription));
        }
    }

    private static string? WorkModeOf(JsonElement job) => Text(job, "workplaceType") switch
    {
        "Remote" => "remote",
        "Hybrid" => "hybrid",
        "OnSite" => "onsite",
        _ => Child(job, "isRemote").ValueKind == JsonValueKind.True ? "remote" : null,
    };

    private static (int? Min, int? Max, string? Currency, string? Period) SalaryOf(JsonElement job)
    {
        var components = Child(Child(job, "compensation"), "summaryComponents");
        if (components.ValueKind != JsonValueKind.Array) return default;

        foreach (var component in components.EnumerateArray())
        {
            if (Text(component, "compensationType") != "Salary") continue;
            var period = Text(component, "interval")?.ToUpperInvariant() switch
            {
                "1 YEAR" => "year",
                "1 MONTH" => "month",
                "1 HOUR" => "hour",
                _ => null,
            };
            return (Whole(component, "minValue"), Whole(component, "maxValue"), Text(component, "currencyCode"), period);
        }
        return default;
    }
}
