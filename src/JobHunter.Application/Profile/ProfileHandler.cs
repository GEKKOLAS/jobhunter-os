using FluentValidation;
using JobHunter.Application.Common;
using JobHunter.Domain.Profile;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.Profile;

public sealed record UpdateProfileCommand(
    string? Headline, string? Summary, string? Location, IReadOnlyList<string>? TargetRoles, string? Seniority,
    IReadOnlyList<string>? WorkModes, IReadOnlyList<LanguageDto>? Languages, uint Version);

public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.Headline).MaximumLength(120);
        RuleFor(x => x.Summary).MaximumLength(2000);
        RuleFor(x => x.Location).MaximumLength(120);
        RuleFor(x => x.TargetRoles).Must(r => r is null || r.Count <= 10).WithMessage("At most 10 target roles.");
        RuleForEach(x => x.TargetRoles).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Seniority).Must(s => s is null || ProfileVocabulary.Seniorities.Contains(s))
            .WithMessage($"Seniority must be one of: {string.Join(", ", ProfileVocabulary.Seniorities)}.");
        RuleForEach(x => x.WorkModes).Must(ProfileVocabulary.WorkModes.Contains)
            .WithMessage($"Work modes are: {string.Join(", ", ProfileVocabulary.WorkModes)}.");
        RuleFor(x => x.Languages).Must(l => l is null || l.Count <= 10).WithMessage("At most 10 languages.");
        RuleForEach(x => x.Languages).ChildRules(language =>
        {
            language.RuleFor(l => l.Name).NotEmpty().MaximumLength(40);
            language.RuleFor(l => l.Level).Must(ProfileVocabulary.LanguageLevels.Contains)
                .WithMessage($"Level must be one of: {string.Join(", ", ProfileVocabulary.LanguageLevels)}.");
        });
    }
}

public sealed class ProfileHandler(
    IProfileDbContext db, ICurrentUser currentUser, IAuditWriter audit, IValidator<UpdateProfileCommand> validator)
{
    public async Task<ProfileDto> GetAsync(CancellationToken ct)
    {
        var profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == currentUser.UserId, ct);
        return profile is null ? ProfileDto.Empty : ProfileDto.From(profile);
    }

    /// <summary>
    /// The first save creates the row (from version 0); later saves must carry the version
    /// they read, or they fail with 409 instead of overwriting someone else's change.
    /// </summary>
    public async Task<ProfileDto> UpdateAsync(UpdateProfileCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);

        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == currentUser.UserId, ct);
        if (profile is null)
        {
            if (command.Version != 0) throw Stale();
            profile = new CandidateProfile { UserId = currentUser.UserId };
            db.Profiles.Add(profile);
        }
        else
        {
            db.Entry(profile).Property(p => p.Version).OriginalValue = command.Version;
        }

        profile.Headline = command.Headline?.Trim() ?? "";
        profile.Summary = command.Summary?.Trim() ?? "";
        profile.Location = command.Location?.Trim() ?? "";
        profile.TargetRoles = Distinct(command.TargetRoles);
        profile.Seniority = command.Seniority;
        profile.WorkModes = ProfileVocabulary.WorkModes.Where(m => command.WorkModes?.Contains(m) == true).ToList();
        profile.Languages = (command.Languages ?? [])
            .Select(l => new SpokenLanguage { Name = l.Name.Trim(), Level = l.Level })
            .ToList();

        audit.Add(ProfileAudit.ProfileUpdated, currentUser.UserId, "profile", profile.Id);
        await db.SaveChangesAsync(ct);
        return ProfileDto.From(profile);
    }

    private static List<string> Distinct(IReadOnlyList<string>? values) =>
        (values ?? []).Select(v => v.Trim()).Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static ConflictException Stale() =>
        new("concurrency.conflict", "Your profile was changed elsewhere. Reload and try again.");
}
