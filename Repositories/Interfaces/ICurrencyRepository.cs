using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface ICurrencyRepository : IRepository<Currency>
{
    Task<Currency?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IEnumerable<Currency>> GetActiveAsync(CancellationToken ct = default);
}
