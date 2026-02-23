using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.AudId);

        builder.Property(a => a.AudId).HasColumnName("aud_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(a => a.AudEntityName).HasColumnName("aud_entity_name").HasMaxLength(100).IsRequired();
        builder.Property(a => a.AudEntityId).HasColumnName("aud_entity_id").IsRequired();
        builder.Property(a => a.AudActionType).HasColumnName("aud_action_type").HasMaxLength(50).IsRequired();
        builder.Property(a => a.AudPerformedByUserId).HasColumnName("aud_performed_by_user_id").IsRequired();
        builder.Property(a => a.AudPerformedAt).HasColumnName("aud_performed_at").HasDefaultValueSql("now()");
        builder.Property(a => a.AudOldValues).HasColumnName("aud_old_values").HasColumnType("jsonb");
        builder.Property(a => a.AudNewValues).HasColumnName("aud_new_values").HasColumnType("jsonb");

        builder.HasIndex(a => new { a.AudEntityName, a.AudEntityId });
        builder.HasIndex(a => a.AudPerformedByUserId);
        builder.HasIndex(a => a.AudPerformedAt);

        builder.HasOne(a => a.PerformedByUser)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(a => a.AudPerformedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
