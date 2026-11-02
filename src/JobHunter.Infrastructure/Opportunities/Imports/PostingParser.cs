using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using JobHunter.Application.Opportunities.Imports;

namespace JobHunter.Infrastructure.Opportunities.Imports;

/// <summary>
/// Reads a posting from a page or a paste: <c>JobPosting</c> JSON-LD when a page has it, plain
/// heuristics otherwise. Only what's stated; the user checks every field before saving.
/// </summary>
public sealed partial class PostingParser : IPostingParser
{
    private const int MaxTitle = 160;
    private const int MaxCompany = 120;
    private const int MaxLocation = 120;
    private const int MaxDescription = 50_000;

    /// <summary>Less text than this probably means the page renders itself with JavaScript.</summary>
    private const int LittleText = 200;

    private const string LittleTextWarning =
        "This page shows very little text without JavaScript. If the fields look empty, paste the posting instead.";

    private const string DollarWarning = "The posting says \"$\" without naming a currency. USD is assumed; check it.";

    private static readonly HashSet<string> GenericTitleParts = new(StringComparer.OrdinalIgnoreCase)
    {
        "careers", "career", "jobs", "job", "job application", "job details", "apply", "home", "empleos", "empleo", "vacantes",
    };

    public ParsedPosting ParseHtml(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        return ReadJsonLd(document) ?? ReadPage(document);
    }

    public ParsedPosting ParseText(string text)
    {
        var description = Limit(text.Replace("\r\n", "\n").Trim(), MaxDescription)!;
        var lines = description.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

        var title = Labelled(lines, TitleLabel());
        var company = Labelled(lines, CompanyLabel());
        var location = Labelled(lines, LocationLabel());

        if (title is null && lines.Count > 0 && LooksLikeTitle(lines[0]))
        {
            (title, var fromTitle) = SplitTitle(lines[0]);
            company ??= fromTitle;
        }

        return WithFacts(title, company, location, workMode: null, employmentType: null, description, ParsedFrom.PastedText, warnLittleText: false);
    }

    // ---- Page heuristics ----

    private static ParsedPosting ReadPage(IDocument document)
    {
        var pageTitle = Meta(document, "og:title") ?? document.Title;
        var (fromPage, companyFromTitle) = SplitTitle(pageTitle ?? "");
        var heading = document.QuerySelector("h1")?.TextContent;
        var title = !string.IsNullOrWhiteSpace(heading) && LooksLikeTitle(heading.Trim()) ? Clean(heading) : fromPage;
        var company = Meta(document, "og:site_name") ?? companyFromTitle;

        var main = document.QuerySelector("main, article, [role=main]") ?? document.Body;
        var description = main is null ? "" : HtmlText.From(main);

        return WithFacts(title, company, null, null, null, Limit(description, MaxDescription)!, ParsedFrom.PageText, warnLittleText: true);
    }

    private static string? Meta(IDocument document, string property) =>
        Clean(document.QuerySelector($"meta[property='{property}']")?.GetAttribute("content"));

    /// <summary>"Backend Engineer at Contoso", "Backend Engineer - Contoso | Careers" → the role and the company.</summary>
    private static (string? Title, string? Company) SplitTitle(string raw)
    {
        var text = Clean(raw);
        if (text is null) return (null, null);

        var at = RoleAtCompany().Match(text);
        if (at.Success) return (Clean(at.Groups["role"].Value), Clean(at.Groups["company"].Value));

        var parts = TitleSeparator().Split(text)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0 && !GenericTitleParts.Contains(p))
            .ToList();
        return parts.Count switch
        {
            0 => (null, null),
            1 => (parts[0], null),
            _ => (parts[0], parts[1]),
        };
    }

    private static bool LooksLikeTitle(string line) =>
        line.Length <= MaxTitle && !line.EndsWith('.') && line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 14;

    private static string? Labelled(List<string> lines, Regex label)
    {
        foreach (var line in lines.Take(25))
        {
            var match = label.Match(line);
            if (match.Success) return Clean(match.Groups["value"].Value);
        }
        return null;
    }

    private static ParsedPosting WithFacts(
        string? title, string? company, string? location, string? workMode, string? employmentType,
        string description, string parsedFrom, bool warnLittleText,
        int? salaryMin = null, int? salaryMax = null, string? salaryCurrency = null, string? salaryPeriod = null)
    {
        var warnings = new List<string>();
        if (salaryMin is null && salaryMax is null && TextFacts.Salary(description) is { } salary)
        {
            (salaryMin, salaryMax, salaryCurrency, salaryPeriod) = (salary.Min, salary.Max, salary.Currency, salary.Period);
            if (salary.CurrencyAssumed) warnings.Add(DollarWarning);
        }
        if (warnLittleText && description.Length < LittleText) warnings.Add(LittleTextWarning);

        return new ParsedPosting(
            Limit(title, MaxTitle), Limit(company, MaxCompany), Limit(location, MaxLocation),
            workMode ?? TextFacts.WorkMode(description), employmentType ?? TextFacts.EmploymentType(description),
            salaryMin, salaryMax, salaryCurrency, salaryPeriod, description, parsedFrom, warnings);
    }

    // ---- JSON-LD ----

    private static ParsedPosting? ReadJsonLd(IDocument document)
    {
        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            try
            {
                using var json = JsonDocument.Parse(script.TextContent, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });
                if (FindJobPosting(json.RootElement) is { } posting) return FromJobPosting(posting);
            }
            catch (JsonException)
            {
                // Broken structured data is common; the page may still have good text.
            }
        }
        return null;
    }

    private static JsonElement? FindJobPosting(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (FindJobPosting(item) is { } found) return found;
            }
            return null;
        }
        if (element.ValueKind != JsonValueKind.Object) return null;

        if (element.TryGetProperty("@type", out var type) && IsJobPostingType(type)) return element;
        return element.TryGetProperty("@graph", out var graph) ? FindJobPosting(graph) : null;
    }

    private static bool IsJobPostingType(JsonElement type) => type.ValueKind switch
    {
        JsonValueKind.String => string.Equals(type.GetString(), "JobPosting", StringComparison.OrdinalIgnoreCase),
        JsonValueKind.Array => type.EnumerateArray().Any(IsJobPostingType),
        _ => false,
    };

    private static ParsedPosting FromJobPosting(JsonElement posting)
    {
        var description = HtmlText.From(WebUtility.HtmlDecode(Text(posting, "description") ?? ""));
        var remote = string.Equals(Text(posting, "jobLocationType"), "TELECOMMUTE", StringComparison.OrdinalIgnoreCase);
        var location = LocationOf(posting) ?? (remote ? "Remote" : null);
        var company = posting.TryGetProperty("hiringOrganization", out var org)
            ? (org.ValueKind == JsonValueKind.String ? org.GetString() : Text(org, "name"))
            : null;
        var (min, max, currency, period) = SalaryOf(posting);

        return WithFacts(
            Clean(WebUtility.HtmlDecode(Text(posting, "title") ?? "")), Clean(company), Clean(location),
            remote ? "remote" : null, EmploymentOf(posting),
            Limit(description, MaxDescription)!, ParsedFrom.StructuredData, warnLittleText: false,
            min, max, currency, period);
    }

    private static string? LocationOf(JsonElement posting)
    {
        if (!posting.TryGetProperty("jobLocation", out var location)) return null;
        var first = location.ValueKind == JsonValueKind.Array ? location.EnumerateArray().FirstOrDefault() : location;
        if (first.ValueKind != JsonValueKind.Object || !first.TryGetProperty("address", out var address)) return null;
        if (address.ValueKind == JsonValueKind.String) return address.GetString();
        if (address.ValueKind != JsonValueKind.Object) return null;

        var country = address.TryGetProperty("addressCountry", out var c)
            ? (c.ValueKind == JsonValueKind.Object ? Text(c, "name") : c.ValueKind == JsonValueKind.String ? c.GetString() : null)
            : null;
        var parts = new[] { Text(address, "addressLocality"), Text(address, "addressRegion"), country }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        var joined = string.Join(", ", parts);
        return joined.Length > 0 ? joined : null;
    }

    private static string? EmploymentOf(JsonElement posting)
    {
        if (!posting.TryGetProperty("employmentType", out var type)) return null;
        var value = type.ValueKind == JsonValueKind.Array ? type.EnumerateArray().FirstOrDefault() : type;
        if (value.ValueKind != JsonValueKind.String) return null;

        return value.GetString()?.ToUpperInvariant().Replace('-', '_').Replace(' ', '_') switch
        {
            "FULL_TIME" or "FULLTIME" => "full_time",
            "PART_TIME" or "PARTTIME" => "part_time",
            "CONTRACTOR" or "CONTRACT" or "TEMPORARY" => "contract",
            "INTERN" or "INTERNSHIP" => "internship",
            _ => null,
        };
    }

    private static (int? Min, int? Max, string? Currency, string? Period) SalaryOf(JsonElement posting)
    {
        if (!posting.TryGetProperty("baseSalary", out var salary) || salary.ValueKind != JsonValueKind.Object) return default;

        var currency = Text(salary, "currency")?.ToUpperInvariant();
        if (currency is not { Length: 3 } || !salary.TryGetProperty("value", out var value)) return default;

        int? min, max;
        string? unit = null;
        if (value.ValueKind == JsonValueKind.Object)
        {
            min = Whole(value, "minValue") ?? Whole(value, "value");
            max = Whole(value, "maxValue") ?? Whole(value, "value");
            unit = Text(value, "unitText");
        }
        else
        {
            min = max = WholeOf(value);
        }

        if (min is null && max is null) return default;
        if (min is not null && max is not null && max < min) return default;
        var period = unit?.ToUpperInvariant() switch { "YEAR" => "year", "MONTH" => "month", "HOUR" => "hour", _ => null };
        return (min, max, currency, period);
    }

    private static int? Whole(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) ? WholeOf(value) : null;

    private static int? WholeOf(JsonElement value)
    {
        decimal? number = value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var n) => n,
            JsonValueKind.String when decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var s) => s,
            _ => null,
        };
        return number is > 0 and < 100_000_000 ? (int)number.Value : null;
    }

    private static string? Text(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    // ---- Small helpers ----

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Whitespace().Replace(value, " ").Trim();
    }

    private static string? Limit(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max].TrimEnd();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\s+[|–—·•]\s+|\s+-\s+")]
    private static partial Regex TitleSeparator();

    [GeneratedRegex(@"^(?<role>.{2,120}?)\s+(?:at|@|en)\s+(?<company>[^|–—·•]{2,80}?)(?:\s+[|–—·•-]\s+.*)?$")]
    private static partial Regex RoleAtCompany();

    [GeneratedRegex(@"^(?:job title|title|role|position|puesto|cargo|vacante)\s*[:：]\s*(?<value>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex TitleLabel();

    [GeneratedRegex(@"^(?:company(?: name)?|employer|empresa|compa[ñn][ií]a)\s*[:：]\s*(?<value>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex CompanyLabel();

    [GeneratedRegex(@"^(?:location|based in|ubicaci[oó]n|lugar|ciudad)\s*[:：]\s*(?<value>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex LocationLabel();
}
