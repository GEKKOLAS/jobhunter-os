using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace JobHunter.Infrastructure.Opportunities.Imports;

/// <summary>Resolves a host name; a seam so the connect-time check can be tested without real DNS.</summary>
public interface IHostResolver
{
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
}

internal sealed class DnsHostResolver : IHostResolver
{
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) =>
        Dns.GetHostAddressesAsync(host, cancellationToken);
}

/// <summary>Thrown when a host resolves to anything but public addresses.</summary>
public sealed class BlockedAddressException(string host)
    : Exception($"'{host}' resolves to an address that isn't public.");

/// <summary>
/// The socket side of the SSRF guard: resolves the host while connecting, refuses it if any
/// address isn't public, and connects only to the addresses it just checked. Checking
/// earlier and letting the handler resolve again would be open to DNS rebinding.
/// </summary>
public sealed class GuardedConnector(IHostResolver resolver)
{
    public async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await resolver.ResolveAsync(host, cancellationToken);

        if (addresses.Length == 0 || addresses.Any(a => !SsrfGuard.IsPublic(a)))
        {
            throw new BlockedAddressException(host);
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>The primary handler for page imports: no cookies, no automatic redirects, this connector.</summary>
    public SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        AutomaticDecompression = DecompressionMethods.All,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        ConnectCallback = ConnectAsync,
    };
}
