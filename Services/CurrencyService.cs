using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de monedas. Solo lectura (catálogo ISO 4217).
/// </summary>
public class CurrencyService : ICurrencyService
{
    private readonly ICurrencyRepository _currencyRepo;

    public CurrencyService(ICurrencyRepository currencyRepo)
    {
        _currencyRepo = currencyRepo;
    }

    public async Task<Currency?> GetByCodeAsync(string code, CancellationToken ct = default)
        => await _currencyRepo.GetByCodeAsync(code.ToUpperInvariant(), ct);

    public async Task<IEnumerable<Currency>> GetAllActiveAsync(CancellationToken ct = default)
        => await _currencyRepo.GetActiveAsync(ct);
}
