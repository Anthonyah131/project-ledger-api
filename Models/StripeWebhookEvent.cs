namespace ProjectLedger.API.Models;

/// <summary>
/// Idempotency log for incoming Stripe webhook events.
/// Each event is recorded before processing so that duplicate deliveries
/// (Stripe may send the same event more than once) are detected and skipped.
/// </summary>
public class StripeWebhookEvent
{
    /// <summary>Internal primary key.</summary>
    public Guid SweId { get; set; }

    /// <summary>Stripe event ID (e.g. <c>evt_xxx</c>). Used to deduplicate incoming webhooks.</summary>
    public string SweStripeEventId { get; set; } = null!;

    /// <summary>Stripe event type (e.g. <c>checkout.session.completed</c>).</summary>
    public string SweType { get; set; } = null!;

    /// <summary>True if the event was processed without errors; false if it failed or was skipped.</summary>
    public bool SweProcessedSuccessfully { get; set; }

    /// <summary>Error message captured when processing failed. Null on success.</summary>
    public string? SweErrorMessage { get; set; }

    /// <summary>UTC timestamp when this record was created (i.e. when the webhook arrived).</summary>
    public DateTime SweCreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the event was processed. Null if not yet processed.</summary>
    public DateTime? SweProcessedAt { get; set; }
}
