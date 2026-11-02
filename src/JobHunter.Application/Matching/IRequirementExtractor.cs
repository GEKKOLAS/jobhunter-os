namespace JobHunter.Application.Matching;

/// <param name="SkillKey">The catalog key, or null when the requirement isn't about one skill.</param>
public sealed record ExtractedRequirement(string Text, string Kind, string? SkillKey, int? YearsMin);

/// <summary>Reads what a posting asks for (spec 02 §6). Its rows are drafts: the user confirms them.</summary>
public interface IRequirementExtractor
{
    /// <summary><c>rules</c> or <c>ai</c>: the origin its rows are labelled with.</summary>
    string Origin { get; }

    /// <exception cref="AiUnavailableException">An AI extractor couldn't produce a valid answer.</exception>
    Task<IReadOnlyList<ExtractedRequirement>> ExtractAsync(string postingText, CancellationToken ct);
}

/// <summary>The model failed, declined, or answered something invalid. The rules take over; it never reaches the user as an error.</summary>
public sealed class AiUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
