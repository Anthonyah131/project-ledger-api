using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface ITransactionCurrencyExchangeService
{
    Task<IEnumerable<TransactionCurrencyExchange>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default);
    Task SaveExchangesAsync(string entityType, Guid entityId, List<CurrencyExchangeRequest> exchanges, CancellationToken ct = default);
    Task ReplaceExchangesAsync(string entityType, Guid entityId, List<CurrencyExchangeRequest> exchanges, CancellationToken ct = default);
}
