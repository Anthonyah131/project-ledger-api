using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Currency service. Read-only (ISO 4217 catalog).
/// </summary>
public class CurrencyService : ICurrencyService
{
    private readonly ICurrencyRepository _currencyRepo;

    public CurrencyService(ICurrencyRepository currencyRepo)
    {
        _currencyRepo = currencyRepo;
    }

    /// <inheritdoc />
    public async Task<Currency?> GetByCodeAsync(string code, CancellationToken ct = default)
        => await _currencyRepo.GetByCodeAsync(code.ToUpperInvariant(), ct);

    /// <inheritdoc />
    public async Task<IEnumerable<Currency>> GetAllActiveAsync(CancellationToken ct = default)
        => await _currencyRepo.GetActiveAsync(ct);
}
