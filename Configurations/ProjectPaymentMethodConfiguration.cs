using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

public class ProjectPaymentMethodConfiguration : IEntityTypeConfiguration<ProjectPaymentMethod>
{
    public void Configure(EntityTypeBuilder<ProjectPaymentMethod> builder)
    {
        builder.ToTable("project_payment_methods");

        builder.HasKey(ppm => ppm.PpmId);

        builder.Property(ppm => ppm.PpmId).HasColumnName("ppm_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(ppm => ppm.PpmProjectId).HasColumnName("ppm_project_id").IsRequired();
        builder.Property(ppm => ppm.PpmPaymentMethodId).HasColumnName("ppm_payment_method_id").IsRequired();
        builder.Property(ppm => ppm.PpmAddedByUserId).HasColumnName("ppm_added_by_user_id").IsRequired();
        builder.Property(ppm => ppm.PpmCreatedAt).HasColumnName("ppm_created_at").HasDefaultValueSql("now()");

        // UNIQUE: un método de pago solo puede vincularse una vez a un proyecto
        builder.HasIndex(ppm => new { ppm.PpmProjectId, ppm.PpmPaymentMethodId })
            .IsUnique();

        builder.HasIndex(ppm => ppm.PpmProjectId);
        builder.HasIndex(ppm => ppm.PpmPaymentMethodId);

        builder.HasOne(ppm => ppm.Project)
            .WithMany(p => p.ProjectPaymentMethods)
            .HasForeignKey(ppm => ppm.PpmProjectId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(ppm => ppm.PaymentMethod)
            .WithMany(pm => pm.ProjectPaymentMethods)
            .HasForeignKey(ppm => ppm.PpmPaymentMethodId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(ppm => ppm.AddedByUser)
            .WithMany()
            .HasForeignKey(ppm => ppm.PpmAddedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
