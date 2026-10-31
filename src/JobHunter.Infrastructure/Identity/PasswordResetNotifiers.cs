using JobHunter.Application.Identity;
using Microsoft.Extensions.Logging;

namespace JobHunter.Infrastructure.Identity;

/// <summary>
/// Development stand-in for email: logs a ready-to-open reset link. Opt-in through
/// <c>Auth:LogPasswordResetLinks</c>, so it can't become the production default by accident.
/// </summary>
internal sealed class LoggingPasswordResetNotifier(ILogger<LoggingPasswordResetNotifier> logger) : IPasswordResetNotifier
{
    public Task SendAsync(string email, string resetUrl, CancellationToken cancellationToken)
    {
        // Logged as ResetUrl: the secret scrubber redacts properties named "token".
        logger.LogInformation("Password reset requested for {Email}. Open: {ResetUrl}", email, resetUrl);
        return Task.CompletedTask;
    }
}

/// <summary>
/// The default when nothing can deliver reset links. The endpoint must answer the same
/// for known and unknown addresses, so without this log line a missing channel would be silent.
/// </summary>
internal sealed class UnconfiguredPasswordResetNotifier(ILogger<UnconfiguredPasswordResetNotifier> logger) : IPasswordResetNotifier
{
    public Task SendAsync(string email, string resetUrl, CancellationToken cancellationToken)
    {
        logger.LogError("No password reset channel is configured; the reset for {Email} was dropped.", email);
        return Task.CompletedTask;
    }
}
