namespace JobHunter.Application.Common;

/// <summary>The Anthropic API (spec 02 §6). Without a key, everything AI has a non-AI path.</summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>From user secrets or the environment (<c>Ai__ApiKey</c>), never a file under version control.</summary>
    public string ApiKey { get; init; } = "";

    public string Model { get; init; } = "claude-opus-5-5";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
