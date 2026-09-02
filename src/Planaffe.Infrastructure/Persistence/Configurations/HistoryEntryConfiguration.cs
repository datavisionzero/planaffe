using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planaffe.Domain.Epics;
using Planaffe.Domain.History;
using Planaffe.Domain.Identities;
using Planaffe.Domain.Issues;

namespace Planaffe.Infrastructure.Persistence.Configurations;

/// <summary>
/// An issue's history and an epic's in one table, every row pointing at exactly
/// one of the two, and dying with it (ADR 0013).
/// </summary>
public sealed class HistoryEntryConfiguration : IEntityTypeConfiguration<HistoryEntry>
{
    public void Configure(EntityTypeBuilder<HistoryEntry> builder)
    {
        builder.ToTable("history", table =>
            table.HasCheckConstraint("ck_history_subject", "num_nonnulls(issue_id, epic_id) = 1"));

        // Always generated, so that the order of the ids is the order the rows
        // were written and nothing can insert one out of sequence.
        builder.HasKey(h => h.Id).HasName("pk_history");
        builder.Property(h => h.Id).HasColumnName("id").UseIdentityAlwaysColumn();

        builder.Property(h => h.IssueId).HasColumnName("issue_id");
        builder.HasOne<Issue>()
            .WithMany()
            .HasForeignKey(h => h.IssueId)
            .HasConstraintName("fk_history_issue")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(h => h.EpicId).HasColumnName("epic_id");
        builder.HasOne<Epic>()
            .WithMany()
            .HasForeignKey(h => h.EpicId)
            .HasConstraintName("fk_history_epic")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(h => h.ActorId).HasColumnName("actor_id").IsRequired();
        builder.HasOne<Identity>()
            .WithMany()
            .HasForeignKey(h => h.ActorId)
            .HasConstraintName("fk_history_actor")
            .OnDelete(DeleteBehavior.NoAction);

        builder.Property(h => h.At).HasColumnName("at").IsRequired();
        builder.Property(h => h.Field).HasColumnName("field").IsRequired();
        builder.Property(h => h.OldValue).HasColumnName("old_value");
        builder.Property(h => h.NewValue).HasColumnName("new_value");
        builder.Property(h => h.Note).HasColumnName("note");

        builder.HasIndex(h => new { h.IssueId, h.Id }).HasDatabaseName("history_issue");
        builder.HasIndex(h => new { h.EpicId, h.Id }).HasDatabaseName("history_epic");
    }
}
