using JobHunter.Domain.Common;

namespace JobHunter.Domain.Opportunities;

/// <summary>One job the user is considering or pursuing, with where it stands in the pipeline.</summary>
public sealed class Opportunity : IHasTimestamps
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required Guid CompanyId { get; set; }

    public required string Title { get; set; }
    public string? Location { get; set; }
    public string? WorkMode { get; set; }
    public string? EmploymentType { get; set; }

    public int? SalaryMin { get; set; }
    public int? SalaryMax { get; set; }
    public string? SalaryCurrency { get; set; }
    public string? SalaryPeriod { get; set; }

    public required string Source { get; init; }
    public string? SourceUrl { get; set; }

    /// <summary>The posting's id at its source; re-importing the same ref updates instead of duplicating.</summary>
    public string? SourceRef { get; init; }

    /// <summary>Plain text, never rendered as HTML.</summary>
    public string Description { get; set; } = "";

    public string Stage { get; private set; } = Stages.Saved;

    /// <summary>Where the last move came from: the only way back out of a closed stage.</summary>
    public string? PreviousStage { get; private set; }

    public string Priority { get; set; } = "normal";
    public string? PrioritySetBy { get; set; }

    public DateTimeOffset? AppliedAt { get; private set; }
    public DateTimeOffset? FirstResponseAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; set; }

    public uint Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Company? Company { get; init; }

    /// <summary>Puts a new opportunity at its first stage (any open one: you may log a job you already applied to).</summary>
    public OpportunityStageChange Start(string stage, DateTimeOffset now)
    {
        if (!Stages.IsOpen(stage)) throw new DomainException("opportunity.invalid_stage", $"A new opportunity can't start at '{stage}'.");
        return Apply(from: null, stage, now, note: null);
    }

    /// <summary>Moves along the pipeline, enforcing <see cref="StageRules"/> and stamping the dates it implies.</summary>
    public OpportunityStageChange MoveTo(string stage, DateTimeOffset now, string? note)
    {
        if (!StageRules.CanMove(Stage, PreviousStage, stage))
        {
            throw new DomainException("opportunity.invalid_stage", $"Can't move from '{Stage}' to '{stage}'.");
        }
        return Apply(Stage, stage, now, note);
    }

    private OpportunityStageChange Apply(string? from, string stage, DateTimeOffset now, string? note)
    {
        if (StageRules.ImpliesApplied(stage)) AppliedAt ??= now;
        if (StageRules.ImpliesResponse(from, stage)) FirstResponseAt ??= now;
        ClosedAt = Stages.IsClosed(stage) ? now : null;

        PreviousStage = from;
        Stage = stage;
        return new OpportunityStageChange
        {
            OpportunityId = Id,
            UserId = UserId,
            FromStage = from,
            ToStage = stage,
            OccurredAt = now,
            Note = note,
        };
    }
}

/// <summary>One step in an opportunity's history. Append-only.</summary>
public sealed class OpportunityStageChange
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid OpportunityId { get; init; }
    public required Guid UserId { get; init; }

    /// <summary>Null for the first stage.</summary>
    public string? FromStage { get; init; }

    public required string ToStage { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public string? Note { get; init; }
}
