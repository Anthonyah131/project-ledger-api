using ProjectLedger.API.DTOs.PaymentMethod;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IPaymentMethodService
{
    Task<PaymentMethod?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<PaymentMethod>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<PaymentMethod> CreateAsync(PaymentMethod paymentMethod, CancellationToken ct = default);
    Task UpdateAsync(PaymentMethod paymentMethod, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default);
    Task<PaymentMethodBalanceResponse> GetProjectBalanceAsync(Guid pmtId, Guid projectId, CancellationToken ct = default);
    Task<PaymentMethod> LinkPartnerAsync(Guid pmtId, Guid partnerId, Guid userId, CancellationToken ct = default);
    Task<PaymentMethod> UnlinkPartnerAsync(Guid pmtId, Guid userId, CancellationToken ct = default);
}
