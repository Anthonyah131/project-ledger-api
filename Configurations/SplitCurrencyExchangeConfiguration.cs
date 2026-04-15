using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

/// <summary>
/// Entity Framework configuration for the SplitCurrencyExchange model.
/// </summary>
public class SplitCurrencyExchangeConfiguration : IEntityTypeConfiguration<SplitCurrencyExchange>
{
    /// <summary>
    /// Configures the database schema and relationships for SplitCurrencyExchange.
    /// </summary>
    public void Configure(EntityTypeBuilder<SplitCurrencyExchange> builder)
    {
        builder.ToTable("split_currency_exchanges");

        builder.HasKey(e => e.SceId);

        builder.Property(e => e.SceId).HasColumnName("sce_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.SceExpenseSplitId).HasColumnName("sce_expense_split_id");
        builder.Property(e => e.SceIncomeSplitId).HasColumnName("sce_income_split_id");
        builder.Property(e => e.SceSettlementId).HasColumnName("sce_settlement_id");
        builder.Property(e => e.SceCurrencyCode).HasColumnName("sce_currency_code").HasMaxLength(3).IsRequired();
        builder.Property(e => e.SceExchangeRate).HasColumnName("sce_exchange_rate").HasColumnType("numeric(18,6)").IsRequired();
        builder.Property(e => e.SceConvertedAmount).HasColumnName("sce_converted_amount").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.SceCreatedAt).HasColumnName("sce_created_at").HasDefaultValueSql("now()");

        // Three-way XOR mutex: exactly one of SceExpenseSplitId / SceIncomeSplitId / SceSettlementId
        // must be NOT NULL per row, tying each currency-conversion record to exactly one parent.
        // Filtered unique indexes enforce one record per currency per parent:
        //   • (expense_split_id, currency_code) unique where expense_split_id IS NOT NULL
        //   • (income_split_id,  currency_code) unique where income_split_id  IS NOT NULL
        //   • (settlement_id,    currency_code) unique where settlement_id    IS NOT NULL
        builder.HasIndex(e => new { e.SceExpenseSplitId, e.SceCurrencyCode })
            .IsUnique()
            .HasFilter("sce_expense_split_id IS NOT NULL");

        builder.HasIndex(e => new { e.SceIncomeSplitId, e.SceCurrencyCode })
            .IsUnique()
            .HasFilter("sce_income_split_id IS NOT NULL");

        builder.HasIndex(e => new { e.SceSettlementId, e.SceCurrencyCode })
            .IsUnique()
            .HasFilter("sce_settlement_id IS NOT NULL");

        builder.HasOne(e => e.ExpenseSplit)
            .WithMany(s => s.CurrencyExchanges)
            .HasForeignKey(e => e.SceExpenseSplitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.IncomeSplit)
            .WithMany(s => s.CurrencyExchanges)
            .HasForeignKey(e => e.SceIncomeSplitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Settlement)
            .WithMany(s => s.CurrencyExchanges)
            .HasForeignKey(e => e.SceSettlementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Currency)
            .WithMany(c => c.SplitCurrencyExchanges)
            .HasForeignKey(e => e.SceCurrencyCode)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
