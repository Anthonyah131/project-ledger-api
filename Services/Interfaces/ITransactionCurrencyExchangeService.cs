using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface ITransactionCurrencyExchangeService
{
    /// <summary>
    /// Gets all currency exchange conversions linked to a specific transaction (Expense, Income, or Settlement).
    /// </summary>
    Task<IEnumerable<TransactionCurrencyExchange>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default);

    /// <summary>
    /// Saves multiple currency exchange requests for a transaction.
    /// </summary>
    Task SaveExchangesAsync(string entityType, Guid entityId, List<CurrencyExchangeRequest> exchanges, CancellationToken ct = default);

    /// <summary>
    /// Saves multiple currency exchange inputs for a transaction.
    /// </summary>
    Task SaveExchangesAsync(string entityType, Guid entityId, IReadOnlyList<TransactionExchangeInput> exchanges, CancellationToken ct = default);

    /// <summary>
    /// Replaces all existing currency exchanges for a transaction with a new set.
    /// </summary>
    Task ReplaceExchangesAsync(string entityType, Guid entityId, List<CurrencyExchangeRequest> exchanges, CancellationToken ct = default);
}
