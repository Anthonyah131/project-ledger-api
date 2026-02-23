using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("expenses");

        builder.HasKey(e => e.ExpId);

        builder.Property(e => e.ExpId).HasColumnName("exp_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.ExpProjectId).HasColumnName("exp_project_id").IsRequired();
        builder.Property(e => e.ExpCategoryId).HasColumnName("exp_category_id").IsRequired();
        builder.Property(e => e.ExpPaymentMethodId).HasColumnName("exp_payment_method_id").IsRequired();
        builder.Property(e => e.ExpCreatedByUserId).HasColumnName("exp_created_by_user_id").IsRequired();
        builder.Property(e => e.ExpObligationId).HasColumnName("exp_obligation_id");

        // Montos y moneda
        builder.Property(e => e.ExpOriginalAmount).HasColumnName("exp_original_amount").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(e => e.ExpOriginalCurrency).HasColumnName("exp_original_currency").HasMaxLength(3).IsRequired();
        builder.Property(e => e.ExpExchangeRate).HasColumnName("exp_exchange_rate").HasColumnType("numeric(18,6)").HasDefaultValue(1.000000m);
        builder.Property(e => e.ExpConvertedAmount).HasColumnName("exp_converted_amount").HasColumnType("numeric(14,2)").IsRequired();

        // Datos descriptivos
        builder.Property(e => e.ExpTitle).HasColumnName("exp_title").HasMaxLength(255).IsRequired();
        builder.Property(e => e.ExpDescription).HasColumnName("exp_description");
        builder.Property(e => e.ExpExpenseDate).HasColumnName("exp_expense_date").IsRequired();
        builder.Property(e => e.ExpReceiptNumber).HasColumnName("exp_receipt_number").HasMaxLength(100);
        builder.Property(e => e.ExpNotes).HasColumnName("exp_notes");

        // Plantilla
        builder.Property(e => e.ExpIsTemplate).HasColumnName("exp_is_template").HasDefaultValue(false);

        // Moneda alternativa
        builder.Property(e => e.ExpAltCurrency).HasColumnName("exp_alt_currency").HasMaxLength(3);
        builder.Property(e => e.ExpAltExchangeRate).HasColumnName("exp_alt_exchange_rate").HasColumnType("numeric(18,6)");
        builder.Property(e => e.ExpAltAmount).HasColumnName("exp_alt_amount").HasColumnType("numeric(14,2)");

        // Timestamps y soft delete
        builder.Property(e => e.ExpCreatedAt).HasColumnName("exp_created_at").HasDefaultValueSql("now()");
        builder.Property(e => e.ExpUpdatedAt).HasColumnName("exp_updated_at").HasDefaultValueSql("now()");
        builder.Property(e => e.ExpIsDeleted).HasColumnName("exp_is_deleted").HasDefaultValue(false);
        builder.Property(e => e.ExpDeletedAt).HasColumnName("exp_deleted_at");
        builder.Property(e => e.ExpDeletedByUserId).HasColumnName("exp_deleted_by_user_id");

        // Índices
        builder.HasIndex(e => e.ExpProjectId);
        builder.HasIndex(e => e.ExpCategoryId);
        builder.HasIndex(e => e.ExpPaymentMethodId);
        builder.HasIndex(e => e.ExpCreatedByUserId);
        builder.HasIndex(e => e.ExpExpenseDate);
        builder.HasIndex(e => e.ExpObligationId);
        builder.HasIndex(e => e.ExpIsDeleted);
        builder.HasIndex(e => e.ExpIsTemplate);
        builder.HasIndex(e => e.ExpAltCurrency).HasFilter("exp_alt_currency IS NOT NULL");

        // Relaciones
        builder.HasOne(e => e.Project)
            .WithMany(p => p.Expenses)
            .HasForeignKey(e => e.ExpProjectId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.Category)
            .WithMany(c => c.Expenses)
            .HasForeignKey(e => e.ExpCategoryId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.PaymentMethod)
            .WithMany(pm => pm.Expenses)
            .HasForeignKey(e => e.ExpPaymentMethodId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.CreatedByUser)
            .WithMany(u => u.CreatedExpenses)
            .HasForeignKey(e => e.ExpCreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.DeletedByUser)
            .WithMany()
            .HasForeignKey(e => e.ExpDeletedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.Obligation)
            .WithMany(o => o.Payments)
            .HasForeignKey(e => e.ExpObligationId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.OriginalCurrencyNavigation)
            .WithMany(c => c.ExpensesOriginalCurrency)
            .HasForeignKey(e => e.ExpOriginalCurrency)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.AltCurrencyNavigation)
            .WithMany(c => c.ExpensesAltCurrency)
            .HasForeignKey(e => e.ExpAltCurrency)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
