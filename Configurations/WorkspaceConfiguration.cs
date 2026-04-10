using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

/// <summary>
/// Entity Framework configuration for the Workspace model.
/// </summary>
public class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    /// <summary>
    /// Configures the database schema and relationships for Workspace.
    /// </summary>
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("workspaces");

        builder.HasKey(w => w.WksId);

        builder.Property(w => w.WksId).HasColumnName("wks_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(w => w.WksName).HasColumnName("wks_name").HasMaxLength(255).IsRequired();
        builder.Property(w => w.WksOwnerUserId).HasColumnName("wks_owner_user_id").IsRequired();
        builder.Property(w => w.WksDescription).HasColumnName("wks_description");
        builder.Property(w => w.WksColor).HasColumnName("wks_color").HasMaxLength(7);
        builder.Property(w => w.WksIcon).HasColumnName("wks_icon").HasMaxLength(50);
        builder.Property(w => w.WksCreatedAt).HasColumnName("wks_created_at").HasDefaultValueSql("now()");
        builder.Property(w => w.WksUpdatedAt).HasColumnName("wks_updated_at").HasDefaultValueSql("now()");
        builder.Property(w => w.WksIsDeleted).HasColumnName("wks_is_deleted").HasDefaultValue(false);
        builder.Property(w => w.WksDeletedAt).HasColumnName("wks_deleted_at");
        builder.Property(w => w.WksDeletedByUserId).HasColumnName("wks_deleted_by_user_id");

        builder.HasIndex(w => w.WksOwnerUserId);
        builder.HasIndex(w => w.WksIsDeleted);

        builder.HasOne(w => w.OwnerUser)
            .WithMany()
            .HasForeignKey(w => w.WksOwnerUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(w => w.DeletedByUser)
            .WithMany()
            .HasForeignKey(w => w.WksDeletedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
