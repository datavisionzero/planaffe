using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planaffe.Domain.Identities;
using Planaffe.Domain.Spaces;

namespace Planaffe.Infrastructure.Persistence.Configurations;

/// <summary>
/// The bracket of the knowledge base (<c>docs/storage.md</c>, Spaces). The
/// unique index covers deleted rows on purpose, for the reason the page's slug
/// index does: a name stays spent until the purge, so that restoring a space
/// never lands on a name somebody else has taken (ADR 0013).
/// </summary>
public sealed class SpaceConfiguration : IEntityTypeConfiguration<Space>
{
    public void Configure(EntityTypeBuilder<Space> builder)
    {
        builder.ToTable("space");

        builder.HasKey(s => s.Id).HasName("pk_space");
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.Name).HasColumnName("name").IsRequired();

        // Unique across the instance: there is no bracket above a space in
        // which the name could be unique instead (ADR 0027).
        builder.HasIndex(s => s.Name).IsUnique().HasDatabaseName("space_name");

        builder.Property(s => s.Title).HasColumnName("title").IsRequired();

        builder.Property(s => s.ClosedToAgents)
            .HasColumnName("closed_to_agents")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(s => s.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.HasOne<Identity>()
            .WithMany()
            .HasForeignKey(s => s.CreatedBy)
            .HasConstraintName("fk_space_created_by")
            .OnDelete(DeleteBehavior.NoAction);

        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");

        builder.Property(s => s.DeletedBy).HasColumnName("deleted_by");
        builder.HasOne<Identity>()
            .WithMany()
            .HasForeignKey(s => s.DeletedBy)
            .HasConstraintName("fk_space_deleted_by")
            .OnDelete(DeleteBehavior.NoAction);

        builder.Ignore(s => s.Deleted);
    }
}

/// <inheritdoc cref="ProjectAccessConfiguration"/>
public sealed class SpaceAccessConfiguration : IEntityTypeConfiguration<SpaceAccess>
{
    public void Configure(EntityTypeBuilder<SpaceAccess> builder)
    {
        builder.ToTable("space_access");
        builder.HasKey(access => new { access.SpaceId, access.UserId }).HasName("pk_space_access");
        builder.HasIndex(access => access.UserId).HasDatabaseName("space_access_user");
        builder.Property(access => access.SpaceId).HasColumnName("space_id");
        builder.Property(access => access.UserId).HasColumnName("user_id");
        builder.Property(access => access.GrantedBy).HasColumnName("granted_by");
        builder.Property(access => access.GrantedAt).HasColumnName("granted_at");
        builder.HasOne<Space>().WithMany().HasForeignKey(access => access.SpaceId)
            .HasConstraintName("fk_space_access_space").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(access => access.UserId)
            .HasConstraintName("fk_space_access_user").OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<User>().WithMany().HasForeignKey(access => access.GrantedBy)
            .HasConstraintName("fk_space_access_granted_by").OnDelete(DeleteBehavior.NoAction);
    }
}
