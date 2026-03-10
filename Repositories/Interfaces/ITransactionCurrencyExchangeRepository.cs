using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface ITransactionCurrencyExchangeRepository : IRepository<TransactionCurrencyExchange>
{
    Task<IEnumerable<TransactionCurrencyExchange>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default);
    Task<IEnumerable<TransactionCurrencyExchange>> GetByEntitiesAsync(string entityType, IEnumerable<Guid> entityIds, CancellationToken ct = default);
    Task DeleteByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default);
}
