using JobHunter.Application.Contacts;
using JobHunter.Application.Identity;
using JobHunter.Application.Matching;
using JobHunter.Application.Opportunities;
using JobHunter.Application.Profile;
using JobHunter.Domain.Contacts;
using JobHunter.Domain.Identity;
using JobHunter.Domain.Matching;
using JobHunter.Domain.Opportunities;
using JobHunter.Domain.Profile;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Infrastructure.Persistence;

/// <summary>
/// One database, one context. It implements each module's narrow port
/// (<see cref="IIdentityDbContext"/>, …), so handlers only see their own module's tables
/// (spec 02 §3). Configurations are registered from this assembly.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IIdentityDbContext, IProfileDbContext, IOpportunitiesDbContext, IMatchingDbContext, IContactsDbContext
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // citext: case-insensitive emails and company names without lower() everywhere.
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
