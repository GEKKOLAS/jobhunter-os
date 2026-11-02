using System.Globalization;
using System.Text.RegularExpressions;

namespace JobHunter.Infrastructure.Opportunities.Imports;

public sealed record StatedSalary(int? Min, int? Max, string Currency, string? Period, bool CurrencyAssumed);

/// <summary>
/// Facts a posting's text states outright, in English or Spanish. These are deliberately
/// conservative: an ambiguous mention yields nothing rather than a guess (spec 03, Opportunities).
/// </summary>
public static partial class TextFacts
{
    /// <summary>Only the top of a posting describes the job itself; later "remote" can be about a team or a perk.</summary>
    private const int HeaderLines = 15;

    private const int MaxSalaryLine = 1000;

    /// <summary>A line this short is a heading ("Base Salary"); a longer one is a sentence about something else.</summary>
    private const int MaxPayHeading = 60;

    /// <summary><c>remote|hybrid|onsite</c> when the header mentions exactly one of them.</summary>
    public static string? WorkMode(string text)
    {
        var header = Header(text);
        var found = new List<string>();
        if (Remote().IsMatch(header)) found.Add("remote");
        if (Hybrid().IsMatch(header)) found.Add("hybrid");
        if (OnSite().IsMatch(header)) found.Add("onsite");
        return found.Count == 1 ? found[0] : null;
    }

    /// <summary><c>full_time|part_time|contract|internship</c> when the header mentions exactly one of them.</summary>
    public static string? EmploymentType(string text)
    {
        var header = Header(text);
        var found = new List<string>();
        if (FullTime().IsMatch(header)) found.Add("full_time");
        if (PartTime().IsMatch(header)) found.Add("part_time");
        if (Contract().IsMatch(header)) found.Add("contract");
        if (Internship().IsMatch(header)) found.Add("internship");
        return found.Count == 1 ? found[0] : null;
    }

    /// <summary>
    /// A salary, only where the text says it is one: the line names pay, sits right under a short
    /// heading that does ("Base Salary"), or the amount states its period. "Raised $40M" and
    /// "$1B in revenue" never match. Needs a currency and an amount.
    /// </summary>
    public static StatedSalary? Salary(string text)
    {
        var previous = "";
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            var heading = previous;
            previous = line;

            // Pages are untrusted: a salary statement is short, and the pattern has a time limit.
            if (line.Length > MaxSalaryLine) continue;

            Match match;
            try
            {
                match = SalaryAmount().Match(line);
            }
            catch (RegexMatchTimeoutException)
            {
                continue;
            }
            if (!match.Success) continue;

            var period = PeriodOf(match.Groups["period"].Value);
            var saysPay = PayWord().IsMatch(line) || (heading.Length <= MaxPayHeading && PayWord().IsMatch(heading));
            if (period is null && !saysPay) continue;

            var min = Amount(match.Groups["min"].Value, match.Groups["k1"].Success || match.Groups["k2"].Success);
            var max = match.Groups["max"].Success ? Amount(match.Groups["max"].Value, match.Groups["k2"].Success) : null;
            if (min is null) continue;
            if (max is not null && max < min) continue;

            var symbol = match.Groups["cur"].Success ? match.Groups["cur"].Value : match.Groups["cur2"].Value;
            var (currency, assumed) = CurrencyOf(symbol);
            // "$160,000—$200,000 USD" or "$90,000 CAD": a code after the amount settles what "$" meant.
            if (assumed && TrailingCode().Match(line[(match.Index + match.Length)..]) is { Success: true } code)
            {
                (currency, assumed) = (code.Groups["code"].Value.ToUpperInvariant(), false);
            }
            // "The US base salary range is $85,000 - $135,000" states it too.
            if (assumed && (UnitedStates().IsMatch(line) || UnitedStates().IsMatch(heading))) assumed = false;
            return new StatedSalary(min, max ?? min, currency, period, assumed);
        }
        return null;
    }

    private static string Header(string text) => string.Join('\n', text.Split('\n').Take(HeaderLines));

    private static (string Code, bool Assumed) CurrencyOf(string symbol) => symbol.ToUpperInvariant() switch
    {
        "$" => ("USD", true), // "$" alone is also CAD, AUD, COP, MXN…: stated as an assumption
        "US$" => ("USD", false),
        "€" => ("EUR", false),
        "£" => ("GBP", false),
        var code => (code, false),
    };

    private static string? PeriodOf(string word) => word.ToLowerInvariant() switch
    {
        "year" or "yr" or "annum" or "annual" or "annually" or "año" or "ano" or "anual" => "year",
        "month" or "mo" or "monthly" or "mes" or "mensual" => "month",
        "hour" or "hr" or "hourly" or "hora" => "hour",
        _ => null,
    };

    /// <summary>"4,000", "4.000" and "4000" are four thousand; "4.5" with k is 4,500; anything out of int range is no salary.</summary>
    private static int? Amount(string digits, bool thousands)
    {
        decimal value;
        if (GroupedThousands().IsMatch(digits))
        {
            value = decimal.Parse(digits.Replace(",", "").Replace(".", ""), CultureInfo.InvariantCulture);
        }
        else if (!decimal.TryParse(digits.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            return null;
        }

        if (thousands) value *= 1000;
        return value is > 0 and < 100_000_000 ? (int)value : null;
    }

    [GeneratedRegex(@"\b(remote|fully remote|work from home|wfh|remoto|teletrabajo)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Remote();

    [GeneratedRegex(@"\b(hybrid|h[ií]brido)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Hybrid();

    [GeneratedRegex(@"\b(on[- ]?site|in[- ]office|presencial)\b", RegexOptions.IgnoreCase)]
    private static partial Regex OnSite();

    [GeneratedRegex(@"\b(full[- ]?time|tiempo completo)\b", RegexOptions.IgnoreCase)]
    private static partial Regex FullTime();

    [GeneratedRegex(@"\b(part[- ]?time|medio tiempo|tiempo parcial)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PartTime();

    [GeneratedRegex(@"\b(contractor|contract role|contract position|contrato por prestaci[oó]n)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Contract();

    [GeneratedRegex(@"\b(internship|intern|pasant[ií]a|pr[aá]cticas)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Internship();

    [GeneratedRegex(@"\b(salary|salaries|compensation|pay range|base pay|wage|ote|salario|sueldo|compensaci[oó]n|remuneraci[oó]n)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PayWord();

    [GeneratedRegex(@"^\d{1,3}(?:[.,]\d{3})+$")]
    private static partial Regex GroupedThousands();

    [GeneratedRegex(@"\b(?:US|U\.S\.|USA|United States)\b")]
    private static partial Regex UnitedStates();

    [GeneratedRegex(@"^\s*(?<code>" + Code + @")", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingCode();

    private const string Code = @"\b(?:USD|EUR|GBP|CAD|AUD|MXN|COP|BRL|CLP|ARS|PEN)\b";
    private const string Symbol = @"US\$|\$|€|£|" + Code;

    // A number that isn't the start of a bigger one or a magnitude: "$40M" and "$1B" aren't pay.
    private const string Number = @"\d[\d.,]*\d(?!\d|\s?(?:[mb]\b|million|billion|mill[oó]n|%))|\d(?![\d.,]*\d|\s?(?:[mb]\b|million|billion|mill[oó]n|%))";
    private const string To = @"\s*(?:-|–|—|to|a|hasta)\s*";

    // CUR 120,000[k] [- CUR 150,000[k]] [/ per a year], or 120,000 - 150,000 CUR [per year].
    [GeneratedRegex(
        @"(?:(?<cur>" + Symbol + @")\s?(?<min>" + Number + @")(?:\s?(?<k1>k\b))?(?:" + To + @"(?:" + Symbol + @")?\s?(?<max>" + Number + @")(?:\s?(?<k2>k\b))?)?" +
        @"|(?<min>" + Number + @")(?:\s?(?<k1>k\b))?(?:" + To + @"(?<max>" + Number + @")(?:\s?(?<k2>k\b))?)?\s?(?<cur2>" + Code + @"))" +
        @"(?:\s*(?:/|per|a|an|al|por|p/)\s*(?<period>year|yr|annum|month|mo|hour|hr|a[ñn]o|mes|hora)\b|\s+(?<period>annually|annual|monthly|hourly|anual|mensual)\b)?",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 250)]
    private static partial Regex SalaryAmount();
}
