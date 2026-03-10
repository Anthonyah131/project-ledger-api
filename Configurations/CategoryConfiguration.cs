using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(c => c.CatId);

        builder.Property(c => c.CatId).HasColumnName("cat_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(c => c.CatProjectId).HasColumnName("cat_project_id").IsRequired();
        builder.Property(c => c.CatName).HasColumnName("cat_name").HasMaxLength(100).IsRequired();
        builder.Property(c => c.CatDescription).HasColumnName("cat_description");
        builder.Property(c => c.CatIsDefault).HasColumnName("cat_is_default").HasDefaultValue(false);
        builder.Property(c => c.CatBudgetAmount).HasColumnName("cat_budget_amount").HasColumnType("numeric(14,2)");
        builder.Property(c => c.CatCreatedAt).HasColumnName("cat_created_at").HasDefaultValueSql("now()");
        builder.Property(c => c.CatUpdatedAt).HasColumnName("cat_updated_at").HasDefaultValueSql("now()");
        builder.Property(c => c.CatIsDeleted).HasColumnName("cat_is_deleted").HasDefaultValue(false);
        builder.Property(c => c.CatDeletedAt).HasColumnName("cat_deleted_at");
        builder.Property(c => c.CatDeletedByUserId).HasColumnName("cat_deleted_by_user_id");

        builder.HasIndex(c => c.CatProjectId);
        builder.HasIndex(c => c.CatIsDeleted);

        // Partial UNIQUE: nombre único por proyecto entre categorías activas
        builder.HasIndex(c => new { c.CatProjectId, c.CatName })
            .IsUnique()
            .HasFilter("cat_is_deleted = false");

        builder.HasOne(c => c.Project)
            .WithMany(p => p.Categories)
            .HasForeignKey(c => c.CatProjectId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(c => c.DeletedByUser)
            .WithMany()
            .HasForeignKey(c => c.CatDeletedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
