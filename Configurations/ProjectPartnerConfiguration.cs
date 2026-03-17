using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

public class ProjectPartnerConfiguration : IEntityTypeConfiguration<ProjectPartner>
{
    public void Configure(EntityTypeBuilder<ProjectPartner> builder)
    {
        builder.ToTable("project_partners");

        builder.HasKey(p => p.PtpId);

        builder.Property(p => p.PtpId).HasColumnName("ptp_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(p => p.PtpProjectId).HasColumnName("ptp_project_id").IsRequired();
        builder.Property(p => p.PtpPartnerId).HasColumnName("ptp_partner_id").IsRequired();
        builder.Property(p => p.PtpAddedByUserId).HasColumnName("ptp_added_by_user_id").IsRequired();
        builder.Property(p => p.PtpCreatedAt).HasColumnName("ptp_created_at").HasDefaultValueSql("now()");
        builder.Property(p => p.PtpUpdatedAt).HasColumnName("ptp_updated_at").HasDefaultValueSql("now()");
        builder.Property(p => p.PtpIsDeleted).HasColumnName("ptp_is_deleted").HasDefaultValue(false);
        builder.Property(p => p.PtpDeletedAt).HasColumnName("ptp_deleted_at");
        builder.Property(p => p.PtpDeletedByUserId).HasColumnName("ptp_deleted_by_user_id");

        builder.HasIndex(p => p.PtpProjectId);
        builder.HasIndex(p => p.PtpPartnerId);
        builder.HasIndex(p => p.PtpIsDeleted);

        // Partial UNIQUE: un partner activo por proyecto
        builder.HasIndex(p => new { p.PtpProjectId, p.PtpPartnerId })
            .IsUnique()
            .HasFilter("ptp_is_deleted = false");

        builder.HasOne(p => p.Project)
            .WithMany()
            .HasForeignKey(p => p.PtpProjectId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(p => p.Partner)
            .WithMany()
            .HasForeignKey(p => p.PtpPartnerId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(p => p.AddedByUser)
            .WithMany()
            .HasForeignKey(p => p.PtpAddedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(p => p.DeletedByUser)
            .WithMany()
            .HasForeignKey(p => p.PtpDeletedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
