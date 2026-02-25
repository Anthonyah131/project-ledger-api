using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IObligationService
{
    Task<Obligation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Obligation>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<(IReadOnlyList<Obligation> Items, int TotalCount)> GetByProjectIdPagedAsync(Guid projectId, int skip, int take, string? sortBy, bool descending, CancellationToken ct = default);
    Task<Obligation> CreateAsync(Obligation obligation, CancellationToken ct = default);
    Task UpdateAsync(Obligation obligation, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default);
}
