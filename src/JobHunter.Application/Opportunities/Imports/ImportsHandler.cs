using FluentValidation;
using JobHunter.Application.Common;
using JobHunter.Domain.Opportunities;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Application.Opportunities.Imports;

public sealed record TextImportCommand(string Text);

public sealed class TextImportValidator : AbstractValidator<TextImportCommand>
{
    public TextImportValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(50_000);
    }
}

public sealed record UrlImportCommand(string Url);

public sealed class UrlImportValidator : AbstractValidator<UrlImportCommand>
{
    public UrlImportValidator()
    {
        RuleFor(x => x.Url).NotEmpty().MaximumLength(2048)
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            .WithMessage("Use the posting's full web address.");
    }
}

/// <summary>Turns a paste or a link into a preview. Nothing is saved here (spec 03, Imports never save).</summary>
public sealed class ImportsHandler(
    IPostingFetcher fetcher,
    IPostingParser parser,
    IEnumerable<IJobBoardClient> boards,
    IOpportunitiesDbContext db,
    ICurrentUser currentUser,
    IValidator<TextImportCommand> textValidator,
    IValidator<UrlImportCommand> urlValidator)
{
    public async Task<PostingPreview> FromTextAsync(TextImportCommand command, CancellationToken ct)
    {
        await textValidator.ValidateAndThrowAsync(command, ct);
        return ToPreview(parser.ParseText(command.Text), url: null, source: "paste", sourceRef: null, existing: null);
    }

    public async Task<PostingPreview> FromUrlAsync(UrlImportCommand command, CancellationToken ct)
    {
        await urlValidator.ValidateAndThrowAsync(command, ct);
        var url = new Uri(command.Url.Trim());

        if (ImportRules.ForbiddenSite(url.Host) is { } site) throw ImportFailedException.NotAllowed(site);

        if (ImportRules.ParseJobBoardLink(url) is { } link && await FromBoardAsync(link, ct) is { } boardPreview)
        {
            return boardPreview;
        }

        var page = await fetcher.FetchAsync(url, ct);
        // A redirect can land on a forbidden site too.
        if (ImportRules.ForbiddenSite(page.FinalUrl.Host) is { } finalSite) throw ImportFailedException.NotAllowed(finalSite);

        var parsed = page.MediaType == "text/html" ? parser.ParseHtml(page.Body) : parser.ParseText(page.Body);
        var sourceRef = PostingRefs.ForUrl(page.FinalUrl);
        return ToPreview(parsed, page.FinalUrl.ToString(), "url", sourceRef, await ExistingAsync("url", sourceRef, ct));
    }

    /// <summary>The posting from the provider's API; null when it's not there, so the page itself is tried next.</summary>
    private async Task<PostingPreview?> FromBoardAsync(JobBoardLink link, CancellationToken ct)
    {
        var client = boards.FirstOrDefault(b => b.Provider == link.Provider);
        if (client is null) return null;

        IReadOnlyList<BoardPosting>? postings;
        try
        {
            postings = await client.ListAsync(link.Board, ct);
        }
        catch (UpstreamUnavailableException)
        {
            return null;
        }

        var posting = postings?.FirstOrDefault(p => p.Id == link.PostingId)?.WithUsableSalary();
        if (posting is null) return null;

        var sourceRef = PostingRefs.ForBoard(link.Board, posting.Id);
        var company = posting.CompanyName ?? ImportRules.TidyBoardName(link.Board);
        return new PostingPreview(
            posting.Title, company, posting.Location, posting.WorkMode, posting.EmploymentType, posting.SalaryMin, posting.SalaryMax,
            posting.SalaryCurrency, posting.SalaryPeriod, posting.Url, posting.Description, link.Provider, sourceRef,
            ParsedFrom.JobBoard, [], await ExistingAsync(link.Provider, sourceRef, ct));
    }

    private async Task<Guid?> ExistingAsync(string source, string sourceRef, CancellationToken ct) =>
        await db.Opportunities.AsNoTracking()
            .Where(o => o.UserId == currentUser.UserId && o.Source == source && o.SourceRef == sourceRef)
            .Select(o => (Guid?)o.Id)
            .FirstOrDefaultAsync(ct);

    private static PostingPreview ToPreview(ParsedPosting p, string? url, string source, string? sourceRef, Guid? existing) => new(
        p.Title, p.CompanyName, p.Location, p.WorkMode, p.EmploymentType, p.SalaryMin, p.SalaryMax, p.SalaryCurrency,
        p.SalaryPeriod, url, p.Description, source, sourceRef, p.ParsedFrom, p.Warnings, existing);
}
