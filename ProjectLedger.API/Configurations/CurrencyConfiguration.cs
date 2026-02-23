using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

public class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.ToTable("currencies");

        builder.HasKey(c => c.CurCode);

        builder.Property(c => c.CurCode).HasColumnName("cur_code").HasMaxLength(3).IsRequired();
        builder.Property(c => c.CurName).HasColumnName("cur_name").HasMaxLength(100).IsRequired();
        builder.Property(c => c.CurSymbol).HasColumnName("cur_symbol").HasMaxLength(10).IsRequired();
        builder.Property(c => c.CurDecimalPlaces).HasColumnName("cur_decimal_places").HasDefaultValue((short)2);
        builder.Property(c => c.CurIsActive).HasColumnName("cur_is_active").HasDefaultValue(true);
        builder.Property(c => c.CurCreatedAt).HasColumnName("cur_created_at").HasDefaultValueSql("now()");
    }
}
