namespace ProjectLedger.API.Models;

public class UserSubscription
{
    public Guid UssId { get; set; }
    public Guid? UssUserId { get; set; }
    public Guid? UssPlanId { get; set; }
    public string UssStripeSubscriptionId { get; set; } = null!;
    public string? UssStripeCustomerId { get; set; }
    public string? UssStripePriceId { get; set; }
    public string UssStatus { get; set; } = null!;
    public DateTime? UssCurrentPeriodStart { get; set; }
    public DateTime? UssCurrentPeriodEnd { get; set; }
    public bool UssCancelAtPeriodEnd { get; set; }
    public DateTime? UssCanceledAt { get; set; }
    public DateTime UssCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UssUpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public Plan? Plan { get; set; }
}
