using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IStripeBillingService
{
    /// <summary>
    /// Synchronizes plans and payment links from Stripe into the local database.
    /// </summary>
    Task<IReadOnlyList<StripePlanSyncResult>> SyncPlansAndPaymentLinksAsync(CancellationToken ct = default);

    /// <summary>
    /// Processes an incoming Stripe webhook payload after verifying its signature.
    /// handled events like checkout.session.completed, customer.subscription.deleted, etc.
    /// </summary>
    Task ProcessWebhookAsync(string payload, string signatureHeader, CancellationToken ct = default);

    /// <summary>
    /// Returns subscription state from local persistence only (no Stripe API calls).
    /// Used by billing read-only mode when Stripe is disabled.
    /// </summary>
    Task<UserSubscription?> GetCurrentUserSubscriptionReadOnlyAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the current user's subscription details, potentially refreshing from Stripe.
    /// </summary>
    Task<UserSubscription?> GetCurrentUserSubscriptionAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Updates the user's subscription to a new plan.
    /// </summary>
    Task<UserSubscription> ChangePlanAsync(Guid userId, Guid newPlanId, bool prorate = true, CancellationToken ct = default);

    /// <summary>
    /// Cancels the user's current subscription.
    /// </summary>
    Task<UserSubscription> CancelSubscriptionAsync(Guid userId, bool cancelAtPeriodEnd = true, CancellationToken ct = default);

    /// <summary>
    /// Creates a Stripe Checkout Session linked to the authenticated user.
    /// Guarantees that client_reference_id = userId, allowing the webhook
    /// to deterministically link the subscription to the user.
    /// </summary>
    Task<(string SessionId, string CheckoutUrl)> CreateCheckoutSessionAsync(
        Guid userId, string userEmail, Guid planId, CancellationToken ct = default);
}

public class StripePlanSyncResult
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
