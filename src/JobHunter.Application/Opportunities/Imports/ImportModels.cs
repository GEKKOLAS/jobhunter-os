using System.Security.Cryptography;
using System.Text;
using JobHunter.Application.Common;

namespace JobHunter.Application.Opportunities.Imports;

public sealed class ImportOptions
{
    public const string SectionName = "Imports";

    /// <summary>Development only: also fetch plain-http pages.</summary>
    public bool AllowHttp { get; init; }

    public int TimeoutSeconds { get; init; } = 10;
    public int MaxBytes { get; init; } = 2 * 1024 * 1024;
    public int MaxRedirects { get; init; } = 3;

    public string GreenhouseBaseUrl { get; init; } = "https://boards-api.greenhouse.io";
    public string LeverBaseUrl { get; init; } = "https://api.lever.co";
    public string AshbyBaseUrl { get; init; } = "https://api.ashbyhq.com";
}

/// <summary>An import that can't go ahead, always a 422 the user can act on (spec 03, Opportunities).</summary>
public sealed class ImportFailedException(string code, string message) : AppException(code, message)
{
    public static ImportFailedException NotAllowed(string siteName) => new(
        "import.not_allowed", $"{siteName}'s terms don't allow automated reading. Open the posting and paste its text instead.");

    public static ImportFailedException Unreachable(string why = "Couldn't reach that page.") => new("import.unreachable", why);

    public static ImportFailedException Unsupported(string why) => new("import.unsupported", why);
}

/// <summary>A job board's outside service failed for a reason the user can't fix: 502.</summary>
public static class SourceErrors
{
    public static UpstreamUnavailableException Unavailable(string provider) =>
        new("source.unavailable", $"{provider} isn't answering right now. Try again in a moment.");
}

/// <summary>What was read from a page or a paste. Nothing here is saved until the user saves it.</summary>
public sealed record ParsedPosting(
    string? Title, string? CompanyName, string? Location, string? WorkMode, string? EmploymentType,
    int? SalaryMin, int? SalaryMax, string? SalaryCurrency, string? SalaryPeriod, string Description,
    string ParsedFrom, IReadOnlyList<string> Warnings);

/// <summary>The API's answer to an import: a posting to check, then save through <c>POST /opportunities</c>.</summary>
public sealed record PostingPreview(
    string? Title, string? CompanyName, string? Location, string? WorkMode, string? EmploymentType,
    int? SalaryMin, int? SalaryMax, string? SalaryCurrency, string? SalaryPeriod, string? SourceUrl, string Description,
    string Source, string? SourceRef, string ParsedFrom, IReadOnlyList<string> Warnings, Guid? ExistingOpportunityId);

public static class ParsedFrom
{
    public const string JobBoard = "job_board";
    public const string StructuredData = "structured_data";
    public const string PageText = "page_text";
    public const string PastedText = "pasted_text";
}

/// <summary>A page fetched through the SSRF guard.</summary>
public sealed record FetchedPage(Uri FinalUrl, string MediaType, string Body);

/// <summary>Fetches one user-supplied page safely (spec 02 §5). Failures are <see cref="ImportFailedException"/>.</summary>
public interface IPostingFetcher
{
    Task<FetchedPage> FetchAsync(Uri url, CancellationToken cancellationToken);
}

/// <summary>Reads a posting out of HTML or plain text. Only what the source states; never invents.</summary>
public interface IPostingParser
{
    ParsedPosting ParseHtml(string html);
    ParsedPosting ParseText(string text);
}

/// <summary>One job as a company's public board lists it.</summary>
public sealed record BoardPosting(
    string Id, string Title, string? CompanyName, string? Location, string? Url, string? WorkMode, string? EmploymentType,
    int? SalaryMin, int? SalaryMax, string? SalaryCurrency, string? SalaryPeriod, string Description)
{
    /// <summary>
    /// A salary with a currency and a sane range. Saving from a board has no form to fix one
    /// in, so anything less is left out; the posting's text still has it.
    /// </summary>
    public BoardPosting WithUsableSalary()
    {
        var usable = SalaryCurrency is { Length: 3 }
            && (SalaryMin is not null || SalaryMax is not null)
            && (SalaryMin is null || SalaryMax is null || SalaryMin <= SalaryMax);
        return usable ? this : this with { SalaryMin = null, SalaryMax = null, SalaryCurrency = null, SalaryPeriod = null };
    }
}

/// <summary>A company's public job board at one provider. These are APIs companies publish for this purpose.</summary>
public interface IJobBoardClient
{
    /// <summary><c>greenhouse</c>, <c>lever</c> or <c>ashby</c>.</summary>
    string Provider { get; }

    /// <summary>The board's listed postings, or null when there is no such board. Throws when the provider fails.</summary>
    Task<IReadOnlyList<BoardPosting>?> ListAsync(string board, CancellationToken cancellationToken);
}

public static class PostingRefs
{
    /// <summary>A board posting's ref: stable across saves and link imports.</summary>
    public static string ForBoard(string board, string id) => $"{board}:{id}";

    /// <summary>
    /// A page's ref: the SHA-256 of its URL without fragment or <c>utm_*</c>, so the same
    /// page shared through two campaigns is still one opportunity.
    /// </summary>
    public static string ForUrl(Uri url)
    {
        var kept = url.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.StartsWith("utm_", StringComparison.OrdinalIgnoreCase));
        var query = string.Join('&', kept);
        var normal = $"{url.Scheme}://{url.Host.ToLowerInvariant()}{url.AbsolutePath.TrimEnd('/')}{(query.Length > 0 ? "?" + query : "")}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normal)));
    }
}
