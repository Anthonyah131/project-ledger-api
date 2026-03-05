using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.Billing;

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
    public DateTime? CurrentPeriodStart { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? CanceledAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
