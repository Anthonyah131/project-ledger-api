using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository interface for ProjectPaymentMethod operations.
/// </summary>
public interface IProjectPaymentMethodRepository : IRepository<ProjectPaymentMethod>
{
    Task<IEnumerable<ProjectPaymentMethod>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<IEnumerable<ProjectPaymentMethod>> GetByPaymentMethodIdAsync(Guid paymentMethodId, CancellationToken ct = default);
    Task<ProjectPaymentMethod?> GetByProjectAndPaymentMethodAsync(Guid projectId, Guid paymentMethodId, CancellationToken ct = default);
    Task<bool> IsPaymentMethodLinkedToProjectAsync(Guid projectId, Guid paymentMethodId, CancellationToken ct = default);
}
