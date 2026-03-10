using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

public class ObligationConfiguration : IEntityTypeConfiguration<Obligation>
{
    public void Configure(EntityTypeBuilder<Obligation> builder)
    {
        builder.ToTable("obligations");

        builder.HasKey(o => o.OblId);

        builder.Property(o => o.OblId).HasColumnName("obl_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(o => o.OblProjectId).HasColumnName("obl_project_id").IsRequired();
        builder.Property(o => o.OblCreatedByUserId).HasColumnName("obl_created_by_user_id").IsRequired();
        builder.Property(o => o.OblTitle).HasColumnName("obl_title").HasMaxLength(255).IsRequired();
        builder.Property(o => o.OblDescription).HasColumnName("obl_description");
        builder.Property(o => o.OblTotalAmount).HasColumnName("obl_total_amount").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(o => o.OblCurrency).HasColumnName("obl_currency").HasMaxLength(3).IsRequired();
        builder.Property(o => o.OblDueDate).HasColumnName("obl_due_date");
        builder.Property(o => o.OblCreatedAt).HasColumnName("obl_created_at").HasDefaultValueSql("now()");
        builder.Property(o => o.OblUpdatedAt).HasColumnName("obl_updated_at").HasDefaultValueSql("now()");
        builder.Property(o => o.OblIsDeleted).HasColumnName("obl_is_deleted").HasDefaultValue(false);
        builder.Property(o => o.OblDeletedAt).HasColumnName("obl_deleted_at");
        builder.Property(o => o.OblDeletedByUserId).HasColumnName("obl_deleted_by_user_id");

        builder.HasIndex(o => o.OblProjectId);
        builder.HasIndex(o => o.OblCreatedByUserId);
        builder.HasIndex(o => o.OblIsDeleted);

        builder.HasOne(o => o.Project)
            .WithMany(p => p.Obligations)
            .HasForeignKey(o => o.OblProjectId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(o => o.CreatedByUser)
            .WithMany(u => u.CreatedObligations)
            .HasForeignKey(o => o.OblCreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(o => o.DeletedByUser)
            .WithMany()
            .HasForeignKey(o => o.OblDeletedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(o => o.Currency)
            .WithMany(c => c.ObligationsWithCurrency)
            .HasForeignKey(o => o.OblCurrency)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
