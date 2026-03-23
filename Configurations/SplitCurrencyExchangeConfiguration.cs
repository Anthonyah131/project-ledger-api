using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

public class SplitCurrencyExchangeConfiguration : IEntityTypeConfiguration<SplitCurrencyExchange>
{
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

        // Índices únicos filtrados — evita duplicados de moneda por fuente
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
