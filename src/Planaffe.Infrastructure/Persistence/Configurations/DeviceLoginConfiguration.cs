using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planaffe.Domain.Identities;

namespace Planaffe.Infrastructure.Persistence.Configurations;

/// <summary>
/// One <c>pa login</c> in progress (ADR 0025, <c>docs/storage.md</c>).
/// </summary>
/// <remarks>
/// Two indexes and both are the lookups: the device-code hash is what the CLI's
/// poll finds a row by, and the user code is what the confirmation page finds
/// one by. The second is unique among rows nobody has decided yet, the way
/// <c>one_live_secret_per_purpose</c> is: two live logins wearing the same code
/// would be a request a human could confirm the wrong half of.
/// </remarks>
public sealed class DeviceLoginConfiguration : IEntityTypeConfiguration<DeviceLogin>
{
    public void Configure(EntityTypeBuilder<DeviceLogin> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("device_login");
        builder.HasKey(x => x.Id).HasName("pk_device_login");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.DeviceCodeHash).HasColumnName("device_code_hash");
        builder.Property(x => x.UserCode).HasColumnName("user_code").HasMaxLength(UserCode.Length);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.ApprovedAt).HasColumnName("approved_at");
        builder.Property(x => x.ApprovedByUserId).HasColumnName("approved_by_user_id");
        builder.Property(x => x.DeniedAt).HasColumnName("denied_at");
        builder.Property(x => x.RedeemedAt).HasColumnName("redeemed_at");
        builder.Property(x => x.IssuedTokenId).HasColumnName("issued_token_id");

        builder.HasIndex(x => x.DeviceCodeHash).IsUnique().HasDatabaseName("device_login_code");
        builder.HasIndex(x => x.UserCode).IsUnique().HasDatabaseName("one_live_login_per_code")
            .HasFilter("approved_at is null and denied_at is null and redeemed_at is null");
        builder.HasIndex(x => x.ExpiresAt).HasDatabaseName("device_login_expiry");

        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ApprovedByUserId)
            .HasConstraintName("fk_device_login_user").OnDelete(DeleteBehavior.NoAction);
    }
}
