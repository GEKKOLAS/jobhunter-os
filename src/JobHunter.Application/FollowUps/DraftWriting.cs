using System.Text.RegularExpressions;
using JobHunter.Application.Profile.Contracts;
using JobHunter.Domain.FollowUps;

namespace JobHunter.Application.FollowUps;

/// <summary>Everything a draft may be written from, and nothing else (spec 02 §6).</summary>
public sealed record DraftBrief(
    string Kind, string? OpportunityTitle, string? CompanyName, string? ContactName, string? ContactRole,
    string SenderName, IReadOnlyList<string> Facts)
{
    /// <summary>All of it as one text, which is what the guardrails check an answer against.</summary>
    public string AllText => string.Join('\n', new[] { OpportunityTitle, CompanyName, ContactName, ContactRole, SenderName }
        .Where(s => !string.IsNullOrWhiteSpace(s)).Concat(Facts));
}

public sealed record WrittenDraft(string Subject, string Body);

/// <summary>Writes a draft from a brief. Its answer still goes through <see cref="DraftGuardrails"/>.</summary>
public interface IDraftWriter
{
    bool IsAvailable { get; }

    /// <exception cref="Common.AiUnavailableException">It failed, declined, or answered off-format.</exception>
    Task<WrittenDraft> WriteAsync(DraftBrief brief, CancellationToken ct);
}

/// <summary>Used when no key is configured: drafting by AI is simply off, and the user writes their own.</summary>
public sealed class UnavailableDraftWriter : IDraftWriter
{
    public bool IsAvailable => false;

    public Task<WrittenDraft> WriteAsync(DraftBrief brief, CancellationToken ct) =>
        throw new Common.AiUnavailableException("Drafting by AI isn't configured.");
}

/// <summary>
/// What a written draft may not do (spec 03, Follow-ups): say anything the input doesn't. It is
/// checked, never repaired: one violation and the user gets a blank draft instead. Words in an
/// email are the user's words once sent, so a confident invention is worse than nothing.
/// </summary>
public static partial class DraftGuardrails
{
    public const int MaxBody = 4000;

    /// <summary>Null when it passes; otherwise the first rule it broke, for the log (never shown as the user's fault).</summary>
    public static string? Check(DraftBrief brief, WrittenDraft draft, ISkillCatalog catalog, IReadOnlyCollection<string> otherCompanies)
    {
        if (string.IsNullOrWhiteSpace(draft.Subject) || string.IsNullOrWhiteSpace(draft.Body)) return "empty";
        if (draft.Subject.Length > FollowUpVocabulary.MaxSubject || draft.Body.Length > MaxBody) return "too_long";

        var input = brief.AllText;
        var output = $"{draft.Subject}\n{draft.Body}";

        var inputNumbers = Numbers().Matches(input).Select(m => m.Value).ToHashSet();
        if (Numbers().Matches(output).Any(m => !inputNumbers.Contains(m.Value))) return "invented_number";

        var inputDates = DateWord().Matches(input).Select(m => m.Value.ToLowerInvariant()).ToHashSet();
        if (DateWord().Matches(output).Any(m => !inputDates.Contains(m.Value.ToLowerInvariant()))) return "invented_date";

        var inputSkills = catalog.MentionsIn(input).Select(s => s.Key).ToHashSet();
        if (catalog.MentionsIn(output).Any(s => !inputSkills.Contains(s.Key))) return "invented_skill";

        foreach (var company in otherCompanies)
        {
            if (company.Length >= 2 && !ContainsWord(input, company) && ContainsWord(output, company)) return "other_company";
        }

        var inputPlaceholders = Placeholders().Matches(input).Select(m => Normalize(m.Value)).ToHashSet();
        var outputPlaceholders = Placeholders().Matches(output).Select(m => Normalize(m.Value)).ToHashSet();
        if (!inputPlaceholders.SetEquals(outputPlaceholders)) return "placeholders";

        return null;
    }

    private static string Normalize(string placeholder) => Regex.Replace(placeholder, @"\s+", "");

    private static bool ContainsWord(string text, string word)
    {
        try
        {
            return Regex.IsMatch(text, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(word)}(?![\p{{L}}\p{{N}}])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
        }
        catch (RegexMatchTimeoutException)
        {
            return true;
        }
    }

    // Digits, with their separators: "6", "2.5", "10,000". A number the input doesn't have is a claim.
    [GeneratedRegex(@"\d+(?:[.,]\d+)*")]
    private static partial Regex Numbers();

    // Months and weekdays, English and Spanish. "May" is left out: it's also "may I".
    [GeneratedRegex(@"\b(?:January|February|March|April|June|July|August|September|October|November|December|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday|enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|octubre|noviembre|diciembre|lunes|martes|miércoles|miercoles|jueves|viernes|sábado|sabado|domingo)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DateWord();

    [GeneratedRegex(@"\{\{\s*[\w.]+\s*\}\}")]
    private static partial Regex Placeholders();
}
