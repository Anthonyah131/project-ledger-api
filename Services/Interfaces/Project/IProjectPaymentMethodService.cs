using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IProjectPaymentMethodService
{
    Task<IEnumerable<ProjectPaymentMethod>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<IEnumerable<ProjectPaymentMethod>> GetByPaymentMethodIdAsync(Guid paymentMethodId, CancellationToken ct = default);
    Task<ProjectPaymentMethod> LinkAsync(ProjectPaymentMethod link, CancellationToken ct = default);
    Task UnlinkAsync(Guid projectId, Guid linkId, CancellationToken ct = default);
    Task<bool> IsLinkedAsync(Guid projectId, Guid paymentMethodId, CancellationToken ct = default);
}
