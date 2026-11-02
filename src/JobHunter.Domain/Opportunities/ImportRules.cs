using System.Text.RegularExpressions;

namespace JobHunter.Domain.Opportunities;

/// <summary>A link to one posting on a job board whose public API can answer for it.</summary>
public sealed record JobBoardLink(string Provider, string Board, string PostingId);

/// <summary>What can be imported from where (spec 03, Opportunities). Pure, so it is unit-tested.</summary>
public static partial class ImportRules
{
    public const string Greenhouse = "greenhouse";
    public const string Lever = "lever";
    public const string Ashby = "ashby";

    public static readonly IReadOnlyList<string> BoardProviders = [Greenhouse, Lever, Ashby];

    /// <summary>A board token as the providers use them; also what keeps it safe in the API's URL.</summary>
    public static bool IsValidBoardToken(string? board) => board is not null && BoardToken().IsMatch(board);

    // Their terms forbid automated access, so the user pastes the text instead (PRD principle 4).
    private static readonly Dictionary<string, string> Forbidden = new()
    {
        ["linkedin"] = "LinkedIn",
        ["lnkd"] = "LinkedIn",
        ["indeed"] = "Indeed",
        ["glassdoor"] = "Glassdoor",
        ["ziprecruiter"] = "ZipRecruiter",
        ["monster"] = "Monster",
    };

    /// <summary>
    /// The site's name when its terms forbid automated reading; else null. Covers subdomains and
    /// country sites (de.indeed.com, indeed.co.uk, glassdoor.de), not lookalikes (monster.energy).
    /// </summary>
    public static string? ForbiddenSite(string host)
    {
        var match = ForbiddenHost().Match(host.ToLowerInvariant().TrimEnd('.'));
        return match.Success ? Forbidden[match.Groups["brand"].Value] : null;
    }

    /// <summary>Recognizes links to one posting on Greenhouse, Lever or Ashby; null otherwise.</summary>
    public static JobBoardLink? ParseJobBoardLink(Uri url)
    {
        var host = url.Host.ToLowerInvariant();
        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return host switch
        {
            "boards.greenhouse.io" or "job-boards.greenhouse.io"
                when parts.Length >= 3 && parts[1] == "jobs" && IsValidBoardToken(parts[0]) && parts[2].All(char.IsDigit)
                => new JobBoardLink(Greenhouse, parts[0], parts[2]),
            "jobs.lever.co" when parts.Length >= 2 && IsValidBoardToken(parts[0]) && IsValidBoardToken(parts[1])
                => new JobBoardLink(Lever, parts[0], parts[1]),
            "jobs.ashbyhq.com" when parts.Length >= 2 && IsValidBoardToken(parts[0]) && IsValidBoardToken(parts[1])
                => new JobBoardLink(Ashby, parts[0], parts[1]),
            _ => null,
        };
    }

    /// <summary>"contoso-inc" → "Contoso Inc": a label until the user gives a better one.</summary>
    public static string TidyBoardName(string board)
    {
        var words = board.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,100}$")]
    private static partial Regex BoardToken();

    [GeneratedRegex(@"(?:^|\.)(?<brand>linkedin|lnkd|indeed|glassdoor|ziprecruiter|monster)\.(?:com|[a-z]{2}|co\.[a-z]{2}|com\.[a-z]{2})$")]
    private static partial Regex ForbiddenHost();
}
