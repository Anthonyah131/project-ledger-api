using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface IStripeWebhookEventRepository : IRepository<StripeWebhookEvent>
{
    Task<StripeWebhookEvent?> GetByStripeEventIdAsync(string stripeEventId, CancellationToken ct = default);
}
