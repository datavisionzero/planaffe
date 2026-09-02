using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planaffe.Domain.Identities;
using Planaffe.Domain.Projects;

namespace Planaffe.Infrastructure.Persistence.Configurations;

/// <inheritdoc cref="IdentityConfiguration"/>
public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("project");

        builder.HasKey(p => p.Id).HasName("pk_project");
        builder.Property(p => p.Id).HasColumnName("id");

        // Unique across deleted projects as well: while a deleted project waits
        // out its grace period, its key cannot be taken (docs/storage.md).
        builder.Property(p => p.Key).HasColumnName("key").IsRequired();
        builder.HasIndex(p => p.Key).IsUnique().HasDatabaseName("project_key");

        builder.Property(p => p.Name).HasColumnName("name").IsRequired();

        builder.Property(p => p.TriageRequired)
            .HasColumnName("triage_required")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(p => p.ReviewRequired)
            .HasColumnName("review_required")
            .HasDefaultValue(false)
            .IsRequired();

        // The two counters every key in the project is drawn from, incremented
        // by one statement under the row's lock in the store — never by the
        // change tracker, which is why nothing in Domain sets them.
        builder.Property(p => p.LastIssueNumber)
            .HasColumnName("last_issue_number")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(p => p.LastEpicNumber)
            .HasColumnName("last_epic_number")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(p => p.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.HasOne<Identity>()
            .WithMany()
            .HasForeignKey(p => p.CreatedBy)
            .HasConstraintName("fk_project_created_by")
            .OnDelete(DeleteBehavior.NoAction);

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at");
        builder.Property(p => p.DeletedBy).HasColumnName("deleted_by");
        builder.HasOne<Identity>()
            .WithMany()
            .HasForeignKey(p => p.DeletedBy)
            .HasConstraintName("fk_project_deleted_by")
            .OnDelete(DeleteBehavior.NoAction);

        builder.Ignore(p => p.Deleted);
    }
}
