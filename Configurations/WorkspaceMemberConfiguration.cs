using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

public class WorkspaceMemberConfiguration : IEntityTypeConfiguration<WorkspaceMember>
{
    public void Configure(EntityTypeBuilder<WorkspaceMember> builder)
    {
        builder.ToTable("workspace_members");

        builder.HasKey(m => m.WkmId);

        builder.Property(m => m.WkmId).HasColumnName("wkm_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(m => m.WkmWorkspaceId).HasColumnName("wkm_workspace_id").IsRequired();
        builder.Property(m => m.WkmUserId).HasColumnName("wkm_user_id").IsRequired();
        builder.Property(m => m.WkmRole).HasColumnName("wkm_role").HasMaxLength(20).IsRequired();
        builder.Property(m => m.WkmJoinedAt).HasColumnName("wkm_joined_at").HasDefaultValueSql("now()");
        builder.Property(m => m.WkmCreatedAt).HasColumnName("wkm_created_at").HasDefaultValueSql("now()");
        builder.Property(m => m.WkmUpdatedAt).HasColumnName("wkm_updated_at").HasDefaultValueSql("now()");
        builder.Property(m => m.WkmIsDeleted).HasColumnName("wkm_is_deleted").HasDefaultValue(false);
        builder.Property(m => m.WkmDeletedAt).HasColumnName("wkm_deleted_at");
        builder.Property(m => m.WkmDeletedByUserId).HasColumnName("wkm_deleted_by_user_id");

        builder.HasIndex(m => m.WkmWorkspaceId);
        builder.HasIndex(m => m.WkmUserId);
        builder.HasIndex(m => m.WkmIsDeleted);

        // Partial UNIQUE: un miembro activo por workspace
        builder.HasIndex(m => new { m.WkmWorkspaceId, m.WkmUserId })
            .IsUnique()
            .HasFilter("wkm_is_deleted = false");

        builder.HasOne(m => m.Workspace)
            .WithMany(w => w.Members)
            .HasForeignKey(m => m.WkmWorkspaceId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.WkmUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(m => m.DeletedByUser)
            .WithMany()
            .HasForeignKey(m => m.WkmDeletedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
