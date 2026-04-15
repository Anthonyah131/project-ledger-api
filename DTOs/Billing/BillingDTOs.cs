using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.Billing;

/// <summary>Indicates whether Stripe billing is enabled and the reason if it is not.</summary>
public class BillingStatusResponse
{
    public bool StripeEnabled { get; set; }
    public string? Reason { get; set; }
}

/// <summary>Standard error response returned by billing endpoints when Stripe is disabled by configuration.</summary>
public class BillingUnavailableResponse
{
    public string Code { get; set; } = "STRIPE_DISABLED";
    public string Message { get; set; } = "Stripe billing is disabled by configuration.";
}

// ── Checkout Session ────────────────────────────────────────

public class CreateCheckoutSessionRequest
{
    /// <summary>ID of the plan the user wishes to subscribe to.</summary>
    [Required]
    public Guid PlanId { get; set; }
}

public class CreateCheckoutSessionResponse
{
    /// <summary>Stripe Checkout URL to which the frontend should redirect the user.</summary>
    public string CheckoutUrl { get; set; } = null!;

    /// <summary>ID of the created Checkout Session (for reference / debugging).</summary>
    public string SessionId { get; set; } = null!;
}

// ── Subscription Management ───────────────────────────────

public class ChangeSubscriptionPlanRequest
{
    /// <summary>ID of the new plan to which the current subscription will be moved.</summary>
    [Required]
    public Guid PlanId { get; set; }

    /// <summary>
    /// If true, Stripe calculates immediate proration for the plan change.
    /// If false, no proration adjustment is applied.
    /// </summary>
    public bool Prorate { get; set; } = true;
}

public class CancelSubscriptionRequest
{
    /// <summary>
    /// true: cancels at the end of the current period.
    /// false: cancels immediately.
    /// </summary>
    public bool CancelAtPeriodEnd { get; set; } = true;
}

// ── Sync Plans ──────────────────────────────────────────────

/// <summary>Result for a single plan synced from Stripe: contains the Stripe product, price, and payment link IDs.</summary>
public class StripePlanSyncItemResponse
{
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = null!;
    public string PlanSlug { get; set; } = null!;
    public decimal MonthlyPrice { get; set; }
    public string Currency { get; set; } = null!;
    public string StripeProductId { get; set; } = null!;
    public string StripePriceId { get; set; } = null!;
    public string StripePaymentLinkId { get; set; } = null!;
    public string StripePaymentLinkUrl { get; set; } = null!;
}

/// <summary>Response from the admin plan-sync endpoint: list of all plans updated from Stripe.</summary>
public class StripePlanSyncResponse
{
    public IReadOnlyList<StripePlanSyncItemResponse> Items { get; set; } = [];
}

/// <summary>
/// Current subscription details for the authenticated user, including plan info,
/// billing cycle dates, cancellation state, and Stripe identifiers.
/// </summary>
public class MySubscriptionResponse
{
    public Guid? UserId { get; set; }
    public Guid? PlanId { get; set; }
    public string? PlanName { get; set; }
    public string? PlanSlug { get; set; }
    public string StripeSubscriptionId { get; set; } = null!;
    public string? StripeCustomerId { get; set; }
    public string? StripePriceId { get; set; }
    public string Status { get; set; } = null!;
    public bool CancelAtPeriodEnd { get; set; }
    public bool AutoRenews { get; set; }
    public bool WillDowngradeToFree { get; set; }
    public DateTime? DowngradeToFreeAt { get; set; }
    public DateTime? CurrentPeriodStart { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? CanceledAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
