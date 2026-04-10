using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository interface for ProjectPartner operations.
/// </summary>
public interface IProjectPartnerRepository : IRepository<ProjectPartner>
{
    Task<IEnumerable<ProjectPartner>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectPartner?> GetActiveAsync(Guid projectId, Guid partnerId, CancellationToken ct = default);
    Task<IEnumerable<PaymentMethod>> GetAvailablePaymentMethodsAsync(Guid projectId, Guid userId, CancellationToken ct = default);
    Task<bool> HasPartnerPaymentMethodsLinkedToProjectAsync(Guid projectId, Guid partnerId, CancellationToken ct = default);
    Task<bool> HasPartnerSplitsInProjectAsync(Guid projectId, Guid partnerId, CancellationToken ct = default);
    Task<bool> HasPartnerSettlementsInProjectAsync(Guid projectId, Guid partnerId, CancellationToken ct = default);
    /// <summary>
    /// Payment methods owned by a partner that IS assigned to this project
    /// and NOT yet linked to the project's payment method list.
    /// </summary>
    Task<IEnumerable<PaymentMethod>> GetLinkablePaymentMethodsAsync(Guid projectId, Guid userId, CancellationToken ct = default);
}
