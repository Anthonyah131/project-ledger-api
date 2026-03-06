using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IStripeBillingService
{
    Task<IReadOnlyList<StripePlanSyncResult>> SyncPlansAndPaymentLinksAsync(CancellationToken ct = default);
    Task ProcessWebhookAsync(string payload, string signatureHeader, CancellationToken ct = default);
    Task<UserSubscription?> GetCurrentUserSubscriptionAsync(Guid userId, CancellationToken ct = default);
    Task<UserSubscription> ChangePlanAsync(Guid userId, Guid newPlanId, bool prorate = true, CancellationToken ct = default);
    Task<UserSubscription> CancelSubscriptionAsync(Guid userId, bool cancelAtPeriodEnd = true, CancellationToken ct = default);

    /// <summary>
    /// Crea una Stripe Checkout Session vinculada al usuario autenticado.
    /// Garantiza que client_reference_id = userId, lo que permite al webhook
    /// asociar la suscripción al usuario de forma determinista.
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
