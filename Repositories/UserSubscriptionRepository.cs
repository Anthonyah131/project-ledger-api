using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository implementation for UserSubscription operations.
/// </summary>
public class UserSubscriptionRepository : Repository<UserSubscription>, IUserSubscriptionRepository
{
    private static readonly string[] ActiveLikeStatuses = ["active", "trialing", "past_due", "incomplete"];

    public UserSubscriptionRepository(AppDbContext context) : base(context) { }

    public async Task<UserSubscription?> GetByStripeSubscriptionIdAsync(string stripeSubscriptionId, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(s => s.UssStripeSubscriptionId == stripeSubscriptionId, ct);

    public async Task<UserSubscription?> GetCurrentByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await DbSet
            .Include(s => s.Plan)
            .Where(s => s.UssUserId == userId)
            .OrderByDescending(s => s.UssUpdatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<UserSubscription?> GetByStripeCustomerIdAsync(string stripeCustomerId, CancellationToken ct = default)
        => await DbSet
            .Include(s => s.Plan)
            .Where(s => s.UssStripeCustomerId == stripeCustomerId)
            .OrderByDescending(s => s.UssUpdatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<UserSubscription>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await DbSet
            .Include(s => s.Plan)
            .Where(s => s.UssUserId == userId)
            .OrderByDescending(s => s.UssUpdatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<UserSubscription>> GetActiveLikeByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await DbSet
            .Include(s => s.Plan)
            .Where(s => s.UssUserId == userId && ActiveLikeStatuses.Contains(s.UssStatus))
            .OrderByDescending(s => s.UssUpdatedAt)
            .ToListAsync(ct);
}
