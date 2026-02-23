using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.UsrId);

        builder.Property(u => u.UsrId).HasColumnName("usr_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(u => u.UsrEmail).HasColumnName("usr_email").HasMaxLength(255).IsRequired();
        builder.Property(u => u.UsrPasswordHash).HasColumnName("usr_password_hash");
        builder.Property(u => u.UsrFullName).HasColumnName("usr_full_name").HasMaxLength(255).IsRequired();
        builder.Property(u => u.UsrPlanId).HasColumnName("usr_plan_id").IsRequired();
        builder.Property(u => u.UsrIsActive).HasColumnName("usr_is_active").HasDefaultValue(false);
        builder.Property(u => u.UsrIsAdmin).HasColumnName("usr_is_admin").HasDefaultValue(false);
        builder.Property(u => u.UsrAvatarUrl).HasColumnName("usr_avatar_url");
        builder.Property(u => u.UsrLastLoginAt).HasColumnName("usr_last_login_at");
        builder.Property(u => u.UsrCreatedAt).HasColumnName("usr_created_at").HasDefaultValueSql("now()");
        builder.Property(u => u.UsrUpdatedAt).HasColumnName("usr_updated_at").HasDefaultValueSql("now()");
        builder.Property(u => u.UsrIsDeleted).HasColumnName("usr_is_deleted").HasDefaultValue(false);
        builder.Property(u => u.UsrDeletedAt).HasColumnName("usr_deleted_at");
        builder.Property(u => u.UsrDeletedByUserId).HasColumnName("usr_deleted_by_user_id");

        builder.HasIndex(u => u.UsrEmail).IsUnique();
        builder.HasIndex(u => u.UsrIsDeleted);
        builder.HasIndex(u => u.UsrPlanId);

        // FK → Plans
        builder.HasOne(u => u.Plan)
            .WithMany(p => p.Users)
            .HasForeignKey(u => u.UsrPlanId)
            .OnDelete(DeleteBehavior.NoAction);

        // Self-reference: deleted by
        builder.HasOne(u => u.DeletedByUser)
            .WithMany()
            .HasForeignKey(u => u.UsrDeletedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
