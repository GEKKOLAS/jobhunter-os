using FluentValidation;
using JobHunter.Application.Common;
using JobHunter.Domain.Profile;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.Profile;

public sealed record EvidenceInput(
    string Kind, string Title, string? Organization, DateOnly? StartDate, DateOnly? EndDate,
    string? Description, string? Outcome, IReadOnlyList<LinkDto>? Links, IReadOnlyList<Guid>? SkillIds);

public sealed class EvidenceInputValidator : AbstractValidator<EvidenceInput>
{
    public EvidenceInputValidator()
    {
        RuleFor(x => x.Kind).Must(ProfileVocabulary.EvidenceKinds.Contains)
            .WithMessage($"Kind must be one of: {string.Join(", ", ProfileVocabulary.EvidenceKinds)}.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Organization).MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Outcome).MaximumLength(1000);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.StartDate is not null && x.EndDate is not null)
            .WithMessage("The end can't be before the start.");
        RuleFor(x => x.Links).Must(l => l is null || l.Count <= 5).WithMessage("At most 5 links.");
        RuleForEach(x => x.Links).ChildRules(link =>
        {
            link.RuleFor(l => l.Label).NotEmpty().MaximumLength(60);
            link.RuleFor(l => l.Url).NotEmpty().MaximumLength(2048)
                .Must(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                .WithMessage("Links must be https URLs.");
        });
        RuleFor(x => x.SkillIds).Must(s => s is null || s.Count <= 50).WithMessage("At most 50 skills per item.");
    }
}

public sealed class EvidenceHandler(
    IProfileDbContext db, ICurrentUser currentUser, IAuditWriter audit, IValidator<EvidenceInput> validator)
{
    public async Task<IReadOnlyList<EvidenceDto>> ListAsync(CancellationToken ct)
    {
        // PostgreSQL sorts NULLs first in descending order: undated items lead.
        var items = await db.EvidenceItems.AsNoTracking()
            .Where(e => e.UserId == currentUser.UserId)
            .OrderByDescending(e => e.StartDate).ThenByDescending(e => e.CreatedAt)
            .ToListAsync(ct);
        return items.Select(EvidenceDto.From).ToList();
    }

    public async Task<EvidenceDto> CreateAsync(EvidenceInput input, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        if (await db.EvidenceItems.CountAsync(e => e.UserId == currentUser.UserId, ct) >= ProfileVocabulary.MaxEvidence)
        {
            throw new ConflictException("profile.limit", $"You can keep up to {ProfileVocabulary.MaxEvidence} evidence items.");
        }

        var item = new EvidenceItem { UserId = currentUser.UserId, Kind = input.Kind, Title = input.Title };
        await ApplyAsync(item, input, ct);
        db.EvidenceItems.Add(item);
        audit.Add(ProfileAudit.EvidenceCreated, currentUser.UserId, "evidence_item", item.Id);
        await db.SaveChangesAsync(ct);
        return EvidenceDto.From(item);
    }

    public async Task<EvidenceDto> UpdateAsync(Guid id, EvidenceInput input, uint version, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        var item = await FindAsync(id, ct);

        db.Entry(item).Property(e => e.Version).OriginalValue = version;
        await ApplyAsync(item, input, ct);
        audit.Add(ProfileAudit.EvidenceUpdated, currentUser.UserId, "evidence_item", item.Id);
        await db.SaveChangesAsync(ct);
        return EvidenceDto.From(item);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var item = await FindAsync(id, ct);
        db.EvidenceItems.Remove(item);
        audit.Add(ProfileAudit.EvidenceDeleted, currentUser.UserId, "evidence_item", id);
        await db.SaveChangesAsync(ct);
    }

    private async Task<EvidenceItem> FindAsync(Guid id, CancellationToken ct) =>
        await db.EvidenceItems.FirstOrDefaultAsync(e => e.Id == id && e.UserId == currentUser.UserId, ct)
        ?? throw new NotFoundException();

    private async Task ApplyAsync(EvidenceItem item, EvidenceInput input, CancellationToken ct)
    {
        var skillIds = (input.SkillIds ?? []).Distinct().ToList();
        var owned = await db.ProfileSkills.CountAsync(s => s.UserId == currentUser.UserId && skillIds.Contains(s.Id), ct);
        if (owned != skillIds.Count)
        {
            throw new InvalidInputException("evidence.unknown_skill", "Link only skills from your own profile.");
        }

        item.Kind = input.Kind;
        item.Title = input.Title.Trim();
        item.Organization = string.IsNullOrWhiteSpace(input.Organization) ? null : input.Organization.Trim();
        item.StartDate = input.StartDate;
        item.EndDate = input.EndDate;
        item.Description = input.Description?.Trim() ?? "";
        item.Outcome = string.IsNullOrWhiteSpace(input.Outcome) ? null : input.Outcome.Trim();
        item.Links = (input.Links ?? []).Select(l => new EvidenceLink { Label = l.Label.Trim(), Url = l.Url.Trim() }).ToList();
        item.SkillIds = skillIds;
    }
}
