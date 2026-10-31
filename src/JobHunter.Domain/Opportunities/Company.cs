using JobHunter.Domain.Common;

namespace JobHunter.Domain.Opportunities;

/// <summary>An employer. One per name per user (case-insensitive), and one per web domain.</summary>
public sealed class Company : IHasTimestamps
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required string Name { get; set; }
    public string? Website { get; private set; }

    /// <summary>The website's host, lowercased, without <c>www.</c>.</summary>
    public string? Domain { get; private set; }

    public string? Notes { get; set; }

    public uint Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public void SetWebsite(string? website)
    {
        Website = string.IsNullOrWhiteSpace(website) ? null : website.Trim();
        Domain = DomainOf(Website);
    }

    /// <summary>"https://www.Contoso.com/careers" → "contoso.com"; null when there's no usable host.</summary>
    public static string? DomainOf(string? website)
    {
        if (string.IsNullOrWhiteSpace(website)) return null;
        var candidate = website.Contains("://", StringComparison.Ordinal) ? website.Trim() : $"https://{website.Trim()}";
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || uri.Host.Length == 0) return null;

        var host = uri.IdnHost.ToLowerInvariant();
        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }
}
