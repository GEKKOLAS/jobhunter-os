using JobHunter.Domain.Identity;
using JobHunter.Domain.Opportunities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobHunter.Infrastructure.Persistence.Configurations.Opportunities;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.HasKey(c => c.Id);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);

        // citext: "Contoso" and "contoso" are the same company.
        builder.Property(c => c.Name).HasColumnType("citext").HasMaxLength(120).IsRequired();
        builder.HasIndex(c => new { c.UserId, c.Name }).IsUnique();
        builder.Property(c => c.Website).HasMaxLength(2048);
        builder.Property(c => c.Domain).HasMaxLength(253);
        builder.HasIndex(c => new { c.UserId, c.Domain }).IsUnique().HasFilter("domain IS NOT NULL");
        builder.Property(c => c.Notes).HasMaxLength(2000);
        builder.Property(c => c.Version).IsRowVersion();
    }
}

internal sealed class OpportunityConfiguration : IEntityTypeConfiguration<Opportunity>
{
    public void Configure(EntityTypeBuilder<Opportunity> builder)
    {
        builder.HasKey(o => o.Id);
        builder.HasOne<User>().WithMany().HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Cascade);
        // NO ACTION, not RESTRICT: when a user row goes, both cascades finish before the check runs.
        builder.HasOne(o => o.Company).WithMany().HasForeignKey(o => o.CompanyId).OnDelete(DeleteBehavior.NoAction);

        builder.Property(o => o.Title).HasMaxLength(160).IsRequired();
        builder.Property(o => o.Location).HasMaxLength(120);
        builder.Property(o => o.WorkMode).HasMaxLength(20);
        builder.Property(o => o.EmploymentType).HasMaxLength(20);
        builder.Property(o => o.SalaryCurrency).HasMaxLength(3);
        builder.Property(o => o.SalaryPeriod).HasMaxLength(10);
        builder.Property(o => o.Source).HasMaxLength(20).IsRequired();
        builder.Property(o => o.SourceUrl).HasMaxLength(2048);
        builder.Property(o => o.SourceRef).HasMaxLength(200);
        builder.Property(o => o.Description).HasMaxLength(50_000);
        builder.Property(o => o.Stage).HasMaxLength(20).IsRequired();
        builder.Property(o => o.PreviousStage).HasMaxLength(20);
        builder.Property(o => o.Priority).HasMaxLength(10).IsRequired();
        builder.Property(o => o.PrioritySetBy).HasMaxLength(10);
        builder.Property(o => o.Version).IsRowVersion();
        builder.Ignore(o => o.DomainEvents);

        // The board and the default lists: a user's unarchived opportunities by stage.
        builder.HasIndex(o => new { o.UserId, o.Stage }).HasFilter("archived_at IS NULL");
        // Re-importing a posting updates it instead of adding a second copy (phase 4).
        builder.HasIndex(o => new { o.UserId, o.Source, o.SourceRef }).IsUnique().HasFilter("source_ref IS NOT NULL");
    }
}

internal sealed class OpportunityStageChangeConfiguration : IEntityTypeConfiguration<OpportunityStageChange>
{
    public void Configure(EntityTypeBuilder<OpportunityStageChange> builder)
    {
        builder.HasKey(c => c.Id);
        builder.HasOne<Opportunity>().WithMany().HasForeignKey(c => c.OpportunityId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(c => c.FromStage).HasMaxLength(20);
        builder.Property(c => c.ToStage).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Note).HasMaxLength(500);
        builder.HasIndex(c => new { c.OpportunityId, c.OccurredAt });
        // Analytics (phase 8) reads moves by user and time.
        builder.HasIndex(c => new { c.UserId, c.OccurredAt });
    }
}

internal sealed class JobSourceConfiguration : IEntityTypeConfiguration<JobSource>
{
    public void Configure(EntityTypeBuilder<JobSource> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(s => s.Provider).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Board).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Label).HasMaxLength(80).IsRequired();
        builder.HasIndex(s => new { s.UserId, s.Provider, s.Board }).IsUnique();
    }
}
