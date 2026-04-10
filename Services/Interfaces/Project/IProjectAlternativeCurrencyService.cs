using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IProjectAlternativeCurrencyService
{
    /// <summary>
    /// Gets all alternative currencies configured for a specific project.
    /// </summary>
    Task<IEnumerable<ProjectAlternativeCurrency>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Adds a new alternative currency to the project.
    /// </summary>
    Task<ProjectAlternativeCurrency> AddAsync(Guid projectId, string currencyCode, CancellationToken ct = default);

    /// <summary>
    /// Removes an alternative currency from the project.
    /// </summary>
    Task RemoveAsync(Guid projectId, string currencyCode, CancellationToken ct = default);
}
