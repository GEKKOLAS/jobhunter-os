namespace JobHunter.Application.Common;

/// <summary>The only source of "now", so time-based rules (reminders, staleness) are testable.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
