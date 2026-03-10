using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(rt => rt.RtkId);

        builder.Property(rt => rt.RtkId).HasColumnName("rtk_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(rt => rt.RtkUserId).HasColumnName("rtk_user_id").IsRequired();
        builder.Property(rt => rt.RtkTokenHash).HasColumnName("rtk_token_hash").IsRequired();
        builder.Property(rt => rt.RtkExpiresAt).HasColumnName("rtk_expires_at").IsRequired();
        builder.Property(rt => rt.RtkRevokedAt).HasColumnName("rtk_revoked_at");
        builder.Property(rt => rt.RtkCreatedAt).HasColumnName("rtk_created_at").HasDefaultValueSql("now()");

        builder.HasIndex(rt => rt.RtkUserId);
        builder.HasIndex(rt => rt.RtkExpiresAt);

        builder.HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.RtkUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
