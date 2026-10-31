namespace JobHunter.Domain.Opportunities;

/// <summary>
/// Which stage moves are allowed (spec 03, Opportunities, the "Allowed moves" table) and
/// what each move stamps. Pure, so the whole table is unit-tested.
/// </summary>
public static class StageRules
{
    private static readonly HashSet<string> NoResponseFrom = [Stages.Applied, Stages.Screening, Stages.Interviewing];

    /// <summary>Every stage the opportunity may move to from where it is now.</summary>
    /// <param name="previous">Where the last move came from; the only way back out of a closed stage.</param>
    public static IReadOnlyList<string> AllowedMoves(string current, string? previous)
    {
        if (Stages.IsClosed(current))
        {
            return previous is not null && Stages.IsOpen(previous) ? [previous] : [];
        }

        var index = Stages.Open.ToList().IndexOf(current);
        if (index < 0) return [];

        var moves = new List<string>();
        if (index > 0) moves.Add(Stages.Open[index - 1]);   // a one-step correction
        moves.AddRange(Stages.Open.Skip(index + 1));         // forward, skipping allowed
        if (current == Stages.Offer) moves.AddRange([Stages.Accepted, Stages.Declined]);
        moves.AddRange([Stages.Rejected, Stages.Withdrawn]);
        if (NoResponseFrom.Contains(current)) moves.Add(Stages.NoResponse);
        return moves;
    }

    public static bool CanMove(string current, string? previous, string target) =>
        AllowedMoves(current, previous).Contains(target);

    /// <summary>Reaching this stage means an application went out.</summary>
    public static bool ImpliesApplied(string stage) =>
        stage is Stages.Applied or Stages.Screening or Stages.Interviewing or Stages.Offer or Stages.Accepted or Stages.Declined;

    /// <summary>Reaching this stage (coming from <paramref name="from"/>) means the employer answered.</summary>
    public static bool ImpliesResponse(string? from, string stage) =>
        stage is Stages.Screening or Stages.Interviewing or Stages.Offer or Stages.Accepted or Stages.Declined
        || (stage == Stages.Rejected && from is not null && ImpliesApplied(from));
}
