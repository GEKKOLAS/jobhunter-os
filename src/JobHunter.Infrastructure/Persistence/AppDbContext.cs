using JobHunter.Application.Analytics;
using JobHunter.Application.Contacts;
using JobHunter.Application.FollowUps;
using JobHunter.Application.Identity;
using JobHunter.Application.Integrations;
using JobHunter.Application.Matching;
using JobHunter.Application.Opportunities;
using JobHunter.Application.Profile;
using JobHunter.Domain.Contacts;
using JobHunter.Domain.FollowUps;
using JobHunter.Domain.Identity;
using JobHunter.Domain.Integrations;
using JobHunter.Domain.Matching;
using JobHunter.Domain.Opportunities;
using JobHunter.Domain.Profile;
using JobHunter.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Infrastructure.Persistence;

/// <summary>
/// One database, one context. It implements each module's narrow port
/// (<see cref="IIdentityDbContext"/>, …), so handlers only see their own module's tables
/// (spec 02 §3). Configurations are registered from this assembly.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IIdentityDbContext, IProfileDbContext, IOpportunitiesDbContext, IMatchingDbContext, IContactsDbContext,
        IFollowUpsDbContext, IIntegrationsDbContext, IAnalyticsDbContext
{
    // Identity
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Profile
    public DbSet<CandidateProfile> Profiles => Set<CandidateProfile>();
    public DbSet<ProfileSkill> ProfileSkills => Set<ProfileSkill>();
    public DbSet<EvidenceItem> EvidenceItems => Set<EvidenceItem>();

    // Opportunities
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<OpportunityStageChange> OpportunityStageChanges => Set<OpportunityStageChange>();
    public DbSet<JobSource> JobSources => Set<JobSource>();

    // Matching
    public DbSet<OpportunityRequirement> OpportunityRequirements => Set<OpportunityRequirement>();
    public DbSet<FitAssessment> FitAssessments => Set<FitAssessment>();

    // Contacts
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<ContactOpportunity> ContactOpportunities => Set<ContactOpportunity>();
    public DbSet<Interaction> Interactions => Set<Interaction>();

    // FollowUps
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<Draft> Drafts => Set<Draft>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // Integrations
    public DbSet<OutreachConnection> OutreachConnections => Set<OutreachConnection>();

    // Module events (spec 02 §4)
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxReceipt> InboxReceipts => Set<InboxReceipt>();

    // Analytics reads the other modules' tables, untracked, so it can't write (spec 02 §3).
    IQueryable<Opportunity> IAnalyticsDbContext.Opportunities => Opportunities.AsNoTracking();
    IQueryable<OpportunityStageChange> IAnalyticsDbContext.OpportunityStageChanges => OpportunityStageChanges.AsNoTracking();
    IQueryable<CandidateProfile> IAnalyticsDbContext.Profiles => Profiles.AsNoTracking();
    IQueryable<ProfileSkill> IAnalyticsDbContext.ProfileSkills => ProfileSkills.AsNoTracking();
    IQueryable<OpportunityRequirement> IAnalyticsDbContext.OpportunityRequirements => OpportunityRequirements.AsNoTracking();
    IQueryable<FitAssessment> IAnalyticsDbContext.FitAssessments => FitAssessments.AsNoTracking();
    IQueryable<Reminder> IAnalyticsDbContext.Reminders => Reminders.AsNoTracking();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // citext: case-insensitive emails and company names without lower() everywhere.
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
