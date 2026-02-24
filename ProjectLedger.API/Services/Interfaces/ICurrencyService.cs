using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface ICurrencyService
{
    Task<Currency?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IEnumerable<Currency>> GetAllActiveAsync(CancellationToken ct = default);
}
