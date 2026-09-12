using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planaffe.Domain.Identities;
using Planaffe.Domain.Spaces;

namespace Planaffe.Infrastructure.Persistence.Configurations;

/// <summary>
/// The knowledge base's page and its place in the tree (<c>docs/storage.md</c>,
/// Space pages). The unique index covers deleted rows on purpose, as the
/// project page's does: a slug stays spent until the purge, so that restoring a
/// page never lands on a name somebody else has taken (ADR 0013).
/// </summary>
/// <remarks>
/// Two rules the model states twice on purpose. The depth is a check
/// constraint as well as a rule of <see cref="SpacePage"/>, because a limit
/// that only one of the two holds is a limit one forgotten act removes. And
/// the slug is unique under the parent rather than in the space (ADR 0028),
/// which needs <c>nulls not distinct</c>: the pages directly under a space
/// carry no parent, and without it Postgres would let every one of them repeat
/// the same slug.
/// </remarks>
public sealed class SpacePageConfiguration : IEntityTypeConfiguration<SpacePage>
{
    public void Configure(EntityTypeBuilder<SpacePage> builder)
    {
        builder.ToTable("space_page", table => table.HasCheckConstraint(
            "ck_space_page_depth", $"depth >= 0 and depth <= {SpacePage.MaxDepth}"));

        builder.HasKey(p => p.Id).HasName("pk_space_page");
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.SpaceId).HasColumnName("space_id").IsRequired();
        builder.HasOne<Space>()
            .WithMany()
            .HasForeignKey(p => p.SpaceId)
            .HasConstraintName("fk_space_page_space")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.ParentId).HasColumnName("parent_id");
        builder.HasOne<SpacePage>()
            .WithMany()
            .HasForeignKey(p => p.ParentId)
            .HasConstraintName("fk_space_page_parent")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.Depth).HasColumnName("depth").IsRequired();

        builder.Property(p => p.Slug).HasColumnName("slug").IsRequired();

        // One index for both jobs: it holds the slug unique under its parent,
        // and it is how a space's tree is read — by the space, then level by
        // level down the parents.
        builder.HasIndex(p => new { p.SpaceId, p.ParentId, p.Slug })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("space_page_slug");

        builder.Property(p => p.Title).HasColumnName("title").IsRequired();

        builder.Property(p => p.Body)
            .HasColumnName("body")
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(p => p.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.HasOne<Identity>()
            .WithMany()
            .HasForeignKey(p => p.CreatedBy)
            .HasConstraintName("fk_space_page_created_by")
            .OnDelete(DeleteBehavior.NoAction);

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by").IsRequired();
        builder.HasOne<Identity>()
            .WithMany()
            .HasForeignKey(p => p.UpdatedBy)
            .HasConstraintName("fk_space_page_updated_by")
            .OnDelete(DeleteBehavior.NoAction);

        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at");

        builder.Property(p => p.DeletedBy).HasColumnName("deleted_by");
        builder.HasOne<Identity>()
            .WithMany()
            .HasForeignKey(p => p.DeletedBy)
            .HasConstraintName("fk_space_page_deleted_by")
            .OnDelete(DeleteBehavior.NoAction);

        // It points at a page, and it deliberately carries no foreign key of
        // its own: the page it names is always this row's ancestor, so the
        // parent's cascade already takes the row with it. A second
        // self-reference would buy nothing and give the model two paths
        // between the same two rows to reason about.
        builder.Property(p => p.DeletedWith).HasColumnName("deleted_with");

        builder.Ignore(p => p.Deleted);
    }
}
