using JobHunter.Domain.Opportunities;

namespace JobHunter.Application.Opportunities;

public sealed record CompanyDto(Guid Id, string Name, string? Website, string? Domain, string? Notes, uint Version)
{
    public static CompanyDto From(Company c) => new(c.Id, c.Name, c.Website, c.Domain, c.Notes, c.Version);
}

/// <summary>An opportunity as lists and the board show it.</summary>
/// <param name="AllowedMoves">From the server's rules, so the board never re-derives them.</param>
public sealed record OpportunityCardDto(
    Guid Id, string Title, Guid CompanyId, string CompanyName, string? Location, string? WorkMode, string Stage,
    IReadOnlyList<string> AllowedMoves, string Priority, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    DateTimeOffset? AppliedAt, DateTimeOffset? ArchivedAt)
{
    public static OpportunityCardDto From(Opportunity o, string companyName) => new(
        o.Id, o.Title, o.CompanyId, companyName, o.Location, o.WorkMode, o.Stage,
        o.ArchivedAt is null ? StageRules.AllowedMoves(o.Stage, o.PreviousStage) : [],
        o.Priority, o.CreatedAt, o.UpdatedAt, o.AppliedAt, o.ArchivedAt);
}

/// <param name="AllowedMoves">Where the stage can go from here (spec 03, Opportunities), so clients don't re-derive the rules.</param>
public sealed record OpportunityDto(
    Guid Id, string Title, CompanyDto Company, string? Location, string? WorkMode, string? EmploymentType,
    int? SalaryMin, int? SalaryMax, string? SalaryCurrency, string? SalaryPeriod,
    string Source, string? SourceUrl, string Description, string Stage, string? PreviousStage,
    IReadOnlyList<string> AllowedMoves, string Priority, string? PrioritySetBy,
    DateTimeOffset? AppliedAt, DateTimeOffset? FirstResponseAt, DateTimeOffset? ClosedAt, DateTimeOffset? ArchivedAt,
    uint Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static OpportunityDto From(Opportunity o, Company company) => new(
        o.Id, o.Title, CompanyDto.From(company), o.Location, o.WorkMode, o.EmploymentType,
        o.SalaryMin, o.SalaryMax, o.SalaryCurrency, o.SalaryPeriod,
        o.Source, o.SourceUrl, o.Description, o.Stage, o.PreviousStage,
        o.ArchivedAt is null ? StageRules.AllowedMoves(o.Stage, o.PreviousStage) : [],
        o.Priority, o.PrioritySetBy, o.AppliedAt, o.FirstResponseAt, o.ClosedAt, o.ArchivedAt,
        o.Version, o.CreatedAt, o.UpdatedAt);
}

/// <param name="Created">False when it updated an opportunity already saved under the same source ref.</param>
public sealed record SavedOpportunity(OpportunityDto Opportunity, bool Created);

public sealed record StageChangeDto(Guid Id, string? FromStage, string ToStage, DateTimeOffset OccurredAt, string? Note)
{
    public static StageChangeDto From(OpportunityStageChange c) => new(c.Id, c.FromStage, c.ToStage, c.OccurredAt, c.Note);
}

public sealed record BoardColumnDto(string Stage, IReadOnlyList<OpportunityCardDto> Items);

/// <param name="ClosedCounts">How many (unarchived) opportunities ended in each closed stage.</param>
public sealed record BoardDto(IReadOnlyList<BoardColumnDto> Columns, IReadOnlyDictionary<string, int> ClosedCounts);

/// <summary>Audit action codes of the Opportunities module.</summary>
public static class OpportunitiesAudit
{
    public const string Created = "opportunity.created";
    public const string Updated = "opportunity.updated";
    public const string Reimported = "opportunity.reimported";
    public const string SourceCreated = "source.created";
    public const string SourceDeleted = "source.deleted";
    public const string StageChanged = "opportunity.stage_changed";
    public const string Archived = "opportunity.archived";
    public const string Restored = "opportunity.restored";
    public const string CompanyCreated = "company.created";
    public const string CompanyUpdated = "company.updated";
}
