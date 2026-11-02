namespace JobHunter.Application.Matching.Contracts;

/// <summary>A fit as other screens show it: only ever with a score.</summary>
public sealed record FitScore(int Score, string Band);

public interface IFitScores
{
    /// <summary>The signed-in user's current scores by opportunity id, brought up to date first (spec 03, Matching).</summary>
    Task<IReadOnlyDictionary<Guid, FitScore>> CurrentAsync(CancellationToken ct);
}
