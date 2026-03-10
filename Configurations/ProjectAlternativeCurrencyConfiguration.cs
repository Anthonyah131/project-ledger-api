using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

public class ProjectAlternativeCurrencyConfiguration : IEntityTypeConfiguration<ProjectAlternativeCurrency>
{
    public void Configure(EntityTypeBuilder<ProjectAlternativeCurrency> builder)
    {
        builder.ToTable("project_alternative_currencies");

        builder.HasKey(e => e.PacId);

        builder.Property(e => e.PacId).HasColumnName("pac_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.PacProjectId).HasColumnName("pac_project_id").IsRequired();
        builder.Property(e => e.PacCurrencyCode).HasColumnName("pac_currency_code").HasMaxLength(3).IsRequired();
        builder.Property(e => e.PacCreatedAt).HasColumnName("pac_created_at").HasDefaultValueSql("now()");

        // Índices
        builder.HasIndex(e => e.PacProjectId);
        builder.HasIndex(e => new { e.PacProjectId, e.PacCurrencyCode }).IsUnique();

        // Relaciones
        builder.HasOne(e => e.Project)
            .WithMany(p => p.AlternativeCurrencies)
            .HasForeignKey(e => e.PacProjectId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.Currency)
            .WithMany(c => c.ProjectAlternativeCurrencies)
            .HasForeignKey(e => e.PacCurrencyCode)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
