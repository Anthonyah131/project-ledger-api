using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

public class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("project_members");

        builder.HasKey(pm => pm.PrmId);

        builder.Property(pm => pm.PrmId).HasColumnName("prm_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(pm => pm.PrmProjectId).HasColumnName("prm_project_id").IsRequired();
        builder.Property(pm => pm.PrmUserId).HasColumnName("prm_user_id").IsRequired();
        builder.Property(pm => pm.PrmRole).HasColumnName("prm_role").HasMaxLength(20).IsRequired();
        builder.Property(pm => pm.PrmJoinedAt).HasColumnName("prm_joined_at").HasDefaultValueSql("now()");
        builder.Property(pm => pm.PrmCreatedAt).HasColumnName("prm_created_at").HasDefaultValueSql("now()");
        builder.Property(pm => pm.PrmUpdatedAt).HasColumnName("prm_updated_at").HasDefaultValueSql("now()");
        builder.Property(pm => pm.PrmIsDeleted).HasColumnName("prm_is_deleted").HasDefaultValue(false);
        builder.Property(pm => pm.PrmDeletedAt).HasColumnName("prm_deleted_at");
        builder.Property(pm => pm.PrmDeletedByUserId).HasColumnName("prm_deleted_by_user_id");

        builder.HasIndex(pm => pm.PrmProjectId);
        builder.HasIndex(pm => pm.PrmUserId);
        builder.HasIndex(pm => pm.PrmIsDeleted);

        // Partial UNIQUE: un miembro activo por proyecto
        builder.HasIndex(pm => new { pm.PrmProjectId, pm.PrmUserId })
            .IsUnique()
            .HasFilter("prm_is_deleted = false");

        builder.HasOne(pm => pm.Project)
            .WithMany(p => p.Members)
            .HasForeignKey(pm => pm.PrmProjectId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(pm => pm.User)
            .WithMany(u => u.ProjectMemberships)
            .HasForeignKey(pm => pm.PrmUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(pm => pm.DeletedByUser)
            .WithMany()
            .HasForeignKey(pm => pm.PrmDeletedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
