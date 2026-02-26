using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");

        builder.HasKey(t => t.PrtId);

        builder.Property(t => t.PrtId).HasColumnName("prt_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(t => t.PrtUserId).HasColumnName("prt_user_id").IsRequired();
        builder.Property(t => t.PrtCodeHash).HasColumnName("prt_code_hash").IsRequired();
        builder.Property(t => t.PrtExpiresAt).HasColumnName("prt_expires_at").IsRequired();
        builder.Property(t => t.PrtUsedAt).HasColumnName("prt_used_at");
        builder.Property(t => t.PrtCreatedAt).HasColumnName("prt_created_at").HasDefaultValueSql("now()");

        builder.HasIndex(t => t.PrtUserId);
        builder.HasIndex(t => t.PrtExpiresAt);

        builder.HasOne(t => t.User)
            .WithMany(u => u.PasswordResetTokens)
            .HasForeignKey(t => t.PrtUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
