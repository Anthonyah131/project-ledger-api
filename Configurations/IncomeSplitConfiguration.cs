using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

public class IncomeSplitConfiguration : IEntityTypeConfiguration<IncomeSplit>
{
    public void Configure(EntityTypeBuilder<IncomeSplit> builder)
    {
        builder.ToTable("income_splits");

        builder.HasKey(e => e.InsId);

        builder.Property(e => e.InsId).HasColumnName("ins_id");
        builder.Property(e => e.InsIncomeId).HasColumnName("ins_income_id");
        builder.Property(e => e.InsPartnerId).HasColumnName("ins_partner_id");
        builder.Property(e => e.InsSplitType).HasColumnName("ins_split_type").HasMaxLength(10);
        builder.Property(e => e.InsSplitValue).HasColumnName("ins_split_value").HasColumnType("decimal(14,4)");
        builder.Property(e => e.InsResolvedAmount).HasColumnName("ins_resolved_amount").HasColumnType("decimal(14,2)");
        builder.Property(e => e.InsCreatedAt).HasColumnName("ins_created_at");
        builder.Property(e => e.InsUpdatedAt).HasColumnName("ins_updated_at");

        builder.HasIndex(e => new { e.InsIncomeId, e.InsPartnerId }).IsUnique();
        builder.HasIndex(e => e.InsIncomeId);
        builder.HasIndex(e => e.InsPartnerId);

        builder.HasOne(e => e.Income)
            .WithMany(inc => inc.Splits)
            .HasForeignKey(e => e.InsIncomeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Partner)
            .WithMany()
            .HasForeignKey(e => e.InsPartnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
