using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

/// <summary>
/// Entity Framework configuration for the Project model.
/// </summary>
public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    /// <summary>
    /// Configures the database schema and relationships for Project.
    /// </summary>
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(p => p.PrjId);

        builder.Property(p => p.PrjId).HasColumnName("prj_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(p => p.PrjName).HasColumnName("prj_name").HasMaxLength(255).IsRequired();
        builder.Property(p => p.PrjOwnerUserId).HasColumnName("prj_owner_user_id").IsRequired();
        builder.Property(p => p.PrjCurrencyCode).HasColumnName("prj_currency_code").HasMaxLength(3).IsRequired();
        builder.Property(p => p.PrjDescription).HasColumnName("prj_description");
        builder.Property(p => p.PrjCreatedAt).HasColumnName("prj_created_at").HasDefaultValueSql("now()");
        builder.Property(p => p.PrjUpdatedAt).HasColumnName("prj_updated_at").HasDefaultValueSql("now()");
        builder.Property(p => p.PrjIsDeleted).HasColumnName("prj_is_deleted").HasDefaultValue(false);
        builder.Property(p => p.PrjDeletedAt).HasColumnName("prj_deleted_at");
        builder.Property(p => p.PrjDeletedByUserId).HasColumnName("prj_deleted_by_user_id");

        // ── Workspace ──────────────────────────────────────────
        builder.Property(p => p.PrjWorkspaceId).HasColumnName("prj_workspace_id");
        builder.Property(p => p.PrjPartnersEnabled).HasColumnName("prj_partners_enabled").HasDefaultValue(false);

        builder.HasIndex(p => p.PrjOwnerUserId);
        builder.HasIndex(p => p.PrjIsDeleted);

        builder.HasOne(p => p.OwnerUser)
            .WithMany(u => u.OwnedProjects)
            .HasForeignKey(p => p.PrjOwnerUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(p => p.DeletedByUser)
            .WithMany()
            .HasForeignKey(p => p.PrjDeletedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(p => p.Currency)
            .WithMany(c => c.ProjectsWithCurrency)
            .HasForeignKey(p => p.PrjCurrencyCode)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(p => p.Workspace)
            .WithMany(w => w.Projects)
            .HasForeignKey(p => p.PrjWorkspaceId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
