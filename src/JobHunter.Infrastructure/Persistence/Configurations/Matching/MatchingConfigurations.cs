using JobHunter.Domain.Identity;
using JobHunter.Domain.Matching;
using JobHunter.Domain.Opportunities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobHunter.Infrastructure.Persistence.Configurations.Matching;

internal sealed class OpportunityRequirementConfiguration : IEntityTypeConfiguration<OpportunityRequirement>
{
    public void Configure(EntityTypeBuilder<OpportunityRequirement> builder)
    {
        builder.HasKey(r => r.Id);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Opportunity>().WithMany().HasForeignKey(r => r.OpportunityId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(r => r.Text).HasMaxLength(RequirementVocabulary.MaxText).IsRequired();
        builder.Property(r => r.Kind).HasMaxLength(10).IsRequired();
        builder.Property(r => r.SkillKey).HasMaxLength(RequirementVocabulary.MaxSkillName + 20);
        builder.Property(r => r.Origin).HasMaxLength(10).IsRequired();

        builder.HasIndex(r => new { r.OpportunityId, r.Position });
        // Every board and list read: a user's confirmed requirements.
        builder.HasIndex(r => r.UserId).HasFilter("confirmed");
    }
}

internal sealed class FitAssessmentConfiguration : IEntityTypeConfiguration<FitAssessment>
{
    public void Configure(EntityTypeBuilder<FitAssessment> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Opportunity>().WithMany().HasForeignKey(a => a.OpportunityId).OnDelete(DeleteBehavior.Cascade);

        // One current row per opportunity.
        builder.HasIndex(a => a.OpportunityId).IsUnique();
        builder.HasIndex(a => a.UserId);

        builder.Property(a => a.Band).HasMaxLength(10);
        builder.Property(a => a.MissingReason).HasMaxLength(30);
        builder.OwnsMany(a => a.Outcomes, outcomes => outcomes.ToJson());
        builder.OwnsMany(a => a.Notes, notes => notes.ToJson());
    }
}
