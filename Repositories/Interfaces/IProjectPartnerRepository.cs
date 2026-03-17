using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface IProjectPartnerRepository : IRepository<ProjectPartner>
{
    Task<IEnumerable<ProjectPartner>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectPartner?> GetActiveAsync(Guid projectId, Guid partnerId, CancellationToken ct = default);
    Task<IEnumerable<PaymentMethod>> GetAvailablePaymentMethodsAsync(Guid projectId, Guid userId, CancellationToken ct = default);
    Task<bool> HasPartnerPaymentMethodsLinkedToProjectAsync(Guid projectId, Guid partnerId, CancellationToken ct = default);
}
