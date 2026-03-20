using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IProjectPartnerService
{
    Task<IEnumerable<ProjectPartner>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectPartner> AddAsync(Guid projectId, Guid partnerId, Guid addedByUserId, CancellationToken ct = default);
    Task RemoveAsync(Guid projectId, Guid partnerId, Guid deletedByUserId, CancellationToken ct = default);
    Task<IEnumerable<PaymentMethod>> GetAvailablePaymentMethodsAsync(Guid projectId, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<PaymentMethod>> GetLinkablePaymentMethodsAsync(Guid projectId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<(Guid PartnerId, string Name, decimal DefaultPercentage)>> GetSplitDefaultsAsync(Guid projectId, CancellationToken ct = default);
}
