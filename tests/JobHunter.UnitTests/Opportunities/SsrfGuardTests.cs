using System.Net;
using System.Text;
using JobHunter.Application.Opportunities.Imports;
using JobHunter.Infrastructure.Opportunities.Imports;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobHunter.UnitTests.Opportunities;

/// <summary>The SSRF rules of spec 02 §5: every way a link could reach something private.</summary>
public class SsrfGuardTests
{
    [Theory]
    // IPv4: loopback, private, link-local and cloud metadata, CGNAT, "this network", documentation, multicast, broadcast
    [InlineData("127.0.0.1")]
    [InlineData("127.255.255.254")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.255")]
    [InlineData("0.0.0.0")]
    [InlineData("192.0.2.10")]
    [InlineData("198.18.0.1")]
    [InlineData("198.51.100.7")]
    [InlineData("203.0.113.7")]
    [InlineData("224.0.0.1")]
    [InlineData("240.0.0.1")]
    [InlineData("255.255.255.255")]
    // IPv6: unspecified, loopback, unique local (AWS metadata is fd00:ec2::254), link-local, site-local, multicast, documentation
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("fc00::1")]
    [InlineData("fd00:ec2::254")]
    [InlineData("fe80::1")]
    [InlineData("fec0::1")]
    [InlineData("ff02::1")]
    [InlineData("2001:db8::1")]
    // IPv6 forms that embed a private IPv4 address
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("::127.0.0.1")]
    [InlineData("64:ff9b::7f00:1")]
    [InlineData("2002:7f00:1::")]
    [InlineData("2001:0:4136:e378:8000:63bf:3fff:fdd2")] // Teredo
    public void Private_and_special_addresses_are_not_public(string address)
    {
        Assert.False(SsrfGuard.IsPublic(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("172.32.0.1")]
    [InlineData("100.128.0.1")]
    [InlineData("192.169.0.1")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("::ffff:8.8.8.8")]
    [InlineData("64:ff9b::808:808")]
    public void Ordinary_addresses_are_public(string address)
    {
        Assert.True(SsrfGuard.IsPublic(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("ftp://example.com/job")]
    [InlineData("file:///etc/passwd")]
    [InlineData("http://example.com/job")] // http needs AllowHttp
    [InlineData("https://user:secret@example.com/job")]
    [InlineData("https://example.com:8443/job")]
    [InlineData("https://localhost/job")]
    [InlineData("https://intranet/job")]
    [InlineData("https://app.localhost/job")]
    [InlineData("https://printer.local/job")]
    [InlineData("https://metadata.google.internal/computeMetadata/v1/")]
    [InlineData("https://127.0.0.1/job")]
    [InlineData("https://[::1]/job")]
    [InlineData("https://169.254.169.254/latest/meta-data/")]
    [InlineData("https://2130706433/job")] // 127.0.0.1 written as one number
    [InlineData("https://0x7f.0.0.1/job")] // and in hex
    [InlineData("https://localhost./job")] // a trailing dot changes nothing
    public void Unsafe_urls_are_rejected_before_any_request(string url)
    {
        Assert.NotNull(SsrfGuard.RejectUrl(new Uri(url), allowHttp: false));
    }

    [Theory]
    [InlineData("https://example.com/jobs/1", false)]
    [InlineData("https://careers.contoso.co.uk/a?b=c", false)]
    [InlineData("https://8.8.8.8/x", false)]
    [InlineData("http://example.com/jobs/1", true)]
    public void Ordinary_urls_pass(string url, bool allowHttp)
    {
        Assert.Null(SsrfGuard.RejectUrl(new Uri(url), allowHttp));
    }

    // ---- The connect-time check, with a real handler and a fake DNS ----

    private sealed class FakeResolver(params string[] addresses) : IHostResolver
    {
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) =>
            Task.FromResult(addresses.Select(IPAddress.Parse).ToArray());
    }

    private static SafePostingFetcher RealFetcher(IHostResolver resolver) => new(
        new HttpClient(new GuardedConnector(resolver).CreateHandler()),
        Options.Create(new ImportOptions { TimeoutSeconds = 5 }),
        NullLogger<SafePostingFetcher>.Instance);

    [Theory]
    [InlineData("10.0.0.5")]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("8.8.8.8", "192.168.1.1")] // one bad address is enough: the client could pick it
    public async Task A_public_looking_name_that_resolves_privately_is_blocked_at_connect_time(params string[] addresses)
    {
        var fetcher = RealFetcher(new FakeResolver(addresses));

        var failure = await Assert.ThrowsAsync<ImportFailedException>(
            () => fetcher.FetchAsync(new Uri("https://rebinding.example.com/job"), CancellationToken.None));

        Assert.Equal("import.unreachable", failure.Code);
        Assert.Equal("That address isn't a public website.", failure.Message);
    }

    // ---- Redirects, content types, size and time, with a scripted handler ----

    private sealed class Scripted(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<Uri> Requested { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requested.Add(request.RequestUri!);
            return respond(request);
        }
    }

    private static HttpResponseMessage Redirect(string to) =>
        new(HttpStatusCode.Found) { Headers = { Location = new Uri(to, UriKind.RelativeOrAbsolute) } };

    private static HttpResponseMessage Page(string body, string type = "text/html") =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, type) };

    private static SafePostingFetcher Fetcher(Scripted handler, int maxBytes = 1024) => new(
        new HttpClient(handler),
        Options.Create(new ImportOptions { MaxBytes = maxBytes, TimeoutSeconds = 5 }),
        NullLogger<SafePostingFetcher>.Instance);

    private static readonly Uri Start = new("https://jobs.example.com/1");

    [Fact]
    public async Task Redirects_are_followed_by_hand_and_the_final_url_is_reported()
    {
        var handler = new Scripted(r => Task.FromResult(r.RequestUri!.AbsolutePath == "/1" ? Redirect("/2") : Page("<p>Hi</p>")));

        var page = await Fetcher(handler).FetchAsync(Start, CancellationToken.None);

        Assert.Equal("https://jobs.example.com/2", page.FinalUrl.ToString());
        Assert.Equal("text/html", page.MediaType);
        Assert.Equal("<p>Hi</p>", page.Body);
    }

    [Theory]
    [InlineData("https://169.254.169.254/latest/meta-data/")]
    [InlineData("https://localhost/admin")]
    [InlineData("http://jobs.example.com/plain")]
    [InlineData("https://jobs.example.com:8443/odd-port")]
    public async Task A_redirect_to_somewhere_unsafe_is_refused_without_requesting_it(string target)
    {
        var handler = new Scripted(_ => Task.FromResult(Redirect(target)));

        var failure = await Assert.ThrowsAsync<ImportFailedException>(() => Fetcher(handler).FetchAsync(Start, CancellationToken.None));

        Assert.Equal("import.unreachable", failure.Code);
        Assert.Equal([Start], handler.Requested);
    }

    [Fact]
    public async Task Too_many_redirects_give_up()
    {
        var hop = 0;
        var handler = new Scripted(_ => Task.FromResult(Redirect($"/{++hop}")));

        var failure = await Assert.ThrowsAsync<ImportFailedException>(() => Fetcher(handler).FetchAsync(Start, CancellationToken.None));

        Assert.Contains("redirects too many times", failure.Message);
        Assert.Equal(4, handler.Requested.Count); // the first request plus 3 redirects
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("application/json")]
    [InlineData("image/png")]
    public async Task Other_content_types_are_unsupported(string type)
    {
        var handler = new Scripted(_ => Task.FromResult(Page("x", type)));

        var failure = await Assert.ThrowsAsync<ImportFailedException>(() => Fetcher(handler).FetchAsync(Start, CancellationToken.None));

        Assert.Equal("import.unsupported", failure.Code);
    }

    [Fact]
    public async Task Bodies_over_the_cap_are_refused()
    {
        var handler = new Scripted(_ => Task.FromResult(Page(new string('x', 5000))));

        var failure = await Assert.ThrowsAsync<ImportFailedException>(() => Fetcher(handler, maxBytes: 1024).FetchAsync(Start, CancellationToken.None));

        Assert.Equal("import.unsupported", failure.Code);
        Assert.Contains("too large", failure.Message);
    }

    private sealed class Hanging : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }
    }

    [Fact]
    public async Task Slow_pages_time_out()
    {
        var fetcher = new SafePostingFetcher(
            new HttpClient(new Hanging()),
            Options.Create(new ImportOptions { TimeoutSeconds = 1 }),
            NullLogger<SafePostingFetcher>.Instance);

        var failure = await Assert.ThrowsAsync<ImportFailedException>(() => fetcher.FetchAsync(Start, CancellationToken.None));

        Assert.Equal("import.unreachable", failure.Code);
        Assert.Contains("too long", failure.Message);
    }

    [Fact]
    public async Task Error_pages_are_unreachable()
    {
        var handler = new Scripted(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)));

        var failure = await Assert.ThrowsAsync<ImportFailedException>(() => Fetcher(handler).FetchAsync(Start, CancellationToken.None));

        Assert.Equal("import.unreachable", failure.Code);
        Assert.Contains("403", failure.Message);
    }
}
