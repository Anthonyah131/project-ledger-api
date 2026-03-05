namespace ProjectLedger.API.Common.Settings;

public class StripeSettings
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string SuccessUrl { get; set; } = "http://localhost:5173/billing/success";
    public string CancelUrl { get; set; } = "http://localhost:5173/billing/cancel";
    public string DefaultCurrency { get; set; } = "usd";
    public bool AllowPromotionCodes { get; set; }
}
