using JobHunter.Domain.Contacts;
using JobHunter.Domain.FollowUps;
using JobHunter.Domain.Identity;
using JobHunter.Domain.Integrations;
using JobHunter.Domain.Opportunities;
using JobHunter.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobHunter.Infrastructure.Persistence.Configurations.FollowUps;

internal sealed class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.HasKey(r => r.Id);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Opportunity>().WithMany().HasForeignKey(r => r.OpportunityId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Contact>().WithMany().HasForeignKey(r => r.ContactId).OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(r => r.EffectiveDueAt);

        builder.Property(r => r.Title).HasMaxLength(FollowUpVocabulary.MaxTitle).IsRequired();
        builder.Property(r => r.Kind).HasMaxLength(20).IsRequired();
        builder.Property(r => r.Status).HasMaxLength(20).IsRequired();
        builder.Property(r => r.Origin).HasMaxLength(10).IsRequired();
        builder.Property(r => r.RuleKey).HasMaxLength(40);
        builder.Property(r => r.Version).IsRowVersion();

        // A rule fires once per opportunity, even if two deliveries of one event race.
        builder.HasIndex(r => new { r.OpportunityId, r.RuleKey }).IsUnique().HasFilter("rule_key IS NOT NULL");
        builder.HasIndex(r => new { r.UserId, r.Status, r.DueAt });
        builder.HasIndex(r => r.ContactId);
        // The due-reminders pass: open ones not yet notified.
        builder.HasIndex(r => r.DueAt).HasFilter("status = 'open' AND notified_at IS NULL");
    }
}

internal sealed class DraftConfiguration : IEntityTypeConfiguration<Draft>
{
    public void Configure(EntityTypeBuilder<Draft> builder)
    {
        builder.HasKey(d => d.Id);
        builder.HasOne<User>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Opportunity>().WithMany().HasForeignKey(d => d.OpportunityId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Contact>().WithMany().HasForeignKey(d => d.ContactId).OnDelete(DeleteBehavior.SetNull);
        builder.Ignore(d => d.IsClosed);

        builder.Property(d => d.Kind).HasMaxLength(20).IsRequired();
        builder.Property(d => d.ToEmail).HasMaxLength(254);
        builder.Property(d => d.Subject).HasMaxLength(FollowUpVocabulary.MaxSubject).IsRequired();
        builder.Property(d => d.Body).HasMaxLength(FollowUpVocabulary.MaxBody).IsRequired();
        builder.Property(d => d.Origin).HasMaxLength(10).IsRequired();
        builder.Property(d => d.Status).HasMaxLength(20).IsRequired();
        builder.Property(d => d.SentVia).HasMaxLength(20);
        builder.Property(d => d.OutreachSendId).HasMaxLength(100);
        builder.Property(d => d.Version).IsRowVersion();

        builder.HasIndex(d => new { d.UserId, d.Status });
        builder.HasIndex(d => d.OpportunityId);
        builder.HasIndex(d => d.ContactId);
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.HasOne<User>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Reminder>().WithMany().HasForeignKey(n => n.ReminderId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(n => n.Title).HasMaxLength(FollowUpVocabulary.MaxTitle).IsRequired();
        builder.HasIndex(n => new { n.UserId, n.CreatedAt });
    }
}

internal sealed class OutreachConnectionConfiguration : IEntityTypeConfiguration<OutreachConnection>
{
    public void Configure(EntityTypeBuilder<OutreachConnection> builder)
    {
        builder.HasKey(c => c.Id);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => c.UserId).IsUnique();

        builder.Property(c => c.BaseUrl).HasMaxLength(300).IsRequired();
        builder.Property(c => c.KeyHint).HasMaxLength(4).IsRequired();
        builder.Property(c => c.Version).IsRowVersion();
    }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Type).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(2000);
        // What the worker claims: unprocessed, unparked, due.
        builder.HasIndex(m => new { m.NextAttemptAt, m.OccurredAt }).HasFilter("processed_at IS NULL AND parked_at IS NULL");
    }
}

internal sealed class InboxReceiptConfiguration : IEntityTypeConfiguration<InboxReceipt>
{
    public void Configure(EntityTypeBuilder<InboxReceipt> builder)
    {
        builder.HasKey(r => new { r.MessageId, r.Handler });
        builder.Property(r => r.Handler).HasMaxLength(300);
    }
}
