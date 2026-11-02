using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace JobHunter.IntegrationTests;

/// <summary>
/// Stands in for every outside site in the integration tests: answers registered URLs and
/// records what was asked, so a test can also prove something was never requested.
/// </summary>
public sealed class StubHttp : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, Func<HttpResponseMessage>> _routes = new();

    public ConcurrentQueue<string> Requested { get; } = new();

    public void On(string url, string body, string contentType = "text/html", HttpStatusCode status = HttpStatusCode.OK) =>
        _routes[url] = () => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, contentType) };

    public void OnJson(string url, string body, HttpStatusCode status = HttpStatusCode.OK) => On(url, body, "application/json", status);

    public void Redirect(string url, string to) =>
        _routes[url] = () => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri(to) } };

    public bool WasRequested(string url) => Requested.Contains(url);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Fragments never reach a server.
        var url = request.RequestUri!.GetLeftPart(UriPartial.Query);
        Requested.Enqueue(url);
        return Task.FromResult(_routes.TryGetValue(url, out var respond) ? respond() : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
