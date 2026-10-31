using FluentValidation;
using JobHunter.Application.Common;
using JobHunter.Domain.Profile;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.Profile;

/// <param name="Category">May be null for a catalog skill: the catalog's category is used.</param>
public sealed record SkillInput(string Name, string? Category, int Level, decimal Years, int? LastUsedYear);

public sealed class SkillInputValidator : AbstractValidator<SkillInput>
{
    public SkillInputValidator(IClock clock)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60)
            .Must(n => SkillKey.Compact(n).Length > 0).WithMessage("Use at least one letter or digit.");
        RuleFor(x => x.Category).Must(c => c is null || ProfileVocabulary.SkillCategories.Contains(c))
            .WithMessage($"Category must be one of: {string.Join(", ", ProfileVocabulary.SkillCategories)}.");
        RuleFor(x => x.Level).InclusiveBetween(1, 5);
        RuleFor(x => x.Years).InclusiveBetween(0, 40)
            .Must(y => decimal.Round(y, 1) == y).WithMessage("Use at most one decimal.");
        RuleFor(x => x.LastUsedYear).InclusiveBetween(1970, clock.UtcNow.Year).When(x => x.LastUsedYear is not null);
    }
}

public sealed class SkillsHandler(
    IProfileDbContext db, ICurrentUser currentUser, IAuditWriter audit, IValidator<SkillInput> validator)
{
    public async Task<IReadOnlyList<SkillDto>> ListAsync(CancellationToken ct)
    {
        var skills = await db.ProfileSkills.AsNoTracking()
            .Where(s => s.UserId == currentUser.UserId)
            .OrderByDescending(s => s.Level).ThenBy(s => s.Name)
            .ToListAsync(ct);
        var citations = await CitationsAsync(ct);
        return skills.Select(s => SkillDto.From(s, citations.GetValueOrDefault(s.Id))).ToList();
    }

    public async Task<SkillDto> CreateAsync(SkillInput input, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        var (key, category) = Resolve(input);

        if (await db.ProfileSkills.CountAsync(s => s.UserId == currentUser.UserId, ct) >= ProfileVocabulary.MaxSkills)
        {
            throw new ConflictException("profile.limit", $"You can keep up to {ProfileVocabulary.MaxSkills} skills.");
        }
        await EnsureUniqueAsync(key, exceptId: null, ct);

        var skill = new ProfileSkill
        {
            UserId = currentUser.UserId,
            Name = input.Name.Trim(),
            Key = key,
            Category = category,
            Level = input.Level,
            Years = input.Years,
            LastUsedYear = input.LastUsedYear,
        };
        db.ProfileSkills.Add(skill);
        audit.Add(ProfileAudit.SkillCreated, currentUser.UserId, "profile_skill", skill.Id, new { skill.Key });
        await db.SaveChangesAsync(ct);
        return SkillDto.From(skill, 0);
    }

    public async Task<SkillDto> UpdateAsync(Guid id, SkillInput input, uint version, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        var skill = await FindAsync(id, ct);
        var (key, category) = Resolve(input);
        await EnsureUniqueAsync(key, exceptId: id, ct);

        db.Entry(skill).Property(s => s.Version).OriginalValue = version;
        skill.Name = input.Name.Trim();
        skill.Key = key;
        skill.Category = category;
        skill.Level = input.Level;
        skill.Years = input.Years;
        skill.LastUsedYear = input.LastUsedYear;

        audit.Add(ProfileAudit.SkillUpdated, currentUser.UserId, "profile_skill", skill.Id, new { skill.Key });
        await db.SaveChangesAsync(ct);
        return SkillDto.From(skill, (await CitationsAsync(ct)).GetValueOrDefault(skill.Id));
    }

    /// <summary>Removes the skill and every evidence link to it.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var skill = await FindAsync(id, ct);
        var citing = await db.EvidenceItems
            .Where(e => e.UserId == currentUser.UserId && e.SkillIds.Contains(id))
            .ToListAsync(ct);
        foreach (var item in citing)
        {
            item.SkillIds = item.SkillIds.Where(s => s != id).ToList();
        }

        db.ProfileSkills.Remove(skill);
        audit.Add(ProfileAudit.SkillDeleted, currentUser.UserId, "profile_skill", id, new { skill.Key, unlinkedFrom = citing.Count });
        await db.SaveChangesAsync(ct);
    }

    private async Task<ProfileSkill> FindAsync(Guid id, CancellationToken ct) =>
        await db.ProfileSkills.FirstOrDefaultAsync(s => s.Id == id && s.UserId == currentUser.UserId, ct)
        ?? throw new NotFoundException();

    /// <summary>The key, and the category: as given, else the catalog's.</summary>
    private static (string Key, string Category) Resolve(SkillInput input)
    {
        var key = SkillKey.For(input.Name);
        var category = input.Category ?? SkillCatalog.FindByKey(key)?.Category
            ?? throw new ValidationException([new FluentValidation.Results.ValidationFailure(
                nameof(SkillInput.Category), "Choose a category: this skill isn't in the catalog.")]);
        return (key, category);
    }

    private async Task EnsureUniqueAsync(string key, Guid? exceptId, CancellationToken ct)
    {
        var existing = await db.ProfileSkills
            .Where(s => s.UserId == currentUser.UserId && s.Key == key && s.Id != exceptId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            throw new ConflictException("skill.duplicate", $"\"{existing}\" is already in your skills.");
        }
    }

    /// <summary>Skill id → how many evidence items cite it.</summary>
    private async Task<Dictionary<Guid, int>> CitationsAsync(CancellationToken ct)
    {
        var lists = await db.EvidenceItems.AsNoTracking()
            .Where(e => e.UserId == currentUser.UserId)
            .Select(e => e.SkillIds)
            .ToListAsync(ct);
        return lists.SelectMany(ids => ids.Distinct()).GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
    }
}
