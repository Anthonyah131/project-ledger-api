using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

public class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.ToTable("payment_methods");

        builder.HasKey(pm => pm.PmtId);

        builder.Property(pm => pm.PmtId).HasColumnName("pmt_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(pm => pm.PmtOwnerUserId).HasColumnName("pmt_owner_user_id").IsRequired();
        builder.Property(pm => pm.PmtName).HasColumnName("pmt_name").HasMaxLength(255).IsRequired();
        builder.Property(pm => pm.PmtType).HasColumnName("pmt_type").HasMaxLength(50).IsRequired();
        builder.Property(pm => pm.PmtCurrency).HasColumnName("pmt_currency").HasMaxLength(3).IsRequired();
        builder.Property(pm => pm.PmtBankName).HasColumnName("pmt_bank_name").HasMaxLength(255);
        builder.Property(pm => pm.PmtAccountNumber).HasColumnName("pmt_account_number").HasMaxLength(100);
        builder.Property(pm => pm.PmtDescription).HasColumnName("pmt_description");
        builder.Property(pm => pm.PmtCreatedAt).HasColumnName("pmt_created_at").HasDefaultValueSql("now()");
        builder.Property(pm => pm.PmtUpdatedAt).HasColumnName("pmt_updated_at").HasDefaultValueSql("now()");
        builder.Property(pm => pm.PmtIsDeleted).HasColumnName("pmt_is_deleted").HasDefaultValue(false);
        builder.Property(pm => pm.PmtDeletedAt).HasColumnName("pmt_deleted_at");
        builder.Property(pm => pm.PmtDeletedByUserId).HasColumnName("pmt_deleted_by_user_id");

        builder.HasIndex(pm => pm.PmtOwnerUserId);
        builder.HasIndex(pm => pm.PmtIsDeleted);

        builder.HasOne(pm => pm.OwnerUser)
            .WithMany(u => u.PaymentMethods)
            .HasForeignKey(pm => pm.PmtOwnerUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(pm => pm.DeletedByUser)
            .WithMany()
            .HasForeignKey(pm => pm.PmtDeletedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(pm => pm.Currency)
            .WithMany(c => c.PaymentMethodsWithCurrency)
            .HasForeignKey(pm => pm.PmtCurrency)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
