using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IProjectAlternativeCurrencyService
{
    Task<IEnumerable<ProjectAlternativeCurrency>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectAlternativeCurrency> AddAsync(Guid projectId, string currencyCode, CancellationToken ct = default);
    Task RemoveAsync(Guid projectId, string currencyCode, CancellationToken ct = default);
}
