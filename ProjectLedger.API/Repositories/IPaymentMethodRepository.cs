using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface IPaymentMethodRepository : IRepository<PaymentMethod>
{
    Task<IEnumerable<PaymentMethod>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default);
}
