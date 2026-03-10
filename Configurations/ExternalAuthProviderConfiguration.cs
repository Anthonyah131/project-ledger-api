using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

public class ExternalAuthProviderConfiguration : IEntityTypeConfiguration<ExternalAuthProvider>
{
    public void Configure(EntityTypeBuilder<ExternalAuthProvider> builder)
    {
        builder.ToTable("external_auth_providers");

        builder.HasKey(e => e.EapId);

        builder.Property(e => e.EapId).HasColumnName("eap_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.EapUserId).HasColumnName("eap_user_id").IsRequired();
        builder.Property(e => e.EapProvider).HasColumnName("eap_provider").HasMaxLength(50).IsRequired();
        builder.Property(e => e.EapProviderUserId).HasColumnName("eap_provider_user_id").HasMaxLength(255).IsRequired();
        builder.Property(e => e.EapProviderEmail).HasColumnName("eap_provider_email").HasMaxLength(255);
        builder.Property(e => e.EapAccessTokenHash).HasColumnName("eap_access_token_hash");
        builder.Property(e => e.EapRefreshTokenHash).HasColumnName("eap_refresh_token_hash");
        builder.Property(e => e.EapTokenExpiresAt).HasColumnName("eap_token_expires_at");
        builder.Property(e => e.EapMetadata).HasColumnName("eap_metadata").HasColumnType("jsonb");
        builder.Property(e => e.EapCreatedAt).HasColumnName("eap_created_at").HasDefaultValueSql("now()");
        builder.Property(e => e.EapUpdatedAt).HasColumnName("eap_updated_at").HasDefaultValueSql("now()");
        builder.Property(e => e.EapIsDeleted).HasColumnName("eap_is_deleted").HasDefaultValue(false);
        builder.Property(e => e.EapDeletedAt).HasColumnName("eap_deleted_at");
        builder.Property(e => e.EapDeletedByUserId).HasColumnName("eap_deleted_by_user_id");

        builder.HasIndex(e => e.EapUserId);
        builder.HasIndex(e => e.EapIsDeleted);
        builder.HasIndex(e => new { e.EapProvider, e.EapProviderUserId }).IsUnique();

        builder.HasOne(e => e.User)
            .WithMany(u => u.ExternalAuthProviders)
            .HasForeignKey(e => e.EapUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.DeletedByUser)
            .WithMany()
            .HasForeignKey(e => e.EapDeletedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
