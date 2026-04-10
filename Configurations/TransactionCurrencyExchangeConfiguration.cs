using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

/// <summary>
/// Entity Framework configuration for the TransactionCurrencyExchange model.
/// </summary>
public class TransactionCurrencyExchangeConfiguration : IEntityTypeConfiguration<TransactionCurrencyExchange>
{
    /// <summary>
    /// Configures the database schema and relationships for TransactionCurrencyExchange.
    /// </summary>
    public void Configure(EntityTypeBuilder<TransactionCurrencyExchange> builder)
    {
        builder.ToTable("transaction_currency_exchanges");

        builder.HasKey(e => e.TceId);

        builder.Property(e => e.TceId).HasColumnName("tce_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.TceExpenseId).HasColumnName("tce_expense_id");
        builder.Property(e => e.TceIncomeId).HasColumnName("tce_income_id");
        builder.Property(e => e.TceCurrencyCode).HasColumnName("tce_currency_code").HasMaxLength(3).IsRequired();
        builder.Property(e => e.TceExchangeRate).HasColumnName("tce_exchange_rate").HasColumnType("numeric(18,6)").IsRequired();
        builder.Property(e => e.TceConvertedAmount).HasColumnName("tce_converted_amount").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(e => e.TceCreatedAt).HasColumnName("tce_created_at").HasDefaultValueSql("now()");

        // Indexes — filtered to avoid null duplicates
        builder.HasIndex(e => new { e.TceExpenseId, e.TceCurrencyCode })
            .IsUnique()
            .HasFilter("tce_expense_id IS NOT NULL");

        builder.HasIndex(e => new { e.TceIncomeId, e.TceCurrencyCode })
            .IsUnique()
            .HasFilter("tce_income_id IS NOT NULL");

        // Relationships with explicit FKs
        builder.HasOne(e => e.Expense)
            .WithMany(ex => ex.CurrencyExchanges)
            .HasForeignKey(e => e.TceExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Income)
            .WithMany(i => i.CurrencyExchanges)
            .HasForeignKey(e => e.TceIncomeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Currency)
            .WithMany(c => c.TransactionCurrencyExchanges)
            .HasForeignKey(e => e.TceCurrencyCode)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
