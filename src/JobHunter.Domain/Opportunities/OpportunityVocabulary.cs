namespace JobHunter.Domain.Opportunities;

/// <summary>The closed value sets of the Opportunities module (spec 03, Opportunities). Stored as text.</summary>
public static class OpportunityVocabulary
{
    public static readonly IReadOnlyList<string> WorkModes = ["remote", "hybrid", "onsite"];
    public static readonly IReadOnlyList<string> EmploymentTypes = ["full_time", "part_time", "contract", "internship"];
    public static readonly IReadOnlyList<string> Sources = ["manual", "paste", "url", "greenhouse", "lever", "ashby"];
    public static readonly IReadOnlyList<string> Priorities = ["low", "normal", "high"];
    public static readonly IReadOnlyList<string> SalaryPeriods = ["year", "month", "hour"];

    public const string PrioritySetByUser = "user";
    public const string PrioritySetBySuggestion = "suggested";
}

/// <summary>Stage names, in pipeline order for the open ones.</summary>
public static class Stages
{
    public const string Saved = "saved";
    public const string Preparing = "preparing";
    public const string Applied = "applied";
    public const string Screening = "screening";
    public const string Interviewing = "interviewing";
    public const string Offer = "offer";

    public const string Accepted = "accepted";
    public const string Declined = "declined";
    public const string Rejected = "rejected";
    public const string Withdrawn = "withdrawn";
    public const string NoResponse = "no_response";

    public static readonly IReadOnlyList<string> Open = [Saved, Preparing, Applied, Screening, Interviewing, Offer];
    public static readonly IReadOnlyList<string> Closed = [Accepted, Declined, Rejected, Withdrawn, NoResponse];
    public static readonly IReadOnlyList<string> All = [.. Open, .. Closed];

    public static bool IsOpen(string stage) => Open.Contains(stage);
    public static bool IsClosed(string stage) => Closed.Contains(stage);
}
