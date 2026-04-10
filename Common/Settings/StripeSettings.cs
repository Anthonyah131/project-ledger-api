namespace ProjectLedger.API.Common.Settings;

/// <summary>
/// Configuration for Stripe payment processing.
/// </summary>
public class StripeSettings
{
    public const string SectionName = "Stripe";

    /// <summary>Whether Stripe integration is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Stripe secret key (sk_...).</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Webhook signing secret (whsec_...) for securing webhook endpoints.</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>Redirect URL after a successful Stripe checkout.</summary>
    public string SuccessUrl { get; set; } = "http://localhost:5173/billing/success";

    /// <summary>Redirect URL after a cancelled or failed Stripe checkout.</summary>
    public string CancelUrl { get; set; } = "http://localhost:5173/billing/cancel";

    /// <summary>Default currency code for Stripe transactions (e.g., 'usd').</summary>
    public string DefaultCurrency { get; set; } = "usd";

    /// <summary>Whether to allow users to apply promotion codes at checkout.</summary>
    public bool AllowPromotionCodes { get; set; }
}
