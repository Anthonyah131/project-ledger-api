using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface IUserSubscriptionRepository : IRepository<UserSubscription>
{
    Task<UserSubscription?> GetByStripeSubscriptionIdAsync(string stripeSubscriptionId, CancellationToken ct = default);
    Task<UserSubscription?> GetCurrentByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<UserSubscription?> GetByStripeCustomerIdAsync(string stripeCustomerId, CancellationToken ct = default);
    Task<IReadOnlyList<UserSubscription>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<UserSubscription>> GetActiveLikeByUserIdAsync(Guid userId, CancellationToken ct = default);
}
