using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface IPartnerRepository : IRepository<Partner>
{
    Task<IEnumerable<Partner>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Partner?> GetByIdWithPaymentMethodsAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Partner>> SearchByNameAsync(Guid userId, string? search, int skip, int take, CancellationToken ct = default);
    Task<int> CountByOwnerUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<bool> HasActivePaymentMethodsInProjectsAsync(Guid partnerId, CancellationToken ct = default);
}
