using JobHunter.Domain.Contacts;
using JobHunter.Domain.Identity;
using JobHunter.Domain.Opportunities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobHunter.Infrastructure.Persistence.Configurations.Contacts;

internal sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.HasKey(c => c.Id);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        // NO ACTION, not RESTRICT: when a user row goes, both cascades finish before the check runs.
        builder.HasOne<Company>().WithMany().HasForeignKey(c => c.CompanyId).OnDelete(DeleteBehavior.NoAction);

        builder.Property(c => c.Name).HasMaxLength(ContactVocabulary.MaxName).IsRequired();
        builder.Property(c => c.Role).HasMaxLength(ContactVocabulary.MaxRole);
        // citext: "Ada@x.com" and "ada@x.com" are the same person.
        builder.Property(c => c.Email).HasColumnType("citext").HasMaxLength(ContactVocabulary.MaxEmail);
        builder.Property(c => c.LinkedInUrl).HasColumnName("linkedin_url").HasMaxLength(ContactVocabulary.MaxLinkedInUrl);
        builder.Property(c => c.Phone).HasMaxLength(ContactVocabulary.MaxPhone);
        builder.Property(c => c.Notes).HasMaxLength(ContactVocabulary.MaxNotes);
        builder.Property(c => c.Version).IsRowVersion();

        builder.HasIndex(c => new { c.UserId, c.Name });
        builder.HasIndex(c => new { c.UserId, c.Email }).IsUnique().HasFilter("email IS NOT NULL");
        builder.HasIndex(c => c.CompanyId);
    }
}

internal sealed class ContactOpportunityConfiguration : IEntityTypeConfiguration<ContactOpportunity>
{
    public void Configure(EntityTypeBuilder<ContactOpportunity> builder)
    {
        builder.HasKey(l => new { l.ContactId, l.OpportunityId });
        builder.HasOne<User>().WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Contact>().WithMany().HasForeignKey(l => l.ContactId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Opportunity>().WithMany().HasForeignKey(l => l.OpportunityId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(l => l.Relation).HasMaxLength(20).IsRequired();
        builder.HasIndex(l => l.OpportunityId);
        builder.HasIndex(l => l.UserId);
    }
}

internal sealed class InteractionConfiguration : IEntityTypeConfiguration<Interaction>
{
    public void Configure(EntityTypeBuilder<Interaction> builder)
    {
        builder.HasKey(i => i.Id);
        builder.HasOne<User>().WithMany().HasForeignKey(i => i.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Contact>().WithMany().HasForeignKey(i => i.ContactId).OnDelete(DeleteBehavior.Cascade);
        // The log outlives the link: what was said stays when an opportunity goes.
        builder.HasOne<Opportunity>().WithMany().HasForeignKey(i => i.OpportunityId).OnDelete(DeleteBehavior.SetNull);

        builder.Property(i => i.Channel).HasMaxLength(20).IsRequired();
        builder.Property(i => i.Direction).HasMaxLength(10).IsRequired();
        builder.Property(i => i.Summary).HasMaxLength(ContactVocabulary.MaxSummary).IsRequired();

        builder.HasIndex(i => new { i.ContactId, i.OccurredAt });
        builder.HasIndex(i => new { i.OpportunityId, i.OccurredAt }).HasFilter("opportunity_id IS NOT NULL");
        builder.HasIndex(i => i.UserId);
    }
}
