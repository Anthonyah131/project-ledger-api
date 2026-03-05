namespace ProjectLedger.API.Models;

public class StripeWebhookEvent
{
    public Guid SweId { get; set; }
    public string SweStripeEventId { get; set; } = null!;
    public string SweType { get; set; } = null!;
    public bool SweProcessedSuccessfully { get; set; }
    public string? SweErrorMessage { get; set; }
    public DateTime SweCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SweProcessedAt { get; set; }
}
