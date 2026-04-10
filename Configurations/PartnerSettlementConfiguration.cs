using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

/// <summary>
/// Entity Framework configuration for the PartnerSettlement model.
/// </summary>
public class PartnerSettlementConfiguration : IEntityTypeConfiguration<PartnerSettlement>
{
    /// <summary>
    /// Configures the database schema and relationships for PartnerSettlement.
    /// </summary>
    public void Configure(EntityTypeBuilder<PartnerSettlement> builder)
    {
        builder.ToTable("partner_settlements");

        builder.HasKey(p => p.PstId);

        builder.Property(p => p.PstId)
            .HasColumnName("pst_id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.PstProjectId).HasColumnName("pst_project_id").IsRequired();
        builder.Property(p => p.PstFromPartnerId).HasColumnName("pst_from_partner_id").IsRequired();
        builder.Property(p => p.PstToPartnerId).HasColumnName("pst_to_partner_id").IsRequired();

        builder.Property(p => p.PstAmount)
            .HasColumnName("pst_amount")
            .HasColumnType("decimal(14,2)")
            .IsRequired();

        builder.Property(p => p.PstCurrency)
            .HasColumnName("pst_currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(p => p.PstExchangeRate)
            .HasColumnName("pst_exchange_rate")
            .HasColumnType("decimal(18,6)")
            .HasDefaultValue(1m)
            .IsRequired();

        builder.Property(p => p.PstConvertedAmount)
            .HasColumnName("pst_converted_amount")
            .HasColumnType("decimal(14,2)")
            .IsRequired();

        builder.Property(p => p.PstSettlementDate).HasColumnName("pst_settlement_date").IsRequired();
        builder.Property(p => p.PstDescription).HasColumnName("pst_description").HasMaxLength(500);
        builder.Property(p => p.PstNotes).HasColumnName("pst_notes");
        builder.Property(p => p.PstCreatedByUserId).HasColumnName("pst_created_by_user_id").IsRequired();

        builder.Property(p => p.PstCreatedAt)
            .HasColumnName("pst_created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(p => p.PstUpdatedAt)
            .HasColumnName("pst_updated_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(p => p.PstIsDeleted).HasColumnName("pst_is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(p => p.PstDeletedAt).HasColumnName("pst_deleted_at");
        builder.Property(p => p.PstDeletedByUserId).HasColumnName("pst_deleted_by_user_id");

        // ── Foreign keys ─────────────────────────────────────
        builder.HasOne(p => p.Project)
            .WithMany()
            .HasForeignKey(p => p.PstProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.FromPartner)
            .WithMany()
            .HasForeignKey(p => p.PstFromPartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.ToPartner)
            .WithMany()
            .HasForeignKey(p => p.PstToPartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Currency)
            .WithMany()
            .HasForeignKey(p => p.PstCurrency)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.CreatedByUser)
            .WithMany()
            .HasForeignKey(p => p.PstCreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.DeletedByUser)
            .WithMany()
            .HasForeignKey(p => p.PstDeletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ──────────────────────────────────────────
        builder.HasIndex(p => p.PstProjectId).HasDatabaseName("idx_pst_project_id");
        builder.HasIndex(p => p.PstFromPartnerId).HasDatabaseName("idx_pst_from_partner_id");
        builder.HasIndex(p => p.PstToPartnerId).HasDatabaseName("idx_pst_to_partner_id");
        builder.HasIndex(p => p.PstSettlementDate).HasDatabaseName("idx_pst_date");
        builder.HasIndex(p => p.PstIsDeleted).HasDatabaseName("idx_pst_is_deleted");
    }
}
