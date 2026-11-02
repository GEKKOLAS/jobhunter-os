using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JobHunter.Domain.Matching;

/// <param name="Family">The catalog family, or null when the catalog doesn't place it in one.</param>
public sealed record RequirementInput(string Text, string Kind, string? SkillKey, string? SkillName, int? YearsMin, string? Family);

public sealed record SkillInput(string Key, string Name, int Level, decimal Years, string? Family);

public sealed record FitResult(int? Score, string? Band, string? MissingReason, IReadOnlyList<RequirementOutcome> Outcomes);

/// <summary>
/// Algorithm v1 (spec 03, Matching). Deterministic: the score comes from this code alone,
/// never from a model. It estimates how the profile covers what the posting asks; it does not
/// predict an outcome. A change to the rules is a new <see cref="Version"/>.
/// </summary>
public static partial class FitAlgorithm
{
    public const int Version = 1;
    public const int MustWeight = 3;
    public const int NiceWeight = 1;

    /// <summary>The level a skill needs for full coverage: 1 is "aware", 5 is "expert".</summary>
    public const int SolidLevel = 3;

    /// <summary>A strong fit is worth moving on while the posting is this fresh.</summary>
    public const int FreshDays = 14;

    public const string Strong = "strong";
    public const string Good = "good";
    public const string Partial = "partial";
    public const string Weak = "weak";

    public const string NoRequirements = "no_requirements";
    public const string NoScoredRequirements = "no_scored_requirements";
    public const string NoSkills = "no_skills";

    public static FitResult Assess(IReadOnlyList<RequirementInput> requirements, IReadOnlyList<SkillInput> skills)
    {
        var outcomes = requirements.Select(r => Outcome(r, skills)).ToList();
        if (requirements.Count == 0) return new FitResult(null, null, NoRequirements, outcomes);
        if (requirements.All(r => r.SkillKey is null)) return new FitResult(null, null, NoScoredRequirements, outcomes);

        // A score of 0 would say the profile lacks all of this, when it just hasn't been filled in.
        if (skills.Count == 0) return new FitResult(null, null, NoSkills, outcomes);

        var earned = 0m;
        var possible = 0m;
        foreach (var (requirement, outcome) in requirements.Zip(outcomes))
        {
            if (requirement.SkillKey is null) continue;
            var weight = requirement.Kind == RequirementVocabulary.Must ? MustWeight : NiceWeight;
            possible += weight;
            earned += weight * CoverageValue(outcome.Coverage);
        }

        var score = (int)Math.Round(100 * earned / possible, MidpointRounding.AwayFromZero);
        return new FitResult(score, BandOf(score), null, outcomes);
    }

    public static string BandOf(int score) => score switch
    {
        >= 75 => Strong,
        >= 55 => Good,
        >= 35 => Partial,
        _ => Weak,
    };

    private static decimal CoverageValue(string coverage) => coverage switch
    {
        "covered" => 1m,
        "partial" => 0.5m,
        _ => 0m,
    };

    private static RequirementOutcome Outcome(RequirementInput requirement, IReadOnlyList<SkillInput> skills)
    {
        var outcome = new RequirementOutcome
        {
            Text = requirement.Text,
            Kind = requirement.Kind,
            SkillKey = requirement.SkillKey,
            SkillName = requirement.SkillName,
            YearsMin = requirement.YearsMin,
            Coverage = "not_scored",
            Reason = "no_skill",
        };
        if (requirement.SkillKey is null) return outcome;

        var own = skills.FirstOrDefault(s => s.Key == requirement.SkillKey);
        if (own is not null)
        {
            outcome.YourLevel = own.Level;
            outcome.YourYears = (double)own.Years;

            var levelOk = own.Level >= SolidLevel;
            var yearsOk = requirement.YearsMin is null || own.Years >= requirement.YearsMin;
            (outcome.Coverage, outcome.Reason) = (levelOk, yearsOk) switch
            {
                (true, true) => ("covered", "meets"),
                (false, true) => ("partial", "under_level"),
                (true, false) => ("partial", "under_years"),
                _ => ("partial", "under_level_and_years"),
            };
            return outcome;
        }

        var related = requirement.Family is null ? null : skills.FirstOrDefault(s => s.Family == requirement.Family);
        if (related is not null)
        {
            (outcome.Coverage, outcome.Reason, outcome.RelatedSkill) = ("partial", "related", related.Name);
            return outcome;
        }

        (outcome.Coverage, outcome.Reason) = ("gap", "missing");
        return outcome;
    }

    /// <summary><c>high</c>, <c>normal</c> or <c>low</c>; null without a score, which says nothing either way.</summary>
    public static string? SuggestedPriority(string? band, string stage, DateTimeOffset createdAt, DateTimeOffset now)
    {
        if (band is null) return null;
        if (band == Weak) return "low";

        var stillPreparing = stage is "saved" or "preparing";
        return band == Strong && stillPreparing && now - createdAt < TimeSpan.FromDays(FreshDays) ? "high" : "normal";
    }

    /// <summary>Mismatches against the profile's preferences, only where both sides are stated.</summary>
    public static IReadOnlyList<FitNote> Notes(
        string title, string? postingLocation, string? postingWorkMode,
        string? seniority, IReadOnlyList<string> workModes, string location)
    {
        var notes = new List<FitNote>();

        if (postingWorkMode is not null && workModes.Count > 0 && !workModes.Contains(postingWorkMode))
        {
            notes.Add(new FitNote { Code = "work_mode", Posting = postingWorkMode, Profile = string.Join(", ", workModes) });
        }

        if (!string.IsNullOrWhiteSpace(postingLocation) && !string.IsNullOrWhiteSpace(location)
            && postingWorkMode != "remote" && !AnywhereWord().IsMatch(postingLocation)
            && NothingInCommon(Words(postingLocation), Words(location)))
        {
            notes.Add(new FitNote { Code = "location", Posting = postingLocation, Profile = location });
        }

        if (seniority is not null && SeniorityInTitle(title) is { } stated && stated != seniority)
        {
            notes.Add(new FitNote { Code = "seniority", Posting = stated, Profile = seniority });
        }

        return notes;
    }

    /// <summary>The level a title states, in the profile's vocabulary: "Staff" reads as lead, "Semi senior" as mid.</summary>
    public static string? SeniorityInTitle(string title)
    {
        var match = SeniorityWord().Match(title);
        if (!match.Success) return null;
        return match.Value.ToLowerInvariant() switch
        {
            "junior" or "jr" or "jr." or "entry level" or "entry-level" => "junior",
            "mid" or "mid-level" or "mid level" or "intermediate" or "semi senior" or "semi-senior" or "ssr" => "mid",
            "senior" or "sénior" or "sr" or "sr." => "senior",
            "lead" or "staff" or "líder" or "lider" => "lead",
            _ => "principal",
        };
    }

    /// <summary>Places with only generic words left have nothing to compare, which isn't a mismatch.</summary>
    private static bool NothingInCommon(HashSet<string> a, HashSet<string> b) => a.Count > 0 && b.Count > 0 && !a.Overlaps(b);

    /// <summary>Lowercase words without accents, so "Bogotá" meets "Bogota". Generic ones don't count as a shared place.</summary>
    private static HashSet<string> Words(string text)
    {
        var plain = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) plain.Append(char.ToLowerInvariant(c));
        }

        return WordPattern().Matches(plain.ToString())
            .Select(m => m.Value)
            .Where(w => w.Length >= 3 && !GenericPlaceWords.Contains(w))
            .ToHashSet();
    }

    private static readonly HashSet<string> GenericPlaceWords = ["area", "bay", "city", "the", "and", "region", "metro", "greater"];

    [GeneratedRegex(@"\b(remote|remoto|anywhere|worldwide|global)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AnywhereWord();

    [GeneratedRegex(@"\p{L}+")]
    private static partial Regex WordPattern();

    // Longest first, so "semi senior" isn't read as "senior".
    [GeneratedRegex(@"(?<![\p{L}\p{N}-])(semi[- ]senior|entry[- ]level|mid[- ]level|intermediate|principal|sénior|senior|junior|staff|lead|líder|lider|ssr|mid|sr\.?|jr\.?)(?![\p{L}\p{N}-])", RegexOptions.IgnoreCase)]
    private static partial Regex SeniorityWord();
}
