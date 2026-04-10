using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository interface for ProjectAlternativeCurrency operations.
/// </summary>
public interface IProjectAlternativeCurrencyRepository : IRepository<ProjectAlternativeCurrency>
{
    Task<IEnumerable<ProjectAlternativeCurrency>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectAlternativeCurrency?> GetByProjectAndCurrencyAsync(Guid projectId, string currencyCode, CancellationToken ct = default);
    Task<int> CountByProjectIdAsync(Guid projectId, CancellationToken ct = default);
}
