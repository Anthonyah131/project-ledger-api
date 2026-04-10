using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository interface for Obligation operations.
/// </summary>
public interface IObligationRepository : IRepository<Obligation>
{
    Task<IEnumerable<Obligation>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<IEnumerable<Obligation>> GetByProjectIdWithPaymentsAsync(Guid projectId, CancellationToken ct = default);
    Task<(IReadOnlyList<Obligation> Items, int TotalCount)> GetByProjectIdPagedAsync(Guid projectId, int skip, int take, string? sortBy, bool descending, CancellationToken ct = default);
}
