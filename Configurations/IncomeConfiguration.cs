using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

/// <summary>
/// Entity Framework configuration for the Income model.
/// </summary>
public class IncomeConfiguration : IEntityTypeConfiguration<Income>
{
    /// <summary>
    /// Configures the database schema and relationships for Income.
    /// </summary>
    public void Configure(EntityTypeBuilder<Income> builder)
    {
        builder.ToTable("incomes");

        builder.HasKey(e => e.IncId);

        builder.Property(e => e.IncId).HasColumnName("inc_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.IncProjectId).HasColumnName("inc_project_id").IsRequired();
        builder.Property(e => e.IncCategoryId).HasColumnName("inc_category_id").IsRequired();
        builder.Property(e => e.IncPaymentMethodId).HasColumnName("inc_payment_method_id").IsRequired();
        builder.Property(e => e.IncCreatedByUserId).HasColumnName("inc_created_by_user_id").IsRequired();

        // Amounts and currency
        builder.Property(e => e.IncOriginalAmount).HasColumnName("inc_original_amount").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(e => e.IncOriginalCurrency).HasColumnName("inc_original_currency").HasMaxLength(3).IsRequired();
        builder.Property(e => e.IncExchangeRate).HasColumnName("inc_exchange_rate").HasColumnType("numeric(18,6)").HasDefaultValue(1.000000m);
        builder.Property(e => e.IncConvertedAmount).HasColumnName("inc_converted_amount").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(e => e.IncAccountAmount).HasColumnName("inc_account_amount").HasColumnType("numeric(14,2)");
        builder.Property(e => e.IncAccountCurrency).HasColumnName("inc_account_currency").HasMaxLength(3);

        // Descriptive data
        builder.Property(e => e.IncTitle).HasColumnName("inc_title").HasMaxLength(255).IsRequired();
        builder.Property(e => e.IncDescription).HasColumnName("inc_description");
        builder.Property(e => e.IncIncomeDate).HasColumnName("inc_income_date").IsRequired();
        builder.Property(e => e.IncReceiptNumber).HasColumnName("inc_receipt_number").HasMaxLength(100);
        builder.Property(e => e.IncNotes).HasColumnName("inc_notes");
        builder.Property(e => e.IncIsActive).HasColumnName("inc_is_active").HasDefaultValue(true);

        // Timestamps and soft delete
        builder.Property(e => e.IncCreatedAt).HasColumnName("inc_created_at").HasDefaultValueSql("now()");
        builder.Property(e => e.IncUpdatedAt).HasColumnName("inc_updated_at").HasDefaultValueSql("now()");
        builder.Property(e => e.IncIsDeleted).HasColumnName("inc_is_deleted").HasDefaultValue(false);
        builder.Property(e => e.IncDeletedAt).HasColumnName("inc_deleted_at");
        builder.Property(e => e.IncDeletedByUserId).HasColumnName("inc_deleted_by_user_id");

        // Indexes
        builder.HasIndex(e => e.IncProjectId);
        builder.HasIndex(e => e.IncCategoryId);
        builder.HasIndex(e => e.IncPaymentMethodId);
        builder.HasIndex(e => e.IncCreatedByUserId);
        builder.HasIndex(e => e.IncIncomeDate);
        builder.HasIndex(e => e.IncIsDeleted);
        builder.HasIndex(e => e.IncIsActive);

        // Relationships
        builder.HasOne(e => e.Project)
            .WithMany(p => p.Incomes)
            .HasForeignKey(e => e.IncProjectId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.Category)
            .WithMany(c => c.Incomes)
            .HasForeignKey(e => e.IncCategoryId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.PaymentMethod)
            .WithMany(pm => pm.Incomes)
            .HasForeignKey(e => e.IncPaymentMethodId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.CreatedByUser)
            .WithMany(u => u.CreatedIncomes)
            .HasForeignKey(e => e.IncCreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.DeletedByUser)
            .WithMany()
            .HasForeignKey(e => e.IncDeletedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.OriginalCurrencyNavigation)
            .WithMany(c => c.IncomesOriginalCurrency)
            .HasForeignKey(e => e.IncOriginalCurrency)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
