using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IProjectPaymentMethodService
{
    /// <summary>
    /// Gets all payment methods linked to a specific project.
    /// </summary>
    Task<IEnumerable<ProjectPaymentMethod>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Gets all project links for a specific payment method.
    /// </summary>
    Task<IEnumerable<ProjectPaymentMethod>> GetByPaymentMethodIdAsync(Guid paymentMethodId, CancellationToken ct = default);

    /// <summary>
    /// Links a payment method to a project.
    /// </summary>
    Task<ProjectPaymentMethod> LinkAsync(ProjectPaymentMethod link, CancellationToken ct = default);

    /// <summary>
    /// Unlinks a payment method from a project.
    /// </summary>
    Task UnlinkAsync(Guid projectId, Guid linkId, CancellationToken ct = default);

    /// <summary>
    /// Checks if a payment method is already linked to a specific project.
    /// </summary>
    Task<bool> IsLinkedAsync(Guid projectId, Guid paymentMethodId, CancellationToken ct = default);
}
