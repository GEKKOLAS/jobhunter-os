using JobHunter.Domain.FollowUps;
using JobHunter.Domain.Matching;
using JobHunter.Domain.Opportunities;
using JobHunter.Domain.Profile;

namespace JobHunter.Application.Analytics;

/// <summary>
/// What Analytics reads (spec 02 §3: the one module allowed to query other modules' entities).
/// Queries only, untracked: nothing here can write.
/// </summary>
public interface IAnalyticsDbContext
{
    IQueryable<Opportunity> Opportunities { get; }
    IQueryable<OpportunityStageChange> OpportunityStageChanges { get; }
    IQueryable<CandidateProfile> Profiles { get; }
    IQueryable<ProfileSkill> ProfileSkills { get; }
    IQueryable<OpportunityRequirement> OpportunityRequirements { get; }
    IQueryable<FitAssessment> FitAssessments { get; }
    IQueryable<Reminder> Reminders { get; }
}
