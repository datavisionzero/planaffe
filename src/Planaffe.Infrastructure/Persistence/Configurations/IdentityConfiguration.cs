using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planaffe.Domain.Identities;

namespace Planaffe.Infrastructure.Persistence.Configurations;

/// <summary>
/// One table for both kinds (<c>docs/storage.md</c>, Identities and tokens),
/// with the kind as the discriminator column. Names are written out rather than
/// derived from a convention, so that what is in the database reads the same as
/// the DDL in that document.
/// </summary>
/// <remarks>
/// The unique index on <c>lower(name)</c> is not here: EF Core has no model for
/// an expression index, so <c>identity_name</c> is SQL in the migration that
/// created the table. It exists, and this is where a reader learns that.
/// </remarks>
public sealed class IdentityConfiguration : IEntityTypeConfiguration<Identity>
{
    public void Configure(EntityTypeBuilder<Identity> builder)
    {
        builder.ToTable("identity", table =>
        {
            table.HasCheckConstraint("ck_identity_kind", "kind in ('user', 'agent')");

            // An agent has an owner and is never an administrator (ADR 0015); a
            // user has no owner. Held here, not only by the write path.
            table.HasCheckConstraint(
                "ck_identity_owner",
                "kind = 'user' and owner_id is null or kind = 'agent' and owner_id is not null and not administrator");
        });

        builder.HasKey(i => i.Id).HasName("pk_identity");
        builder.Property(i => i.Id).HasColumnName("id");

        builder.HasDiscriminator<string>("kind")
            .HasValue<User>("user")
            .HasValue<Agent>("agent");

        // `text`, as every other string column: the convention would size the
        // column to the longest value it knows, and a third kind would then be
        // a column change as well as a code change.
        builder.Property<string>("kind").HasColumnType("text");

        builder.Property(i => i.Name).HasColumnName("name").IsRequired();

        builder.Property(i => i.Administrator)
            .HasColumnName("administrator")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}

/// <inheritdoc cref="IdentityConfiguration"/>
public sealed class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        // Nullable in the column, because the column is shared with users;
        // required on every agent row, by the check constraint above.
        builder.Property(a => a.OwnerId).HasColumnName("owner_id");

        builder.HasOne<Identity>()
            .WithMany()
            .HasForeignKey(a => a.OwnerId)
            .HasConstraintName("fk_identity_owner")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
