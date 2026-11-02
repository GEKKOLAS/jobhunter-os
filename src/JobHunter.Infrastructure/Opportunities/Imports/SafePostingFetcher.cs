using System.Net;
using System.Net.Http.Headers;
using System.Text;
using JobHunter.Application.Opportunities.Imports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobHunter.Infrastructure.Opportunities.Imports;

/// <summary>
/// Fetches one page a user asked to import: the URL rules first, redirects by hand with each
/// hop re-checked, a time limit and a size cap (spec 02 §5). Addresses are checked by the
/// handler's <see cref="GuardedConnector"/>.
/// </summary>
public sealed class SafePostingFetcher(
    HttpClient http, IOptions<ImportOptions> options, ILogger<SafePostingFetcher> logger) : IPostingFetcher
{
    private static readonly HashSet<string> AcceptedTypes = new(StringComparer.OrdinalIgnoreCase) { "text/html", "text/plain" };

    public async Task<FetchedPage> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));

        var current = url;
        for (var hop = 0; ; hop++)
        {
            if (SsrfGuard.RejectUrl(current, settings.AllowHttp) is { } rejection)
            {
                throw ImportFailedException.Unreachable(rejection);
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            // Honest about what's asking; one page, because a person asked for it.
            request.Headers.UserAgent.ParseAdd("JobHunterOS/0.1 (user-initiated import of one job posting)");
            request.Headers.Accept.ParseAdd("text/html");
            request.Headers.Accept.ParseAdd("text/plain;q=0.9");

            try
            {
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

                if (IsRedirect(response.StatusCode))
                {
                    if (hop >= settings.MaxRedirects) throw ImportFailedException.Unreachable("That link redirects too many times.");
                    if (response.Headers.Location is not { } location) throw ImportFailedException.Unreachable();
                    current = new Uri(current, location);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw ImportFailedException.Unreachable($"That page answered with an error ({(int)response.StatusCode}).");
                }

                var mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
                if (!AcceptedTypes.Contains(mediaType))
                {
                    throw ImportFailedException.Unsupported("That link isn't a web page. Paste the posting's text instead.");
                }

                var body = await ReadCappedAsync(response.Content, settings.MaxBytes, timeout.Token);
                return new FetchedPage(current, mediaType.ToLowerInvariant(), body);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw ImportFailedException.Unreachable("That page took too long to answer.");
            }
            catch (HttpRequestException ex) when (ex.InnerException is BlockedAddressException)
            {
                logger.LogWarning("Blocked an import to a non-public address: {Host}", current.Host);
                throw ImportFailedException.Unreachable("That address isn't a public website.");
            }
            catch (HttpRequestException ex)
            {
                logger.LogInformation(ex, "Import fetch failed for {Host}", current.Host);
                throw ImportFailedException.Unreachable();
            }
        }
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    /// <summary>Reads at most <paramref name="maxBytes"/> (after decompression), never the whole of a bigger body.</summary>
    private static async Task<string> ReadCappedAsync(HttpContent content, int maxBytes, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                throw ImportFailedException.Unsupported("That page is too large to import. Paste the posting's text instead.");
            }
            buffer.Write(chunk, 0, read);
        }
        return EncodingOf(content.Headers.ContentType).GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static Encoding EncodingOf(MediaTypeHeaderValue? contentType)
    {
        try
        {
            return string.IsNullOrWhiteSpace(contentType?.CharSet)
                ? Encoding.UTF8
                : Encoding.GetEncoding(contentType.CharSet.Trim('"'));
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }
}
