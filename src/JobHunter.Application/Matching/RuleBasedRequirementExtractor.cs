using System.Text.RegularExpressions;
using JobHunter.Application.Profile.Contracts;
using JobHunter.Domain.Matching;

namespace JobHunter.Application.Matching;

/// <summary>
/// Reads requirements with no model: section headings, inline signals and the skill catalog,
/// in English and Spanish (spec 03, Matching). It only drafts: every row waits for the user's
/// confirmation, so a missed nuance costs a click and not a wrong score.
/// </summary>
public sealed partial class RuleBasedRequirementExtractor(ISkillCatalog catalog) : IRequirementExtractor
{
    /// <summary>Longer than this is prose or a hostile page, not one requirement.</summary>
    private const int MaxLine = 2000;

    private const int MinPlainRequirement = 8;

    /// <summary>A line this long without a bullet or a signal is a paragraph.</summary>
    private const int ParagraphWords = 12;

    private const string SoftSkill = "soft";

    private enum Section { NotStated, Must, Nice, Other }

    public string Origin => RequirementVocabulary.OriginRules;

    public Task<IReadOnlyList<ExtractedRequirement>> ExtractAsync(string postingText, CancellationToken ct) =>
        Task.FromResult(Extract(postingText));

    public IReadOnlyList<ExtractedRequirement> Extract(string posting)
    {
        var rows = new List<ExtractedRequirement>();
        var section = Section.NotStated;

        foreach (var raw in posting.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.Length > MaxLine) continue;

            // Lever's boards put this in a bullet; it comes after the requirements and asks nothing of you.
            if (EqualOpportunity().IsMatch(line))
            {
                section = Section.Other;
                continue;
            }

            var isBullet = Bullet().IsMatch(line);
            if (isBullet) line = Bullet().Replace(line, "");

            if (!isBullet)
            {
                if (HeadingOf(line) is { } heading)
                {
                    section = heading;
                    continue;
                }

                // A paragraph after a list is the posting moving on: "Our salary ranges are..." asks nothing of you.
                if (section is Section.Must or Section.Nice && IsParagraph(line))
                {
                    section = Section.Other;
                    continue;
                }
            }
            if (section == Section.Other) continue;

            AddLine(line, isBullet, section, rows);
        }

        // A posting with no headings or signals still names skills: draft them as the weaker kind.
        // Soft skills are left out, because in passing prose they're about the company.
        if (rows.Count == 0)
        {
            foreach (var skill in catalog.MentionsIn(posting).Where(s => s.Category != SoftSkill))
            {
                rows.Add(new ExtractedRequirement(skill.Name, RequirementVocabulary.Nice, skill.Key, null));
            }
        }

        return Merge(rows);
    }

    private void AddLine(string line, bool isBullet, Section section, List<ExtractedRequirement> rows)
    {
        var clauses = ClauseBreak().Split(line).Select(c => c.Trim()).Where(c => c.Length > 0).ToList();
        var namedSkill = false;

        foreach (var clause in clauses)
        {
            var skills = catalog.MentionsIn(clause);
            var kind = KindOf(clause, section);
            if (skills.Count == 0 || kind is null) continue;
            namedSkill = true;

            // "Go or Python" can be met by either, so needing both would overstate it.
            if (skills.Count > 1 && Alternative().IsMatch(clause)) kind = RequirementVocabulary.Nice;

            var years = YearsIn(clause);
            foreach (var skill in skills)
            {
                // One skill reads best as the posting's sentence; several would repeat it, so each gets its name.
                var text = skills.Count == 1 ? Fit(clause) : years is null ? skill.Name : $"{skill.Name}, {years}+ years";
                rows.Add(new ExtractedRequirement(text, kind, skill.Key, years));
            }
        }

        // A bullet naming no skill is one requirement, however many sentences explain it. Prose isn't one at all.
        if (namedSkill || !isBullet || clauses.Count == 0 || clauses[0].Length < MinPlainRequirement) return;
        if (KindOf(line, section) is { } plainKind) rows.Add(new ExtractedRequirement(Fit(clauses[0]), plainKind, null, YearsIn(line)));
    }

    private static bool IsParagraph(string line) =>
        line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= ParagraphWords
        && !NiceSignal().IsMatch(line) && !MustSignal().IsMatch(line) && !YearsPattern().IsMatch(line);

    private static string? KindOf(string clause, Section section)
    {
        if (NiceSignal().IsMatch(clause)) return RequirementVocabulary.Nice;
        if (MustSignal().IsMatch(clause) || YearsPattern().IsMatch(clause)) return RequirementVocabulary.Must;
        return section switch
        {
            Section.Must => RequirementVocabulary.Must,
            Section.Nice => RequirementVocabulary.Nice,
            _ => null,
        };
    }

    /// <summary>"3-5 years" asks for 3. Out-of-range numbers aren't years of experience.</summary>
    private static int? YearsIn(string clause)
    {
        var match = YearsPattern().Match(clause);
        return match.Success && int.TryParse(match.Groups["years"].Value, out var years) && years <= RequirementVocabulary.MaxYears ? years : null;
    }

    private Section? HeadingOf(string line)
    {
        var plain = line.TrimEnd(':', ' ', '.').Trim();
        if (plain.Length == 0 || plain.Length > 70) return null;

        // "Required: C#, .NET" and "Experience with Python" are requirements, not headings.
        if (catalog.MentionsIn(plain).Count > 0) return null;

        if (NiceHeading().IsMatch(plain)) return Section.Nice;
        if (OtherHeading().IsMatch(plain)) return Section.Other;
        if (MustHeading().IsMatch(plain)) return Section.Must;

        // A heading we don't know ("The Team:", "ENGINEERING CULTURE") still ends the section before it.
        var allCaps = plain.Any(char.IsLetter) && plain == plain.ToUpperInvariant();
        return allCaps || line.EndsWith(':') || (plain.Split(' ').Length <= 6 && TitleCase().IsMatch(plain)) ? Section.Other : null;
    }

    /// <summary>One row per skill or text: the stronger kind and the higher years win, and it keeps its first position.</summary>
    private static List<ExtractedRequirement> Merge(List<ExtractedRequirement> rows)
    {
        var merged = new List<ExtractedRequirement>();
        foreach (var row in rows)
        {
            var index = merged.FindIndex(m => row.SkillKey is null
                ? m.SkillKey is null && string.Equals(m.Text, row.Text, StringComparison.OrdinalIgnoreCase)
                : m.SkillKey == row.SkillKey);
            if (index < 0)
            {
                merged.Add(row);
                continue;
            }

            var first = merged[index];
            merged[index] = first with
            {
                Kind = first.Kind == RequirementVocabulary.Must || row.Kind == RequirementVocabulary.Must ? RequirementVocabulary.Must : RequirementVocabulary.Nice,
                YearsMin = Math.Max(first.YearsMin ?? -1, row.YearsMin ?? -1) is var years and >= 0 ? years : null,
            };
        }
        return merged.Take(RequirementVocabulary.MaxPerOpportunity).ToList();
    }

    private static string Fit(string text) =>
        text.Length <= RequirementVocabulary.MaxText ? text : text[..RequirementVocabulary.MaxText].TrimEnd();

    // Repeated, because a real page's nested list reads "- - Full-time".
    [GeneratedRegex(@"^(?:(?:[-–—*•·‣▪◦]|\d{1,2}[.)])\s+)+")]
    private static partial Regex Bullet();

    // Sentences and semicolons, without breaking ".NET", "Node.js", "e.g. Python" or "B.S. in".
    [GeneratedRegex(@"(?<=[\p{Ll}\d)]{2}[.!?])\s+(?=\p{Lu})|;\s*")]
    private static partial Regex ClauseBreak();

    [GeneratedRegex(@"\s(?:or|o|and/or|y/o)\s|/", RegexOptions.IgnoreCase)]
    private static partial Regex Alternative();

    [GeneratedRegex(@"\b(?:(?:minimum|at least|al menos|m[ií]nimo(?: de)?)\s+)?(?<years>\d{1,2})\s*(?:\+|\s*[-–]\s*\d{1,2}|\s*or more|\s*o m[aá]s)?\s*(?:years?|yrs?|a[ñn]os)\b", RegexOptions.IgnoreCase)]
    private static partial Regex YearsPattern();

    [GeneratedRegex(@"\b(?:nice[- ]to[- ]have|a plus|is a bonus|bonus|preferred|desirable|deseable|valorable|se valora|un plus)\b", RegexOptions.IgnoreCase)]
    private static partial Regex NiceSignal();

    [GeneratedRegex(@"\b(?:required|must[- ]have|must have|mandatory|obligatorio|indispensable|imprescindible|requerido)\b|^(?:requirements?|skills)\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex MustSignal();

    [GeneratedRegex(@"\bequal (?:employment )?opportunity\b|\bigualdad de oportunidades\b", RegexOptions.IgnoreCase)]
    private static partial Regex EqualOpportunity();

    private const string Lead = @"^(?:the\s+|our\s+|key\s+|your\s+)?";

    [GeneratedRegex(Lead + @"(?:nice[- ]to[- ]haves?|bonus(?: points)?|preferred|desired|desirable|pluses|plus|good to have|would be (?:great|a plus|nice)|extra credit|deseables?|valorable|se valorar[aá])\b.{0,40}$", RegexOptions.IgnoreCase)]
    private static partial Regex NiceHeading();

    // Phrases may run on ("Requirements for this role"); single everyday words have to stand alone.
    [GeneratedRegex(Lead + @"(?:(?:(?:(?:minimum|basic|core|required|technical|main)\s+)?(?:requirements?|qualifications?|skills(?:\s+and\s+experience)?)|what you(?:['’]ll| will| should)? (?:need|have|bring)|what we(?:['’]re| are)? (?:need|looking for|look for|expect|want)|(?:we['’]re|we are) looking for|who you are|you(?:['’]ll| will| should)? (?:have|bring|need)|requisitos|requerimientos|lo que buscamos|qu[eé] buscamos|perfil)\b.{0,40}|about you|must[- ]haves?|required|experience|profile|imprescindible)$", RegexOptions.IgnoreCase)]
    private static partial Regex MustHeading();

    [GeneratedRegex(Lead + @"(?:responsibilities|what you(?:['’]ll| will)(?: be)? do(?:ing)?|role|about (?:us|the role|the company|the team)|who we are|benefits|perks|what we offer|we offer|compensation|base salary|salary|how to apply|responsabilidades|funciones|beneficios|ofrecemos|sobre nosotros|qui[eé]nes somos)\b.{0,40}$", RegexOptions.IgnoreCase)]
    private static partial Regex OtherHeading();

    // Every significant word capitalized: "The Engineering Team", not "Strong written skills".
    [GeneratedRegex(@"^\p{Lu}[\p{L}'’-]*(?:\s+(?:\p{Lu}[\p{L}'’-]*|of|the|and|to|in|for|at|&))*$")]
    private static partial Regex TitleCase();
}
