using FluentValidation;
using JobHunter.Application.Profile.Contracts;
using JobHunter.Domain.Matching;

namespace JobHunter.Application.Matching;

/// <param name="SkillName">The catalog's name for the skill, or its key when the catalog doesn't know it.</param>
public sealed record RequirementDto(
    Guid Id, string Text, string Kind, string? SkillKey, string? SkillName, int? YearsMin, string Origin, bool Confirmed)
{
    public static RequirementDto From(OpportunityRequirement r, ISkillCatalog catalog) => new(
        r.Id, r.Text, r.Kind, r.SkillKey, r.SkillKey is null ? null : catalog.Find(r.SkillKey)?.Name ?? r.SkillKey,
        r.YearsMin, r.Origin, r.Confirmed);
}

/// <param name="Extractor">What "read the requirements" runs here: <c>rules</c> or <c>ai</c>.</param>
public sealed record RequirementsDto(IReadOnlyList<RequirementDto> Items, string Extractor);

/// <param name="FellBackToRules">The AI was meant to read it and couldn't, so the rules did.</param>
public sealed record ExtractionDto(IReadOnlyList<RequirementDto> Items, string Extractor, bool FellBackToRules);

/// <param name="MissingReason">Why there's no score: <c>no_requirements</c>, <c>no_scored_requirements</c> or <c>no_skills</c>.</param>
/// <param name="SuggestedPriority">What the fit suggests, whether or not it applied: a priority the user set stays.</param>
public sealed record FitDto(
    int? Score, string? Band, string? MissingReason, IReadOnlyList<RequirementOutcome> Outcomes, IReadOnlyList<FitNote> Notes,
    string? SuggestedPriority, int AlgorithmVersion, DateTimeOffset? ComputedAt);

public sealed record RequirementInputItem(string Text, string Kind, string? Skill, int? YearsMin);

public sealed record ReplaceRequirements(IReadOnlyList<RequirementInputItem> Items);

public sealed class ReplaceRequirementsValidator : AbstractValidator<ReplaceRequirements>
{
    public ReplaceRequirementsValidator(ISkillCatalog catalog)
    {
        RuleFor(x => x.Items).NotNull()
            .Must(items => items is null || items.Count <= RequirementVocabulary.MaxPerOpportunity)
            .WithMessage($"Keep it to {RequirementVocabulary.MaxPerOpportunity} requirements.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Text).NotEmpty().WithMessage("Say what the posting asks for.")
                .MaximumLength(RequirementVocabulary.MaxText);
            item.RuleFor(i => i.Kind).Must(RequirementVocabulary.Kinds.Contains)
                .WithMessage($"Kind must be one of: {string.Join(", ", RequirementVocabulary.Kinds)}.");
            item.RuleFor(i => i.Skill).MaximumLength(RequirementVocabulary.MaxSkillName);
            item.RuleFor(i => i.YearsMin).InclusiveBetween(0, RequirementVocabulary.MaxYears);
        });
        // The same skill twice would count twice in the score.
        RuleFor(x => x.Items)
            .Must(items => items is null || HasNoRepeatedSkill(items, catalog))
            .WithMessage("The same skill is listed twice. Keep one row for it.");
    }

    private static bool HasNoRepeatedSkill(IReadOnlyList<RequirementInputItem> items, ISkillCatalog catalog)
    {
        var keys = items
            .Where(i => !string.IsNullOrWhiteSpace(i.Skill))
            .Select(i => catalog.KeyFor(i.Skill!))
            .Where(key => key.Length > 0)
            .ToList();
        return keys.Count == keys.Distinct().Count();
    }
}

/// <summary>Audit action codes of the Matching module.</summary>
public static class MatchingAudit
{
    public const string Extracted = "requirements.extracted";
    public const string Updated = "requirements.updated";
}
