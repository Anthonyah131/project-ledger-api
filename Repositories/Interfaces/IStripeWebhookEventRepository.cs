using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository interface for StripeWebhookEvent operations.
/// </summary>
public interface IStripeWebhookEventRepository : IRepository<StripeWebhookEvent>
{
    Task<StripeWebhookEvent?> GetByStripeEventIdAsync(string stripeEventId, CancellationToken ct = default);
}
