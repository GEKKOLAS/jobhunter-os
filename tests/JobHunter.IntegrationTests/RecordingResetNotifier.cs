using System.Collections.Concurrent;
using JobHunter.Application.Identity;

namespace JobHunter.IntegrationTests;

/// <summary>Captures password-reset links so tests can follow them.</summary>
public sealed class RecordingResetNotifier : IPasswordResetNotifier
{
    private readonly ConcurrentDictionary<string, string> _lastUrlByEmail = new(StringComparer.OrdinalIgnoreCase);

    public Task SendAsync(string email, string resetUrl, CancellationToken cancellationToken)
    {
        _lastUrlByEmail[email] = resetUrl;
        return Task.CompletedTask;
    }

    public string? LastUrlFor(string email) => _lastUrlByEmail.GetValueOrDefault(email);

    /// <summary>The token from the last link sent to <paramref name="email"/>.</summary>
    public string? LastTokenFor(string email) =>
        LastUrlFor(email) is { } url ? Uri.UnescapeDataString(url[(url.IndexOf("token=", StringComparison.Ordinal) + 6)..]) : null;
}
