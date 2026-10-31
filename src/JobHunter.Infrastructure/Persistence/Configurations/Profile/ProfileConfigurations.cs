using JobHunter.Domain.Identity;
using JobHunter.Domain.Profile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobHunter.Infrastructure.Persistence.Configurations.Profile;

internal sealed class CandidateProfileConfiguration : IEntityTypeConfiguration<CandidateProfile>
{
    public void Configure(EntityTypeBuilder<CandidateProfile> builder)
    {
        builder.ToTable("profiles");
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => p.UserId).IsUnique();
        builder.HasOne<User>().WithOne().HasForeignKey<CandidateProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.Headline).HasMaxLength(120);
        builder.Property(p => p.Summary).HasMaxLength(2000);
        builder.Property(p => p.Location).HasMaxLength(120);
        builder.Property(p => p.Seniority).HasMaxLength(20);
        builder.OwnsMany(p => p.Languages, languages => languages.ToJson());

        // xmin: PostgreSQL's row version, so stale writes fail instead of overwriting.
        builder.Property(p => p.Version).IsRowVersion();
    }
}

internal sealed class ProfileSkillConfiguration : IEntityTypeConfiguration<ProfileSkill>
{
    public void Configure(EntityTypeBuilder<ProfileSkill> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.UserId, s.Key }).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(s => s.Name).HasMaxLength(60).IsRequired();
        builder.Property(s => s.Key).HasMaxLength(120).IsRequired();
        builder.Property(s => s.Category).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Years).HasPrecision(3, 1);
        builder.Property(s => s.Version).IsRowVersion();
    }
}

internal sealed class EvidenceItemConfiguration : IEntityTypeConfiguration<EvidenceItem>
{
    public void Configure(EntityTypeBuilder<EvidenceItem> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.UserId);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.Kind).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Organization).HasMaxLength(120);
        builder.Property(e => e.Description).HasMaxLength(4000);
        builder.Property(e => e.Outcome).HasMaxLength(1000);
        builder.OwnsMany(e => e.Links, links => links.ToJson());

        // uuid[]; a GIN index would serve "which items cite skill X" if it ever gets slow.
        builder.Property(e => e.SkillIds);
        builder.Property(e => e.Version).IsRowVersion();
    }
}
