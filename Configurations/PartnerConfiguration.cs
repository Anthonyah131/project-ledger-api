using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

public class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
    public void Configure(EntityTypeBuilder<Partner> builder)
    {
        builder.ToTable("partners");

        builder.HasKey(p => p.PtrId);

        builder.Property(p => p.PtrId).HasColumnName("ptr_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(p => p.PtrOwnerUserId).HasColumnName("ptr_owner_user_id").IsRequired();
        builder.Property(p => p.PtrName).HasColumnName("ptr_name").HasMaxLength(255).IsRequired();
        builder.Property(p => p.PtrEmail).HasColumnName("ptr_email").HasMaxLength(255);
        builder.Property(p => p.PtrPhone).HasColumnName("ptr_phone").HasMaxLength(50);
        builder.Property(p => p.PtrNotes).HasColumnName("ptr_notes");
        builder.Property(p => p.PtrCreatedAt).HasColumnName("ptr_created_at").HasDefaultValueSql("now()");
        builder.Property(p => p.PtrUpdatedAt).HasColumnName("ptr_updated_at").HasDefaultValueSql("now()");
        builder.Property(p => p.PtrIsDeleted).HasColumnName("ptr_is_deleted").HasDefaultValue(false);
        builder.Property(p => p.PtrDeletedAt).HasColumnName("ptr_deleted_at");
        builder.Property(p => p.PtrDeletedByUserId).HasColumnName("ptr_deleted_by_user_id");

        builder.HasIndex(p => p.PtrOwnerUserId);
        builder.HasIndex(p => p.PtrIsDeleted);

        builder.HasOne(p => p.OwnerUser)
            .WithMany(u => u.Partners)
            .HasForeignKey(p => p.PtrOwnerUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(p => p.DeletedByUser)
            .WithMany()
            .HasForeignKey(p => p.PtrDeletedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
