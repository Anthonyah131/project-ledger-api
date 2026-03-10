using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.Billing;

public class BillingStatusResponse
{
    public bool StripeEnabled { get; set; }
    public string? Reason { get; set; }
}

public class BillingUnavailableResponse
{
    public string Code { get; set; } = "STRIPE_DISABLED";
    public string Message { get; set; } = "Stripe billing is disabled by configuration.";
}

// ── Checkout Session ────────────────────────────────────────

public class CreateCheckoutSessionRequest
{
    /// <summary>ID del plan al que el usuario desea suscribirse.</summary>
    [Required]
    public Guid PlanId { get; set; }
}

public class CreateCheckoutSessionResponse
{
    /// <summary>URL de Stripe Checkout a la que el frontend debe redirigir al usuario.</summary>
    public string CheckoutUrl { get; set; } = null!;

    /// <summary>ID de la Checkout Session creada (para referencia / debugging).</summary>
    public string SessionId { get; set; } = null!;
}

// ── Subscription Management ───────────────────────────────

public class ChangeSubscriptionPlanRequest
{
    /// <summary>ID del nuevo plan al que se moverá la suscripción actual.</summary>
    [Required]
    public Guid PlanId { get; set; }

    /// <summary>
    /// Si es true, Stripe calcula prorrateo inmediato por el cambio de plan.
    /// Si es false, no se aplica ajuste de prorrateo.
    /// </summary>
    public bool Prorate { get; set; } = true;
}

public class CancelSubscriptionRequest
{
    /// <summary>
    /// true: cancela al final del período actual.
    /// false: cancela inmediatamente.
    /// </summary>
    public bool CancelAtPeriodEnd { get; set; } = true;
}

// ── Sync Plans ──────────────────────────────────────────────

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

public class StripePlanSyncResponse
{
    public IReadOnlyList<StripePlanSyncItemResponse> Items { get; set; } = [];
}

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
