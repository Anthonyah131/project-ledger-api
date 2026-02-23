using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IPaymentMethodService
{
    Task<PaymentMethod?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<PaymentMethod>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<PaymentMethod> CreateAsync(PaymentMethod paymentMethod, CancellationToken ct = default);
    Task UpdateAsync(PaymentMethod paymentMethod, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default);
}
