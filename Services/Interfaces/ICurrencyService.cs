using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface ICurrencyService
{
    /// <summary>
    /// Gets a currency by its 3-character ISO code (e.g., "USD").
    /// </summary>
    Task<Currency?> GetByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Returns all available active currencies.
    /// </summary>
    Task<IEnumerable<Currency>> GetAllActiveAsync(CancellationToken ct = default);
}
