using System.Net;
using System.Net.Sockets;

namespace JobHunter.Infrastructure.Opportunities.Imports;

/// <summary>
/// What a user-supplied URL may point at (spec 02 §5). Two checks, both needed: the URL
/// itself before any request, and every resolved address at connect time.
/// </summary>
public static class SsrfGuard
{
    private static readonly string[] InternalSuffixes =
        [".localhost", ".local", ".internal", ".intranet", ".lan", ".home", ".corp", ".test", ".example", ".invalid"];

    /// <summary>Null when the URL may be fetched; otherwise why not, in words for the user.</summary>
    public static string? RejectUrl(Uri url, bool allowHttp)
    {
        if (!url.IsAbsoluteUri) return "Use the page's full web address.";
        if (url.Scheme != Uri.UriSchemeHttps && !(allowHttp && url.Scheme == Uri.UriSchemeHttp)) return "Only https links can be imported.";
        if (url.UserInfo.Length > 0) return "Links with a username or password can't be imported.";
        if (!url.IsDefaultPort) return "Only links on the standard web port can be imported.";

        var host = url.IdnHost.TrimEnd('.').ToLowerInvariant();
        if (url.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
        {
            return IPAddress.TryParse(host.Trim('[', ']'), out var address) && IsPublic(address)
                ? null
                : "That address isn't a public website.";
        }

        // A bare name (no dot) or an internal suffix can only mean something on a private network.
        if (!host.Contains('.') || InternalSuffixes.Any(s => host.EndsWith(s, StringComparison.Ordinal)))
        {
            return "That address isn't a public website.";
        }
        return null;
    }

    /// <summary>True only for addresses on the public internet.</summary>
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPublicV4(address.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => IsPublicV6(address),
            _ => false,
        };
    }

    private static bool IsPublicV4(byte[] b) => (b[0], b[1], b[2]) switch
    {
        (0, _, _) => false,                                  // "this network"
        (10, _, _) => false,                                 // private
        (100, >= 64 and <= 127, _) => false,                 // CGNAT
        (127, _, _) => false,                                // loopback
        (169, 254, _) => false,                              // link-local, cloud metadata
        (172, >= 16 and <= 31, _) => false,                  // private
        (192, 0, 0) => false,                                // IETF assignments
        (192, 0, 2) => false,                                // documentation
        (192, 88, 99) => false,                              // deprecated 6to4 relay
        (192, 168, _) => false,                              // private
        (198, 18 or 19, _) => false,                         // benchmarking
        (198, 51, 100) => false,                             // documentation
        (203, 0, 113) => false,                              // documentation
        ( >= 224, _, _) => false,                            // multicast, reserved, broadcast
        _ => true,
    };

    private static bool IsPublicV6(IPAddress address)
    {
        if (address.Equals(IPAddress.IPv6None) || address.Equals(IPAddress.IPv6Loopback)) return false;
        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || address.IsIPv6UniqueLocal) return false;

        var b = address.GetAddressBytes();
        if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8) return false;       // documentation
        if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0 && b[3] == 0) return false;             // Teredo
        if (b[0] == 0x20 && b[1] == 0x02) return IsPublicV4(b[2..6]);                         // 6to4 embeds an IPv4 address
        if (b[0] == 0 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b) return IsPublicV4(b[12..16]); // NAT64 too
        if (b.Take(12).All(x => x == 0)) return false;                                        // IPv4-compatible (deprecated)

        // Global unicast is 2000::/3; everything else is reserved.
        return (b[0] & 0xE0) == 0x20;
    }
}
