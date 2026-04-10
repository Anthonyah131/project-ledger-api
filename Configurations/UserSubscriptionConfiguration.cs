using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Configurations;

/// <summary>
/// Entity Framework configuration for the UserSubscription model.
/// </summary>
public class UserSubscriptionConfiguration : IEntityTypeConfiguration<UserSubscription>
{
    /// <summary>
    /// Configures the database schema and relationships for UserSubscription.
    /// </summary>
    public void Configure(EntityTypeBuilder<UserSubscription> builder)
    {
        builder.ToTable("user_subscriptions");

        builder.HasKey(s => s.UssId);

        builder.Property(s => s.UssId).HasColumnName("uss_id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(s => s.UssUserId).HasColumnName("uss_user_id");
        builder.Property(s => s.UssPlanId).HasColumnName("uss_plan_id");
        builder.Property(s => s.UssStripeSubscriptionId).HasColumnName("uss_stripe_subscription_id").HasMaxLength(255).IsRequired();
        builder.Property(s => s.UssStripeCustomerId).HasColumnName("uss_stripe_customer_id").HasMaxLength(255);
        builder.Property(s => s.UssStripePriceId).HasColumnName("uss_stripe_price_id").HasMaxLength(255);
        builder.Property(s => s.UssStatus).HasColumnName("uss_status").HasMaxLength(50).IsRequired();
        builder.Property(s => s.UssCurrentPeriodStart).HasColumnName("uss_current_period_start");
        builder.Property(s => s.UssCurrentPeriodEnd).HasColumnName("uss_current_period_end");
        builder.Property(s => s.UssCancelAtPeriodEnd).HasColumnName("uss_cancel_at_period_end").HasDefaultValue(false);
        builder.Property(s => s.UssCanceledAt).HasColumnName("uss_canceled_at");
        builder.Property(s => s.UssCreatedAt).HasColumnName("uss_created_at").HasDefaultValueSql("now()");
        builder.Property(s => s.UssUpdatedAt).HasColumnName("uss_updated_at").HasDefaultValueSql("now()");

        builder.HasIndex(s => s.UssStripeSubscriptionId).IsUnique();
        builder.HasIndex(s => s.UssStripeCustomerId);
        builder.HasIndex(s => s.UssUserId);

        builder.HasOne(s => s.User)
            .WithMany(u => u.Subscriptions)
            .HasForeignKey(s => s.UssUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(s => s.Plan)
            .WithMany(p => p.UserSubscriptions)
            .HasForeignKey(s => s.UssPlanId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
