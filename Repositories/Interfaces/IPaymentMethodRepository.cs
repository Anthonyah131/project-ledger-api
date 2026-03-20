using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface IPaymentMethodRepository : IRepository<PaymentMethod>
{
    Task<IEnumerable<PaymentMethod>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<(decimal TotalIncome, decimal TotalExpenses)> GetProjectBalanceAsync(Guid pmtId, Guid projectId, CancellationToken ct = default);
    Task<bool> IsLinkedToAnyProjectAsync(Guid pmtId, CancellationToken ct = default);
}
