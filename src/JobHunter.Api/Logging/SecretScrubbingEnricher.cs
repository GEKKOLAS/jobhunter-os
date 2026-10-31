using Serilog.Core;
using Serilog.Events;

namespace JobHunter.Api.Logging;

/// <summary>Redacts secret-bearing log properties: tokens, passwords, keys and Authorization headers never reach a sink.</summary>
public sealed class SecretScrubbingEnricher : ILogEventEnricher
{
    private static readonly HashSet<string> SecretKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "token", "sessionToken", "csrfToken", "password", "newPassword", "currentPassword",
        "apiKey", "api_key", "Authorization", "access_token", "refresh_token",
    };

    private const string Redacted = "***redacted***";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var property in logEvent.Properties.ToArray())
        {
            if (SecretKeys.Contains(property.Key))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(property.Key, new ScalarValue(Redacted)));
            }
        }
    }
}
