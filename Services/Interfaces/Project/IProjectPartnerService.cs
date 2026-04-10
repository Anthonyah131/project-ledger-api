using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IProjectPartnerService
{
    /// <summary>
    /// Gets all financial partners associated with a project.
    /// </summary>
    Task<IEnumerable<ProjectPartner>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Links a partner to a project, allowing them to be part of transaction splits.
    /// </summary>
    Task<ProjectPartner> AddAsync(Guid projectId, Guid partnerId, Guid addedByUserId, CancellationToken ct = default);

    /// <summary>
    /// Removes a partner's association with a project.
    /// </summary>
    Task RemoveAsync(Guid projectId, Guid partnerId, Guid deletedByUserId, CancellationToken ct = default);

    /// <summary>
    /// Returns all available payment methods for the active user in the project scope.
    /// </summary>
    Task<IEnumerable<PaymentMethod>> GetAvailablePaymentMethodsAsync(Guid projectId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Returns all payment methods owned by the user that can be linked to this project.
    /// </summary>
    Task<IEnumerable<PaymentMethod>> GetLinkablePaymentMethodsAsync(Guid projectId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the default settlement split percentages for all partners in the project.
    /// </summary>
    Task<IReadOnlyList<(Guid PartnerId, string Name, decimal DefaultPercentage)>> GetSplitDefaultsAsync(Guid projectId, CancellationToken ct = default);
}
