using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

public class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("plans");

        builder.HasKey(p => p.PlnId);

        builder.Property(p => p.PlnId).HasColumnName("pln_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(p => p.PlnName).HasColumnName("pln_name").HasMaxLength(100).IsRequired();
        builder.Property(p => p.PlnSlug).HasColumnName("pln_slug").HasMaxLength(50).IsRequired();
        builder.Property(p => p.PlnDescription).HasColumnName("pln_description");
        builder.Property(p => p.PlnIsActive).HasColumnName("pln_is_active").HasDefaultValue(true);
        builder.Property(p => p.PlnDisplayOrder).HasColumnName("pln_display_order").HasDefaultValue(0);

        // Permisos
        builder.Property(p => p.PlnCanCreateProjects).HasColumnName("pln_can_create_projects").HasDefaultValue(true);
        builder.Property(p => p.PlnCanEditProjects).HasColumnName("pln_can_edit_projects").HasDefaultValue(true);
        builder.Property(p => p.PlnCanDeleteProjects).HasColumnName("pln_can_delete_projects").HasDefaultValue(true);
        builder.Property(p => p.PlnCanShareProjects).HasColumnName("pln_can_share_projects").HasDefaultValue(true);
        builder.Property(p => p.PlnCanExportData).HasColumnName("pln_can_export_data").HasDefaultValue(false);
        builder.Property(p => p.PlnCanUseAdvancedReports).HasColumnName("pln_can_use_advanced_reports").HasDefaultValue(false);
        builder.Property(p => p.PlnCanUseOcr).HasColumnName("pln_can_use_ocr").HasDefaultValue(false);
        builder.Property(p => p.PlnCanUseApi).HasColumnName("pln_can_use_api").HasDefaultValue(false);
        builder.Property(p => p.PlnCanUseMultiCurrency).HasColumnName("pln_can_use_multi_currency").HasDefaultValue(true);
        builder.Property(p => p.PlnCanSetBudgets).HasColumnName("pln_can_set_budgets").HasDefaultValue(true);

        // Límites JSONB
        builder.Property(p => p.PlnLimits).HasColumnName("pln_limits").HasColumnType("jsonb");

        builder.Property(p => p.PlnCreatedAt).HasColumnName("pln_created_at").HasDefaultValueSql("now()");
        builder.Property(p => p.PlnUpdatedAt).HasColumnName("pln_updated_at").HasDefaultValueSql("now()");

        builder.HasIndex(p => p.PlnSlug).IsUnique();
    }
}
