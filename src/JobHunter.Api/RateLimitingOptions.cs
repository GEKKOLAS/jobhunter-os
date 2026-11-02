namespace JobHunter.Api;

/// <summary>Fixed-window limits per client IP (spec 02 §5). Policies are added with the phase that needs them.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public PolicyOptions Auth { get; init; } = new() { PermitLimit = 10, WindowMinutes = 15 };

    /// <summary>Anything that calls out to another site on the user's behalf.</summary>
    public PolicyOptions Import { get; init; } = new() { PermitLimit = 60, WindowMinutes = 60 };

    /// <summary>Calls that spend the Anthropic budget. Applies only when a key is configured.</summary>
    public PolicyOptions Ai { get; init; } = new() { PermitLimit = 30, WindowMinutes = 60 };

    public sealed class PolicyOptions
    {
        public int PermitLimit { get; init; } = 10;
        public int WindowMinutes { get; init; } = 15;
    }
}
