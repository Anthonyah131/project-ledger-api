using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class StripeWebhookEventRepository : Repository<StripeWebhookEvent>, IStripeWebhookEventRepository
{
    public StripeWebhookEventRepository(AppDbContext context) : base(context) { }

    public async Task<StripeWebhookEvent?> GetByStripeEventIdAsync(string stripeEventId, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(e => e.SweStripeEventId == stripeEventId, ct);
}
