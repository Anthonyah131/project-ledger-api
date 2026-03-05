using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

public class StripeWebhookEventConfiguration : IEntityTypeConfiguration<StripeWebhookEvent>
{
    public void Configure(EntityTypeBuilder<StripeWebhookEvent> builder)
    {
        builder.ToTable("stripe_webhook_events");

        builder.HasKey(e => e.SweId);

        builder.Property(e => e.SweId).HasColumnName("swe_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.SweStripeEventId).HasColumnName("swe_stripe_event_id").HasMaxLength(255).IsRequired();
        builder.Property(e => e.SweType).HasColumnName("swe_type").HasMaxLength(100).IsRequired();
        builder.Property(e => e.SweProcessedSuccessfully).HasColumnName("swe_processed_successfully").HasDefaultValue(false);
        builder.Property(e => e.SweErrorMessage).HasColumnName("swe_error_message");
        builder.Property(e => e.SweCreatedAt).HasColumnName("swe_created_at").HasDefaultValueSql("now()");
        builder.Property(e => e.SweProcessedAt).HasColumnName("swe_processed_at");

        builder.HasIndex(e => e.SweStripeEventId).IsUnique();
        builder.HasIndex(e => e.SweCreatedAt);
    }
}
