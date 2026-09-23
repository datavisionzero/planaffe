using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planaffe.Domain.Identities;

namespace Planaffe.Infrastructure.Persistence.Configurations;

/// <inheritdoc cref="IdentityConfiguration"/>
public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency");

        builder.HasKey(r => new { r.IdentityId, r.Key }).HasName("pk_idempotency");
        builder.Property(r => r.IdentityId).HasColumnName("identity_id");
        builder.Property(r => r.Key).HasColumnName("key");

        builder.HasOne<Identity>()
            .WithMany()
            .HasForeignKey(r => r.IdentityId)
            .HasConstraintName("fk_idempotency_identity")
            .OnDelete(DeleteBehavior.NoAction);

        builder.Property(r => r.RequestHash).HasColumnName("request_hash").IsRequired();
        builder.Property(r => r.Status).HasColumnName("status");
        builder.Property(r => r.Body).HasColumnName("body").HasColumnType("jsonb");
        builder.Property(r => r.Withheld).HasColumnName("withheld").IsRequired().HasDefaultValue(false);
        builder.Property(r => r.Location).HasColumnName("location");
        builder.Property(r => r.ETag).HasColumnName("etag");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();

        // The purge looks for rows older than a day, across every identity.
        builder.HasIndex(r => r.CreatedAt).HasDatabaseName("idempotency_created_at");
    }
}
