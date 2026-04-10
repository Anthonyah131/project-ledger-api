using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

/// <summary>
/// Entity Framework configuration for the ExpenseSplit model.
/// </summary>
public class ExpenseSplitConfiguration : IEntityTypeConfiguration<ExpenseSplit>
{
    /// <summary>
    /// Configures the database schema and relationships for ExpenseSplit.
    /// </summary>
    public void Configure(EntityTypeBuilder<ExpenseSplit> builder)
    {
        builder.ToTable("expense_splits");

        builder.HasKey(e => e.ExsId);

        builder.Property(e => e.ExsId).HasColumnName("exs_id");
        builder.Property(e => e.ExsExpenseId).HasColumnName("exs_expense_id");
        builder.Property(e => e.ExsPartnerId).HasColumnName("exs_partner_id");
        builder.Property(e => e.ExsSplitType).HasColumnName("exs_split_type").HasMaxLength(10);
        builder.Property(e => e.ExsSplitValue).HasColumnName("exs_split_value").HasColumnType("decimal(14,4)");
        builder.Property(e => e.ExsResolvedAmount).HasColumnName("exs_resolved_amount").HasColumnType("decimal(14,2)");
        builder.Property(e => e.ExsCreatedAt).HasColumnName("exs_created_at");
        builder.Property(e => e.ExsUpdatedAt).HasColumnName("exs_updated_at");

        builder.HasIndex(e => new { e.ExsExpenseId, e.ExsPartnerId }).IsUnique();
        builder.HasIndex(e => e.ExsExpenseId);
        builder.HasIndex(e => e.ExsPartnerId);

        builder.HasOne(e => e.Expense)
            .WithMany(exp => exp.Splits)
            .HasForeignKey(e => e.ExsExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Partner)
            .WithMany()
            .HasForeignKey(e => e.ExsPartnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
