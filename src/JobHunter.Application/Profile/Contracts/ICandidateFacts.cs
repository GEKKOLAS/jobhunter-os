namespace JobHunter.Application.Profile.Contracts;

public sealed record CandidateSkill(string Key, string Name, int Level, decimal Years);

/// <summary>What another module may know about the signed-in user's profile.</summary>
public sealed record CandidateFacts(
    IReadOnlyList<CandidateSkill> Skills, string? Seniority, IReadOnlyList<string> WorkModes, string Location);

public interface ICandidateFacts
{
    /// <summary>Empty facts when the user hasn't filled anything in yet.</summary>
    Task<CandidateFacts> ForCurrentUserAsync(CancellationToken ct);
}
