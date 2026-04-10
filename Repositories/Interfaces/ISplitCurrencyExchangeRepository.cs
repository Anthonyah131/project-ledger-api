using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository interface for SplitCurrencyExchange operations.
/// </summary>
public interface ISplitCurrencyExchangeRepository : IRepository<SplitCurrencyExchange>
{
    /// <summary>Elimina todos los SplitCurrencyExchange de todos los splits de un gasto.</summary>
    Task DeleteByExpenseIdAsync(Guid expenseId, CancellationToken ct = default);
    /// <summary>Elimina todos los SplitCurrencyExchange de todos los splits de un ingreso.</summary>
    Task DeleteByIncomeIdAsync(Guid incomeId, CancellationToken ct = default);
}
