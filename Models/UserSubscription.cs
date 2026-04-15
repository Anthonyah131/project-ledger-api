namespace ProjectLedger.API.Models;

/// <summary>
/// Represents a user's active or historical Stripe subscription.
/// Created or updated by webhook handlers (<c>checkout.session.completed</c>,
/// <c>customer.subscription.updated</c>, <c>customer.subscription.deleted</c>).
/// The companion field <see cref="User.UsrPlanId"/> is kept in sync as a fast-path
/// cache — authorization handlers read that field instead of joining this table.
/// </summary>
public class UserSubscription
{
    /// <summary>Internal primary key.</summary>
    public Guid UssId { get; set; }

    /// <summary>FK to the user who owns this subscription.</summary>
    public Guid? UssUserId { get; set; }

    /// <summary>FK to the plan contracted through this subscription.</summary>
    public Guid? UssPlanId { get; set; }

    /// <summary>Stripe subscription ID (e.g. <c>sub_xxx</c>).</summary>
    public string UssStripeSubscriptionId { get; set; } = null!;

    /// <summary>Stripe customer ID (e.g. <c>cus_xxx</c>). May be null for legacy records.</summary>
    public string? UssStripeCustomerId { get; set; }

    /// <summary>Stripe price ID (e.g. <c>price_xxx</c>) corresponding to the contracted plan tier.</summary>
    public string? UssStripePriceId { get; set; }

    /// <summary>
    /// Subscription status as reported by Stripe (e.g. <c>active</c>, <c>trialing</c>,
    /// <c>canceled</c>, <c>past_due</c>).
    /// </summary>
    public string UssStatus { get; set; } = null!;

    /// <summary>UTC start of the current billing period.</summary>
    public DateTime? UssCurrentPeriodStart { get; set; }

    /// <summary>UTC end of the current billing period (next renewal date).</summary>
    public DateTime? UssCurrentPeriodEnd { get; set; }

    /// <summary>
    /// If true, the subscription will be canceled at the end of the current period
    /// rather than renewing automatically.
    /// </summary>
    public bool UssCancelAtPeriodEnd { get; set; }

    /// <summary>UTC timestamp when the subscription was canceled, if applicable.</summary>
    public DateTime? UssCanceledAt { get; set; }

    /// <summary>UTC timestamp when this record was first created.</summary>
    public DateTime UssCreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the last update (set on every webhook sync).</summary>
    public DateTime UssUpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public Plan? Plan { get; set; }
}
