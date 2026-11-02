using FluentValidation;
using JobHunter.Application.Common;
using JobHunter.Domain.Opportunities;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.Opportunities.Imports;

public sealed record SourceInput(string Provider, string Board, string? Label);

public sealed class SourceInputValidator : AbstractValidator<SourceInput>
{
    public SourceInputValidator()
    {
        RuleFor(x => x.Provider).Must(ImportRules.BoardProviders.Contains)
            .WithMessage($"Provider must be one of: {string.Join(", ", ImportRules.BoardProviders)}.");
        RuleFor(x => x.Board).Must(ImportRules.IsValidBoardToken)
            .WithMessage("Use the board's name as it appears in its link, like \"contoso\".");
        RuleFor(x => x.Label).MaximumLength(80);
    }
}

public sealed record SourceDto(Guid Id, string Provider, string Board, string Label, DateTimeOffset? LastCheckedAt)
{
    public static SourceDto From(JobSource s) => new(s.Id, s.Provider, s.Board, s.Label, s.LastCheckedAt);
}

public sealed record SourcePostingDto(string Ref, string Title, string? Location, string? Url, bool AlreadySaved);

/// <summary>A company's public job board: follow it, browse it, pick what to save. Nothing is saved on its own.</summary>
public sealed class SourcesHandler(
    IOpportunitiesDbContext db,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock,
    IEnumerable<IJobBoardClient> boards,
    OpportunitiesHandler opportunities,
    IValidator<SourceInput> validator)
{
    public async Task<IReadOnlyList<SourceDto>> ListAsync(CancellationToken ct)
    {
        var sources = await db.JobSources.AsNoTracking()
            .Where(s => s.UserId == currentUser.UserId)
            .OrderBy(s => s.Label)
            .ToListAsync(ct);
        return sources.Select(SourceDto.From).ToList();
    }

    public async Task<SourceDto> CreateAsync(SourceInput input, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        var board = input.Board.Trim();

        if (await db.JobSources.CountAsync(s => s.UserId == currentUser.UserId, ct) >= JobSource.MaxPerUser)
        {
            throw new ConflictException("source.limit", $"You can follow up to {JobSource.MaxPerUser} job boards.");
        }
        if (await db.JobSources.AnyAsync(s => s.UserId == currentUser.UserId && s.Provider == input.Provider && s.Board == board, ct))
        {
            throw new ConflictException("source.duplicate", "You already follow that job board.");
        }

        // Check it exists now, so a typo fails here and not later.
        var postings = await ClientFor(input.Provider).ListAsync(board, ct)
            ?? throw new InvalidInputException("source.not_found", $"There's no public {Title(input.Provider)} job board called \"{board}\".");

        var source = new JobSource
        {
            UserId = currentUser.UserId,
            Provider = input.Provider,
            Board = board,
            Label = string.IsNullOrWhiteSpace(input.Label)
                ? Fit(postings.Select(p => p.CompanyName).FirstOrDefault(n => n is not null) ?? ImportRules.TidyBoardName(board), MaxLabel)
                : input.Label.Trim(),
            LastCheckedAt = clock.UtcNow,
        };
        db.JobSources.Add(source);
        audit.Add(OpportunitiesAudit.SourceCreated, currentUser.UserId, "job_source", source.Id, new { source.Provider, source.Board });
        await db.SaveChangesAsync(ct);
        return SourceDto.From(source);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var source = await FindAsync(id, ct);
        db.JobSources.Remove(source);
        audit.Add(OpportunitiesAudit.SourceDeleted, currentUser.UserId, "job_source", id, new { source.Provider, source.Board });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The board's listed postings, live, each flagged if already saved. Read-only.</summary>
    public async Task<IReadOnlyList<SourcePostingDto>> PostingsAsync(Guid id, CancellationToken ct)
    {
        var source = await FindAsync(id, ct);
        var postings = await LoadAsync(source, ct);

        var prefix = PostingRefs.ForBoard(source.Board, "");
        var saved = await db.Opportunities.AsNoTracking()
            .Where(o => o.UserId == currentUser.UserId && o.Source == source.Provider && o.SourceRef != null && o.SourceRef.StartsWith(prefix))
            .Select(o => o.SourceRef!)
            .ToListAsync(ct);
        var savedRefs = saved.ToHashSet();

        source.LastCheckedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);

        return postings
            .Select(p => new SourcePostingDto(p.Id, p.Title, p.Location, p.Url, savedRefs.Contains(PostingRefs.ForBoard(source.Board, p.Id))))
            .ToList();
    }

    /// <summary>Saves one posting the user picked: new (201), or updating the copy already saved (200).</summary>
    public async Task<SavedOpportunity> SavePostingAsync(Guid id, string postingRef, CancellationToken ct)
    {
        var source = await FindAsync(id, ct);
        var postings = await LoadAsync(source, ct);
        var found = postings.FirstOrDefault(p => p.Id == postingRef)
            ?? throw new NotFoundException("source.posting_gone", "That posting is no longer on the board.");
        var posting = found.WithUsableSalary();

        return await opportunities.SaveAsync(
            new OpportunityInput(
                posting.Title, null, Fit(posting.CompanyName ?? source.Label, MaxCompany), posting.Location, posting.WorkMode, posting.EmploymentType,
                posting.SalaryMin, posting.SalaryMax, posting.SalaryCurrency, posting.SalaryPeriod, posting.Url,
                posting.Description, Priority: null, Stage: null, source.Provider, PostingRefs.ForBoard(source.Board, posting.Id)),
            ct);
    }

    private async Task<IReadOnlyList<BoardPosting>> LoadAsync(JobSource source, CancellationToken ct) =>
        await ClientFor(source.Provider).ListAsync(source.Board, ct)
        ?? throw new InvalidInputException("source.not_found", $"The {Title(source.Provider)} job board \"{source.Board}\" no longer exists.");

    private IJobBoardClient ClientFor(string provider) => boards.First(b => b.Provider == provider);

    private async Task<JobSource> FindAsync(Guid id, CancellationToken ct) =>
        await db.JobSources.FirstOrDefaultAsync(s => s.Id == id && s.UserId == currentUser.UserId, ct)
        ?? throw new NotFoundException();

    private const int MaxLabel = 80;
    private const int MaxCompany = 120;

    private static string Fit(string value, int max) => value.Length <= max ? value : value[..max].TrimEnd();

    private static string Title(string provider) => char.ToUpperInvariant(provider[0]) + provider[1..];
}
