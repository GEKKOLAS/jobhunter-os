using FluentValidation;
using JobHunter.Domain.Opportunities;

namespace JobHunter.Application.Opportunities;

/// <summary>What a user can set on an opportunity. The company is an id, or a name matched or created.</summary>
/// <param name="Stage">Only on create: where it starts (an open stage, default <c>saved</c>).</param>
/// <param name="Source">Only on create: where it came from (default <c>manual</c>); imports pass their preview's value.</param>
/// <param name="SourceRef">Only on create: saving a ref that's already saved updates it instead of duplicating.</param>
public sealed record OpportunityInput(
    string Title, Guid? CompanyId, string? CompanyName, string? Location, string? WorkMode, string? EmploymentType,
    int? SalaryMin, int? SalaryMax, string? SalaryCurrency, string? SalaryPeriod, string? SourceUrl,
    string? Description, string? Priority, string? Stage = null, string? Source = null, string? SourceRef = null);

public sealed class OpportunityInputValidator : AbstractValidator<OpportunityInput>
{
    public OpportunityInputValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(160);
        RuleFor(x => x.CompanyName).NotEmpty().When(x => x.CompanyId is null).WithMessage("Name the company.");
        RuleFor(x => x.CompanyName).MaximumLength(120);
        RuleFor(x => x.Location).MaximumLength(120);
        RuleFor(x => x.WorkMode).Must(v => v is null || OpportunityVocabulary.WorkModes.Contains(v))
            .WithMessage($"Work mode must be one of: {string.Join(", ", OpportunityVocabulary.WorkModes)}.");
        RuleFor(x => x.EmploymentType).Must(v => v is null || OpportunityVocabulary.EmploymentTypes.Contains(v))
            .WithMessage($"Employment type must be one of: {string.Join(", ", OpportunityVocabulary.EmploymentTypes)}.");
        RuleFor(x => x.SalaryMin).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SalaryMax).GreaterThanOrEqualTo(x => x.SalaryMin ?? 0).When(x => x.SalaryMax is not null)
            .WithMessage("The maximum can't be below the minimum.");
        RuleFor(x => x.SalaryCurrency).NotEmpty().When(x => x.SalaryMin is not null || x.SalaryMax is not null)
            .WithMessage("Add the currency the posting states.");
        RuleFor(x => x.SalaryCurrency).Matches("^[A-Za-z]{3}$").When(x => x.SalaryCurrency is not null)
            .WithMessage("Use a three-letter currency code, like USD.");
        RuleFor(x => x.SalaryPeriod).Must(v => v is null || OpportunityVocabulary.SalaryPeriods.Contains(v))
            .WithMessage($"Salary period must be one of: {string.Join(", ", OpportunityVocabulary.SalaryPeriods)}.");
        RuleFor(x => x.SourceUrl).MaximumLength(2048)
            .Must(u => u is null || (Uri.TryCreate(u, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)))
            .WithMessage("Use the posting's web address (http or https).");
        RuleFor(x => x.Description).MaximumLength(50_000);
        RuleFor(x => x.Priority).Must(v => v is null || OpportunityVocabulary.Priorities.Contains(v))
            .WithMessage($"Priority must be one of: {string.Join(", ", OpportunityVocabulary.Priorities)}.");
        RuleFor(x => x.Stage).Must(v => v is null || Stages.IsOpen(v))
            .WithMessage($"A new opportunity starts at an open stage: {string.Join(", ", Stages.Open)}.");
        RuleFor(x => x.Source).Must(v => v is null || OpportunityVocabulary.Sources.Contains(v))
            .WithMessage($"Source must be one of: {string.Join(", ", OpportunityVocabulary.Sources)}.");
        RuleFor(x => x.SourceRef).MaximumLength(200);
    }
}

public sealed record MoveStageCommand(string Stage, string? Note);

public sealed class MoveStageValidator : AbstractValidator<MoveStageCommand>
{
    public MoveStageValidator()
    {
        RuleFor(x => x.Stage).Must(Stages.All.Contains).WithMessage($"Stage must be one of: {string.Join(", ", Stages.All)}.");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed record CompanyInput(string Name, string? Website, string? Notes);

public sealed class CompanyInputValidator : AbstractValidator<CompanyInput>
{
    public CompanyInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Website).MaximumLength(2048)
            .Must(w => w is null || Company.DomainOf(w) is not null).WithMessage("That doesn't look like a web address.");
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed record OpportunityListQuery(string? Stage, string? Priority, string? Q, bool Archived, int Page, int PageSize);
