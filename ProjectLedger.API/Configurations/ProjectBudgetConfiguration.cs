using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

public class ProjectBudgetConfiguration : IEntityTypeConfiguration<ProjectBudget>
{
    public void Configure(EntityTypeBuilder<ProjectBudget> builder)
    {
        builder.ToTable("project_budgets");

        builder.HasKey(pb => pb.PjbId);

        builder.Property(pb => pb.PjbId).HasColumnName("pjb_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(pb => pb.PjbProjectId).HasColumnName("pjb_project_id").IsRequired();
        builder.Property(pb => pb.PjbTotalBudget).HasColumnName("pjb_total_budget").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(pb => pb.PjbAlertPercentage).HasColumnName("pjb_alert_percentage").HasColumnType("numeric(5,2)").HasDefaultValue(80.00m);
        builder.Property(pb => pb.PjbCreatedAt).HasColumnName("pjb_created_at").HasDefaultValueSql("now()");
        builder.Property(pb => pb.PjbUpdatedAt).HasColumnName("pjb_updated_at").HasDefaultValueSql("now()");
        builder.Property(pb => pb.PjbIsDeleted).HasColumnName("pjb_is_deleted").HasDefaultValue(false);
        builder.Property(pb => pb.PjbDeletedAt).HasColumnName("pjb_deleted_at");
        builder.Property(pb => pb.PjbDeletedByUserId).HasColumnName("pjb_deleted_by_user_id");

        builder.HasIndex(pb => pb.PjbProjectId);
        builder.HasIndex(pb => pb.PjbIsDeleted);

        // Partial UNIQUE: un solo presupuesto activo por proyecto
        builder.HasIndex(pb => pb.PjbProjectId)
            .IsUnique()
            .HasFilter("pjb_is_deleted = false")
            .HasDatabaseName("idx_pjb_project_active");

        builder.HasOne(pb => pb.Project)
            .WithMany(p => p.Budgets)
            .HasForeignKey(pb => pb.PjbProjectId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(pb => pb.DeletedByUser)
            .WithMany()
            .HasForeignKey(pb => pb.PjbDeletedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
